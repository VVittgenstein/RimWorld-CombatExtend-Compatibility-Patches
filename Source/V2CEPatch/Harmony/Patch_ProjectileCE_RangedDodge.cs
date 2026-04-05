using System.Reflection;
using CombatExtended;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace V2CEPatch
{
    [HarmonyPatch]
    public static class Patch_ProjectileCE_RangedDodge
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(ProjectileCE), "TryCollideWith");
        }

        private static GeneDef blurRunnerDef;
        private static StatDef evadeProjectileStat;
        private static bool defsLookedUp;

        static bool Prefix(ProjectileCE __instance, Thing thing, ref bool __result)
        {
            Pawn pawn = thing as Pawn;
            if (pawn == null) return true;

            if (pawn.Downed || pawn.InBed())
                return true;

            if (pawn.stances?.stunner?.Stunned == true)
                return true;

            if (!defsLookedUp)
            {
                blurRunnerDef = DefDatabase<GeneDef>.GetNamedSilentFail("VQEA_BlurRunner");
                evadeProjectileStat = DefDatabase<StatDef>.GetNamedSilentFail("GS_Evade_EvadeProjectileChance");
                defsLookedUp = true;
            }

            // Roll 1: BlurRunner (VQE Ancients) — fixed 20% dodge
            if (ModDetection.VQEAncientsActive && blurRunnerDef != null
                && pawn.genes?.HasActiveGene(blurRunnerDef) == true)
            {
                if (Rand.Chance(0.20f))
                {
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "CE_Dodge".Translate(), new Color(0f, 0.8f, 1f));
                    __result = false;
                    return false;
                }
            }

            // Roll 2: GS Cybergenetics ranged evasion — stat-scaled dodge
            if (ModDetection.GreyscytheCybergeneticsActive && evadeProjectileStat != null)
            {
                float evadeChance = pawn.GetStatValue(evadeProjectileStat);
                if (evadeChance > 0f && Rand.Chance(evadeChance))
                {
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "CE_Dodge".Translate(), new Color(0f, 1f, 0.6f));
                    __result = false;
                    return false;
                }
            }

            return true;
        }
    }
}
