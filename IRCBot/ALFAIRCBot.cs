﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Net;
using System.IO;
using Meebey.SmartIrc4net;
using MySql.Data.MySqlClient;
using MySql.Data.Types;
using MySql.Data.Common;

namespace ALFAIRCBot
{
    internal class ALFAIRCBot
    {

        public ALFAIRCBot()
        {
            Client = new IrcClient();
            Rng = new Random();
            Client.OnChannelMessage += new IrcEventHandler(Client_OnChannelMessage);
            Client.OnErrorMessage += new IrcEventHandler(Client_OnErrorMessage);
            Client.OnError += new Meebey.SmartIrc4net.ErrorEventHandler(Client_OnError);
            Client.OnRawMessage += new IrcEventHandler(Client_OnRawMessage);
        }

        public void Run()
        {
            SetConnectionString();

            for (; ; )
            {
                Console.WriteLine("Connecting to server...");

                try
                {
                    string ClientVersion = "ALFAIRCBotv" + Assembly.GetExecutingAssembly().GetName().Version.ToString();

                    Client.ActiveChannelSyncing = true;
                    Client.AutoRejoinOnKick = true;
                    Client.AutoNickHandling = true;

                    Client.CtcpVersion = ClientVersion;
                    Client.Connect(ServerHostname, ServerPort);
                    Client.Login(Nickname, "ALFAStatus", 4, "ALFAStatus");
                    Client.SendMessage(SendType.Message, "NickServ", "identify " + NickservPassword);
                    Client.RfcJoin(HomeChannel);
                    Client.Listen();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception: {0}", e);
                }

                if (Client.IsConnected)
                    Client.Disconnect();

                Console.WriteLine("Waiting to reconnect...");

                Thread.Sleep(60000);
            }
        }

        public string Nickname { get; set; }
        public string ServerHostname { get; set; }
        public int ServerPort { get; set; }
        public string HomeChannel { get; set; }
        public string NickservPassword { get; set; }

        public string DatabaseServer { get; set; }
        public string DatabaseUser { get;set; }
        public string DatabasePassword { get; set; }
        public string DatabaseSchema { get; set; }

        public string BingAppID { get; set; }

        private void SetConnectionString()
        {
            ConnectionString = String.Format("Server={0};Uid={1};Password={2};Database={3};Max Pool Size=2;Pooling=true;Allow Batch=true",
                DatabaseServer,
                DatabaseUser,
                DatabasePassword,
                DatabaseSchema);
        }

        private void Client_OnChannelMessage(object sender, IrcEventArgs e)
        {
            if (e.Data.Channel != HomeChannel)
                return;

//          Console.WriteLine("Channel {0}: [{1}] {2}", e.Data.Channel, e.Data.From, e.Data.Message);

            if (e.Data.Message == "!players")
            {
                try
                {
                    OnCommandPlayers(e.Data.Channel);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception {0} handling !players query.", ex);

                    Client.SendMessage(SendType.Message, HomeChannel, "Internal error handling !players request.  The database server may be offline or unreachable.");
                }
            }
            else if (e.Data.Message.StartsWith("!roll "))
            {
                OnCommandRoll(e.Data.Channel, e.Data.Message.Substring(6));
            }
            else if (e.Data.Message.StartsWith("!weather "))
            {
                OnCommandWeather(e.Data.Channel, e.Data.Message.Substring(9));
            }
            else if (e.Data.Message.StartsWith("!google "))
            {
                OnCommandGoogle(e.Data.Channel, e.Data.Message.Substring(8));
            }
            else if (e.Data.Message.StartsWith("!bing "))
            {
                OnCommandBing(e.Data.Channel, e.Data.Message.Substring(6));
            }
            else if (e.Data.Message.StartsWith("!wikipedia "))
            {
                OnCommandWikipedia(e.Data.Channel, e.Data.Message.Substring(11));
            }
            else if (e.Data.Message.StartsWith("!srd "))
            {
                OnCommandSrd(e.Data.Channel, e.Data.Message.Substring(5));
            }
        }

        private void Client_OnErrorMessage(object sender, IrcEventArgs e)
        {
            Console.WriteLine("Error message: {0}", e.Data.Message);
        }

        private void Client_OnError(object sender, Meebey.SmartIrc4net.ErrorEventArgs e)
        {
            Console.WriteLine("Error: {0}", e.ErrorMessage);
        }

        private void Client_OnRawMessage(object sender, IrcEventArgs e)
        {
            Console.WriteLine("Raw: {0}", e.Data.RawMessage);
        }

        private class SERVER_DATA
        {
            public string Name;
            public int ServerId;
            public int Players;
            public int DMs;
            public int HealthStatus;
            public bool VaultOnline;
        }

        private static string GetServerHealthStatusString(int HealthStatus)
        {
            switch (HealthStatus)
            {

                case 0:
                    return "";

                case 1:
                    return " [medium latency]";

                case 2:
                    return " [high latency ";

                default:
                    return " [unknown latency state]";

            }
        }

        private void OnCommandPlayers(string Channel)
        {
            Dictionary<int, SERVER_DATA> ServerInfoTable = new Dictionary<int, SERVER_DATA>();
            SERVER_DATA ServerData = new SERVER_DATA();

            string QueryFmt =
                "SELECT " +
                    "COUNT(`characters`.`ID`) AS character_count, " +
                    "`servers`.`Name` AS server_name, " +
                    "`servers`.`ID` AS server_id, " +
                    "`pwdata_health`.`Value` as server_health_status, " +
                    "`pwdata_vault`.`Value` as server_vault_status " +
                "FROM `characters` " +
                "INNER JOIN `players` ON `players`.`ID` = `characters`.`PlayerID` " +
                "INNER JOIN `servers` ON `servers`.`ID` = `characters`.`ServerID` " +
                "INNER JOIN `pwdata` ON `pwdata`.`Name` = `servers`.`Name` " +
                "LEFT OUTER JOIN `pwdata` AS `pwdata_health` ON `pwdata_health`.`Name` = `servers`.`Name` " +
                "AND `pwdata_health`.`Key` = 'ACR_HEALTHMONITOR_STATUS' " +
                "LEFT OUTER JOIN `pwdata` AS `pwdata_vault` ON `pwdata_vault`.`Name` = `servers`.`Name` " +
                "AND `pwdata_health`.`Key` = 'ACR_HEALTHMONITOR_VAULT_STATUS' " +
                "WHERE `characters`.`IsOnline` = 1 " +
                "AND `players`.IsDM = {0} " +
                "AND `pwdata`.`Key` = 'ACR_TIME_SERVERTIME' " +
                "AND `pwdata`.`Last` >= DATE_SUB(CURRENT_TIMESTAMP, INTERVAL 10 MINUTE) " +
                "GROUP BY `characters`.`ServerID` "
                ;

            using (MySqlDataReader Reader = ExecuteQuery(String.Format(QueryFmt, 0)))
            {
                while (Reader.Read())
                {
                    int ServerId = Reader.GetInt32(2);

                    if (!ServerInfoTable.TryGetValue(ServerId, out ServerData))
                        ServerInfoTable.Add(ServerId, new SERVER_DATA());

                    ServerInfoTable[ServerId].Players = Reader.GetInt32(0);
                    ServerInfoTable[ServerId].Name = Reader.GetString(1);
                    ServerInfoTable[ServerId].ServerId = Reader.GetInt32(2);

                    if (Reader.IsDBNull(3))
                        ServerInfoTable[ServerId].HealthStatus = -1;
                    else
                        ServerInfoTable[ServerId].HealthStatus = Reader.GetInt32(3);

                    if (Reader.IsDBNull(4))
                        ServerInfoTable[ServerId].VaultOnline = true;
                    else
                    {
                        switch (Reader.GetInt32(4))
                        {

                            case 0:
                            default:
                                ServerInfoTable[ServerId].VaultOnline = true;
                                break;

                            case 1:
                                ServerInfoTable[ServerId].VaultOnline = false;
                                break;


                        }
                    }

                }
            }

            using (MySqlDataReader Reader = ExecuteQuery(String.Format(QueryFmt, 1)))
            {
                while (Reader.Read())
                {
                    int ServerId = Reader.GetInt32(2);

                    //
                    // We might have a server show up here for the first time
                    // if it had DMs only and no regular players.
                    //

                    if (!ServerInfoTable.TryGetValue(ServerId, out ServerData))
                    {
                        ServerInfoTable.Add(ServerId, new SERVER_DATA());

                        ServerInfoTable[ServerId].Players = 0;
                        ServerInfoTable[ServerId].Name = Reader.GetString(1);
                        ServerInfoTable[ServerId].ServerId = Reader.GetInt32(2);
                    }

                    ServerInfoTable[ServerId].DMs = Reader.GetInt32(0);
                }
            }

            StringBuilder Output = new StringBuilder("Servers with activity: " );
            bool First = true;

            if (ServerInfoTable.TryGetValue(3, out ServerData))
                ServerData.Name = "TSM";
            if (ServerInfoTable.TryGetValue(9, out ServerData))
                ServerData.Name = "MS";
            if (ServerInfoTable.TryGetValue(10, out ServerData))
                ServerData.Name = "BG";

            //
            // Now show the results.
            //

            foreach (SERVER_DATA Entry in ServerInfoTable.Values)
            {
                if (First == false)
                {
                    Output.Append("; ");
                }
                else
                {
                    First = false;
                }

                string VaultStatusString;

                if (Entry.VaultOnline)
                    VaultStatusString = "";
                else
                    VaultStatusString = " *VAULT DISCONNECTED*";

                Output.AppendFormat("{0}: {1} player{2}, {3} DM{4}{5}{6}",
                    Entry.Name,
                    Entry.Players,
                    Entry.Players == 1 ? "" : "s",
                    Entry.DMs,
                    Entry.DMs == 1 ? "" : "s",
                    GetServerHealthStatusString(Entry.HealthStatus),
                    VaultStatusString);
            }

            if (First)
                Client.SendMessage(SendType.Message, Channel, "No players are logged on to any servers.");
            else
                Client.SendMessage(SendType.Message, Channel, Output.ToString());

//            Console.WriteLine(Output.ToString());

            /*
            ServerInfoTable[3].Name = "TSM";
            ServerInfoTable[9].Name = "BG";
            ServerInfoTable[10].Name = "MS";

            Console.WriteLine(String.Format(
                "{0}: {1} player(s), {2}DM(s); {3}: {4} player(s) and {5} DM(s); {6}: {7} player(s) and {8} DM(s)",
                ServerInfoTable[3].Name,
                ServerInfoTable[3].Players,
                ServerInfoTable[3].DMs,
                ServerInfoTable[10].Name,
                ServerInfoTable[10].Players,
                ServerInfoTable[10].DMs,
                ServerInfoTable[9].Name,
                ServerInfoTable[9].Players,
                ServerInfoTable[9].DMs));

            Client.SendMessage(SendType.Message, HomeChannel, String.Format(
                "{0}: {1} player(s), {2}DM(s); {3}: {4} player(s) and {5} DM(s); {6}: {7} player(s) and {8} DM(s)",
                ServerInfoTable[3].Name,
                ServerInfoTable[3].Players,
                ServerInfoTable[3].DMs,
                ServerInfoTable[10].Name,
                ServerInfoTable[10].Players,
                ServerInfoTable[10].DMs,
                ServerInfoTable[9].Name,
                ServerInfoTable[9].Players,
                ServerInfoTable[9].DMs));
             */
        }

        private void OnCommandRoll(string Channel, string Cmd)
        {
            string[] CmdArgs = Cmd.Split(new char[] { 'd' });

            if (CmdArgs.Length != 2)
            {
                Client.SendMessage(SendType.Message, Channel, "Usage: !roll <count>d<sides>.");
                return;
            }

            try
            {
                int Dice = Convert.ToInt32(CmdArgs[0]);
                int Sides = Convert.ToInt32(CmdArgs[1]);
                int Sum = 0;

                if (Dice < 1 || Sides < 1 || Dice > 100)
                {
                    Client.SendMessage(SendType.Message, Channel, "Invalid arguments for !roll request.");
                    return;
                }

                for (int i = 0; i < Dice; i += 1)
                {
                    Sum += (Rng.Next() % Sides) + 1;
                }

                Client.SendMessage(SendType.Message, Channel, String.Format(
                    "Rolled {0}d{1}: {2}", Dice, Sides, Sum));
            }
            catch (Exception)
            {
                Client.SendMessage(SendType.Message, Channel, "Internal error processing !roll request.");
            }
        }

        private void OnCommandWeather(string Channel, string Query)
        {
            try
            {
                XmlDocument Document = new XmlDocument();
                string City;
                string Condition;
                string Temperature;
                string Wind;
                string Humidity;

                Document.Load("http://www.google.com/ig/api?weather=" + Uri.EscapeDataString(Query));

                XmlElement ForecastInfo = (XmlElement)Document.GetElementsByTagName("forecast_information")[0];
                XmlElement CurrentConditions = (XmlElement)Document.GetElementsByTagName("current_conditions")[0];

                City = (from XmlElement E in ForecastInfo.GetElementsByTagName("city")
                        where E.HasAttribute("data")
                        select E.GetAttribute("data")).FirstOrDefault();
                Condition = (from XmlElement E in CurrentConditions.GetElementsByTagName("condition")
                             where E.HasAttribute("data")
                             select E.GetAttribute("data")).FirstOrDefault();
                Temperature = (from XmlElement E in CurrentConditions.GetElementsByTagName("temp_c")
                               where E.HasAttribute("data")
                               select E.GetAttribute("data")).FirstOrDefault();
                Wind = (from XmlElement E in CurrentConditions.GetElementsByTagName("wind_condition")
                        where E.HasAttribute("data")
                        select E.GetAttribute("data")).FirstOrDefault();
                Humidity = (from XmlElement E in CurrentConditions.GetElementsByTagName("humidity")
                            where E.HasAttribute("data")
                            select E.GetAttribute("data")).FirstOrDefault();

                Client.SendMessage(SendType.Message, Channel, String.Format(
                    "{0}: {1}, {2}C, {3}, {4}.",
                    City,
                    Condition,
                    Temperature,
                    Wind,
                    Humidity));
            }
            catch (Exception)
            {
                Client.SendMessage(SendType.Message, Channel, String.Format("Unable to retrieve weather for {0}.", Query));
            }
        }

        private void OnCommandGoogle(string Channel, string Query)
        {
            Client.SendMessage(SendType.Message, Channel, "Not supported, using !bing " + Query);
            OnCommandBing(Channel, Query);
        }

        private const string BingAPINamespace = "{http://schemas.microsoft.com/LiveSearch/2008/04/XML/web}";

        private void OnCommandBing(string Channel, string Query)
        {
            try
            {
                XDocument Document;
                XElement SearchResult;
                string Title;
                string Description;
                string Url;

                Document = XDocument.Load(String.Format("http://api.bing.net/xml.aspx?AppId={0}&Sources=Web&Version=2.0&Market=en-US&Web.count=1&XmType=ElementBased&Web.Offset=0&Query={1}", Uri.EscapeDataString(BingAppID), Uri.EscapeDataString(Query)));

                SearchResult = (from XElement E in Document.Descendants(BingAPINamespace + "WebResult") select E).FirstOrDefault();

                if (SearchResult == null)
                {
//                  Console.WriteLine("No results found");
                    Client.SendMessage(SendType.Message, Channel, "No results.");
                    return;
                }

//              Console.WriteLine(Document.ToString());

                Title = (string)(from XElement E in SearchResult.Descendants(BingAPINamespace + "Title") select E).FirstOrDefault();
                Description = (string)(from XElement E in SearchResult.Descendants(BingAPINamespace + "Description") select E).FirstOrDefault();
                Url = (string)(from XElement E in SearchResult.Descendants(BingAPINamespace + "Url") select E).FirstOrDefault();

                Client.SendMessage(SendType.Message, Channel, String.Format("{0}: {1} - {2}", Title, Url, Description));
            }
            catch (Exception)
            {
                Client.SendMessage(SendType.Message, Channel, String.Format("Unable to retrieve search results for {0}.", Query));
            }
        }

        private const string WikipediaAPINamespace = "{http://opensearch.org/searchsuggest2}";

        private void OnCommandWikipedia(string Channel, string Query)
        {
            try
            {
                XDocument Document;
                XElement SearchResult = null;
                string Title;
                string Description;
                string Url;
                HttpWebRequest Request;
                Stream ResponseStream;

                Request = (HttpWebRequest) WebRequest.Create(String.Format("http://en.wikipedia.org/w/api.php?action=opensearch&limit=1&namespace=0&format=xml&search={0}", Uri.EscapeDataString(Query)));

                Request.UserAgent = "ALFAIRCBot/" + Assembly.GetExecutingAssembly().GetName().Version.ToString();

                ResponseStream = Request.GetResponse().GetResponseStream();

                try
                {
                    Document = XDocument.Load(ResponseStream);
                }
                finally
                {
                    ResponseStream.Close();
                }

                SearchResult = (from XElement E in Document.Descendants(WikipediaAPINamespace + "Section") select E).FirstOrDefault();

                if (SearchResult != null)
                    SearchResult = (from XElement E in SearchResult.Descendants(WikipediaAPINamespace + "Item") select E).FirstOrDefault();

                if (SearchResult == null)
                {
                    Client.SendMessage(SendType.Message, Channel, "No results.");
                    return;
                }

                Title = (string)(from XElement E in SearchResult.Descendants(WikipediaAPINamespace + "Text") select E).FirstOrDefault();
                Description = (string)(from XElement E in SearchResult.Descendants(WikipediaAPINamespace + "Description") select E).FirstOrDefault();
                Url = (string)(from XElement E in SearchResult.Descendants(WikipediaAPINamespace + "Url") select E).FirstOrDefault();

                Client.SendMessage(SendType.Message, Channel, String.Format("{0}: {1} - {2}", Title, Url, Description));
            }
            catch (Exception)
            {
                Client.SendMessage(SendType.Message, Channel, String.Format("Unable to retrieve search results for {0}.", Query));
            }
        }

        private void OnCommandSrd(string Channel, string Query)
        {
            OnCommandBing(Channel, "site:d20srd.org " + Query);
        }

        private MySqlDataReader ExecuteQuery(string Query)
        {
            return MySqlHelper.ExecuteReader(ConnectionString, Query);
        }

        private IrcClient Client;
        private Random Rng;

        private string ConnectionString;
    }
}
