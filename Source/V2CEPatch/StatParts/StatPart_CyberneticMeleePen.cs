using System.Linq;
using RimWorld;
using Verse;

namespace V2CEPatch
{
    public class StatPart_CyberneticMeleePen : StatPart
    {
        private const float Multiplier = 1.35f;

        private static GeneDef heavyGene;
        private static HediffDef overdriveDef;
        private static bool defsLookedUp;

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (!req.HasThing) return;

            Pawn wielder = GetWielder(req.Thing);
            if (wielder == null) return;

            if (HasCyberneticPenBonus(wielder))
                val *= Multiplier;
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!req.HasThing) return null;

            Pawn wielder = GetWielder(req.Thing);
            if (wielder == null) return null;

            if (!HasCyberneticPenBonus(wielder)) return null;

            return "V2CEPatch_CyberneticMeleePen".Translate() + ": x" + Multiplier.ToString("F2");
        }

        private static Pawn GetWielder(Thing weapon)
        {
            if (weapon is ThingWithComps twc)
            {
                // Check if the weapon is equipped by a pawn
                if (twc.ParentHolder is Pawn_EquipmentTracker eq)
                    return eq.pawn;
            }
            return null;
        }

        private static bool HasCyberneticPenBonus(Pawn pawn)
        {
            LookupDefs();

            if (heavyGene != null && pawn.genes?.HasActiveGene(heavyGene) == true)
                return true;

            if (overdriveDef != null && pawn.health?.hediffSet?.HasHediff(overdriveDef) == true)
                return true;

            return false;
        }

        private static void LookupDefs()
        {
            if (defsLookedUp) return;
            heavyGene = DefDatabase<GeneDef>.GetNamedSilentFail("GG_M_Heavy");
            overdriveDef = DefDatabase<HediffDef>.GetNamedSilentFail("GS_OverdriveHediff");
            defsLookedUp = true;
        }
    }
}
