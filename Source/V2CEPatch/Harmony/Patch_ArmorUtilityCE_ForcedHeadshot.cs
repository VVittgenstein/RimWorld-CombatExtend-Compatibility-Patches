using System.Linq;
using CombatExtended;
using HarmonyLib;
using RimWorld;
using Verse;

namespace V2CEPatch
{
    [HarmonyPatch(typeof(ArmorUtilityCE), nameof(ArmorUtilityCE.GetAfterArmorDamage))]
    public static class Patch_ArmorUtilityCE_ForcedHeadshot
    {
        private static GeneDef masterfulShootingDef;
        private static bool defLookedUp;

        static void Prefix(ref DamageInfo originalDinfo, Pawn pawn, ref BodyPartRecord hitPart)
        {
            if (!originalDinfo.Def.isRanged) return;

            Pawn attacker = originalDinfo.Instigator as Pawn;
            if (attacker?.genes == null) return;

            if (!ModDetection.VQEAncientsActive) return;

            if (!defLookedUp)
            {
                masterfulShootingDef = DefDatabase<GeneDef>.GetNamedSilentFail("VQEA_MasterfulShooting");
                defLookedUp = true;
            }

            if (masterfulShootingDef == null) return;
            if (!attacker.genes.HasActiveGene(masterfulShootingDef)) return;

            BodyPartRecord headPart = pawn.health.hediffSet
                .GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Undefined)
                .FirstOrDefault(p =>
                    p.def == BodyPartDefOf.Head ||
                    p.def.defName == "Reactor" ||
                    p.def.defName == "InsectHead");

            if (headPart != null)
            {
                hitPart = headPart;
                originalDinfo.SetBodyRegion(BodyPartHeight.Top, BodyPartDepth.Outside);
            }
        }
    }
}
