using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace V2CEPatch
{
    [StaticConstructorOnStartup]
    public static class V2CEPatchMod
    {
        static V2CEPatchMod()
        {
            ModDetection.Init();

            var harmony = new Harmony("v2modpack.cepatch");

            // Doors Expanded: CollisionVertical fix
            if (ModDetection.DoorsExpandedActive)
            {
                harmony.CreateClassProcessor(typeof(Patch_CollisionVertical_CalculateHeightRange)).Patch();
                Log.Message("[V2CEPatch] Applied Doors Expanded collision fix");
            }

            // VWE Heavy Weapons: durability + swarm missile patches
            if (ModDetection.VWEHeavyWeaponsActive)
            {
                harmony.CreateClassProcessor(typeof(Patch_VerbLaunchProjectileCE_DurabilityLoss)).Patch();
                harmony.CreateClassProcessor(typeof(Patch_VerbLaunchProjectileCE_SwarmMissileHoming)).Patch();
                harmony.CreateClassProcessor(typeof(Patch_VerbLaunchProjectileCE_SwarmMissileRetarget)).Patch();
                Log.Message("[V2CEPatch] Applied VWE Heavy Weapons patches");
            }

            // GS Cybergenetics: disable native evasion, inject CE-native replacements
            if (ModDetection.GreyscytheCybergeneticsActive)
            {
                Patch_PreApplyDamage_GSEvadeDisable.Apply(harmony);
                InjectCyberneticStatParts();
                Log.Message("[V2CEPatch] Applied Greyscythe Cybergenetics patches");
            }

            // Shared hooks: apply if ANY consumer mod is active
            bool needForcedHeadshot = ModDetection.VQEAncientsActive || ModDetection.GreyscytheCybergeneticsActive;
            bool needRangedDodge = ModDetection.VQEAncientsActive || ModDetection.GreyscytheCybergeneticsActive;

            if (needForcedHeadshot)
            {
                harmony.CreateClassProcessor(typeof(Patch_ArmorUtilityCE_ForcedHeadshot)).Patch();
                Log.Message("[V2CEPatch] Applied shared headshot hook");
            }

            if (needRangedDodge)
            {
                harmony.CreateClassProcessor(typeof(Patch_ProjectileCE_RangedDodge)).Patch();
                Log.Message("[V2CEPatch] Applied shared ranged dodge hook");
            }

            Log.Message("[V2CEPatch] Initialization complete");
        }

        private static void InjectCyberneticStatParts()
        {
            var meleeDodgeStat = DefDatabase<StatDef>.GetNamedSilentFail("MeleeDodgeChance");
            if (meleeDodgeStat != null)
            {
                meleeDodgeStat.parts ??= new List<StatPart>();
                meleeDodgeStat.parts.Add(new StatPart_CyberneticMeleeDodge());
                Log.Message("[V2CEPatch] Injected StatPart_CyberneticMeleeDodge into MeleeDodgeChance");
            }

            var meleePenStat = DefDatabase<StatDef>.GetNamedSilentFail("MeleePenetrationFactor");
            if (meleePenStat != null)
            {
                meleePenStat.parts ??= new List<StatPart>();
                meleePenStat.parts.Add(new StatPart_CyberneticMeleePen());
                Log.Message("[V2CEPatch] Injected StatPart_CyberneticMeleePen into MeleePenetrationFactor");
            }
        }
    }
}
