﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ALFA;

namespace ACR_ServerCommunicator
{
    /// <summary>
    /// This object maintains state about a game server, gathered from the
    /// database.
    /// </summary>
    class GameServer : IGameEntity
    {

        /// <summary>
        /// Construct a new GameServer object.
        /// </summary>
        /// <param name="WorldManager">Supplies the game world manager.
        /// </param>
        public GameServer(GameWorldManager WorldManager)
        {
            this.WorldManager = WorldManager;
            this.Characters = new List<GameCharacter>();
        }

        /// <summary>
        /// Retrieve the properties of the server from the database.
        /// </summary>
        public void PopulateFromDatabase()
        {
            IALFADatabase Database = WorldManager.Database;

            Database.ACR_SQLQuery(String.Format(
                "SELECT `Name`, `IPAddress` FROM `servers` WHERE `ID` = {0}",
                ServerId));

            if (!Database.ACR_SQLFetch())
            {
                throw new ApplicationException("Failed to populate data for server " + ServerId);
            }

            ServerName = Database.ACR_SQLGetData(0);
            SetHostnameAndPort(Database.ACR_SQLGetData(1));
        }

        /// <summary>
        /// Set the hostname and port based on parsing an address string, which
        /// may be either 'hostname' or 'hostname:port'.  The default port of
        /// 5121 is used if no port was set.
        /// </summary>
        /// <param name="AddressString">Supplies the address string to set the
        /// server network address information from.</param>
        public void SetHostnameAndPort(string AddressString)
        {
            int i = AddressString.IndexOf(':');

            if (i == -1)
            {
                ServerHostname = AddressString;
                ServerPort = 5121;
                return;
            }

            ServerHostname = AddressString.Substring(0, i);
            ServerPort = Convert.ToInt32(AddressString.Substring(i + 1));
        }

        /// <summary>
        /// Get the database id of the server.
        /// </summary>
        public int DatabaseId
        {
            get { return ServerId; }
        }

        /// <summary>
        /// Get the logical name of the server.
        /// </summary>
        public string Name
        {
            get { return ServerName; }
        }

        /// <summary>
        /// The database server id.
        /// </summary>
        public int ServerId { get; set; }

        /// <summary>
        /// The database server name.
        /// </summary>
        public string ServerName { get; set; }

        /// <summary>
        /// The externally reachable network hostname for the server.
        /// </summary>
        public string ServerHostname { get; set; }

        /// <summary>
        /// The externally reachable network port for the server.
        /// </summary>
        public int ServerPort { get; set; }

        /// <summary>
        /// Characters logged on to the server are listed here.
        /// </summary>
        public List<GameCharacter> Characters { get; set; }

        /// <summary>
        /// The associated game world manager.
        /// </summary>
        private GameWorldManager WorldManager;
    }
}
