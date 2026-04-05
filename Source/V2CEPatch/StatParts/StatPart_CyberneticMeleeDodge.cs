using RimWorld;
using Verse;

namespace V2CEPatch
{
    public class StatPart_CyberneticMeleeDodge : StatPart
    {
        private const float ScaleFactor = 0.5f;

        private static StatDef evadeMeleeStat;
        private static bool statLookedUp;

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (!req.HasThing || !(req.Thing is Pawn pawn)) return;

            float bonus = GetBonus(pawn);
            if (bonus > 0f)
                val += bonus;
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!req.HasThing || !(req.Thing is Pawn pawn)) return null;

            float bonus = GetBonus(pawn);
            if (bonus <= 0f) return null;

            return "V2CEPatch_CyberneticMeleeDodge".Translate() + ": +" + bonus.ToStringPercent();
        }

        private static float GetBonus(Pawn pawn)
        {
            if (!statLookedUp)
            {
                evadeMeleeStat = DefDatabase<StatDef>.GetNamedSilentFail("GS_Evade_EvadeMeleeChance");
                statLookedUp = true;
            }

            if (evadeMeleeStat == null) return 0f;

            float rawEvade = pawn.GetStatValue(evadeMeleeStat);
            return rawEvade * ScaleFactor;
        }
    }
}
