//::///////////////////////////////////////////////
//:: Orb of Acid, Lesser.
//:: NX2_S0_OrbAcidLess.nss
//:://////////////////////////////////////////////
/*
    An orb of acid about 2 inches across
	shoots from your palm at its target,
	dealing 1d8 points of acid damage. You
	must succeed on a ranged touch attack
	to hit your target.
	For every two caster levels beyond
	1st, your orb deals an additional 1d8
	points of damage: 2d8 at 3rd level,
	3d8 at 5th level, 4d8 at 7th level, and
	the maximum of 5d8 at 9th level or
	higher.
	
	Fort Save failure = "sick" for one round
*/
//:://////////////////////////////////////////////
//:: Created By: Justin Reynard (JWR-OEI)
//:: Created On: Sept 3, 2008
//:://////////////////////////////////////////////
//:: RPGplayer1 12/22/2008: Added support for critical hits
//:: RPGplayer1 12/22/2008: Removed secondary effect

#include "NW_I0_SPELLS"
#include "x2_inc_spellhook"

void main()
{
	if (!X2PreSpellCastCode())
    {
    // If code within the PreSpellCastHook (i.e. UMD) reports FALSE, do not run this spell
        return;
    }

		
	object oTarget = GetSpellTargetObject();
    int nCasterLevel = GetCasterLevel(OBJECT_SELF);
	int nDice = 1;
	int nDamage = 0;
	// Calculate damage.
	if (nCasterLevel >= 9)
		nDice = 5;
	else if (nCasterLevel >= 7)
		nDice = 4;
	else if (nCasterLevel >= 5)
		nDice = 3;
	else if (nCasterLevel >= 3)
		nDice = 2;
		
	int nTouch = TouchAttackRanged(oTarget);
	if (nTouch == TOUCH_ATTACK_RESULT_CRITICAL && !GetIsImmune(oTarget, IMMUNITY_TYPE_CRITICAL_HIT))
	{
		nDice = nDice*2;
	}

	// calculate base damage	
	nDamage = d8(nDice);	
	if (spellsIsTarget(oTarget, SPELL_TARGET_STANDARDHOSTILE, OBJECT_SELF))
	{
		 SignalEvent(oTarget, EventSpellCastAt(OBJECT_SELF, SPELL_LESSER_ORB_OF_ACID));
		 //if (TouchAttackRanged(oTarget) != TOUCH_ATTACK_RESULT_MISS)
		 if (nTouch != TOUCH_ATTACK_RESULT_MISS)
		 {
		 // Orb spells are not resisted!!
		 	int nMetaMagic = GetMetaMagicFeat();
			if (nMetaMagic == METAMAGIC_MAXIMIZE)
     	    {
      		   	// do MAXIMIZE 8 * nDice
				nDamage = 8 * nDice;
       	 	}
         	if (nMetaMagic == METAMAGIC_EMPOWER)
         	{
            	// DO EMPOWER damage * 1.5
				nDamage = nDamage + (nDamage/2);
         	}
			
			// Savint throw for "sickened" effect
			/*if(!MySavingThrow(SAVING_THROW_FORT, oTarget, GetSpellSaveDC()))
			{
				// failed saving throw, uh oh! SICK!
				effect eDamageDown = EffectDamageDecrease(2);
				effect eThrow = EffectSavingThrowDecrease(SAVING_THROW_ALL, 2);
				effect eAttackDown = EffectAttackDecrease(2);
				// not linking because I think immunity to one could limit all
				ApplyEffectToObject(DURATION_TYPE_TEMPORARY, eDamageDown, oTarget, RoundsToSeconds(1));
				ApplyEffectToObject(DURATION_TYPE_TEMPORARY, eThrow, oTarget, RoundsToSeconds(1));	
				ApplyEffectToObject(DURATION_TYPE_TEMPORARY, eAttackDown, oTarget, RoundsToSeconds(1));
			}*/
			
			
			effect eDam = EffectDamage(nDamage, DAMAGE_TYPE_ACID, DAMAGE_POWER_NORMAL);
			// visual!!!!
			//effect eVis = EffectVisualEffect(VFX_HIT_SPELL_FIRE);
		    //effect eLink = EffectLinkEffects(eFireDamage, eVis);
			ApplyEffectToObject(DURATION_TYPE_INSTANT, eDam, oTarget);
			
			
			
		 	
		 }
		 
	}
	
	}