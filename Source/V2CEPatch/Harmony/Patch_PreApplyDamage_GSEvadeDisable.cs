using System.Reflection;
using HarmonyLib;
using Verse;

namespace V2CEPatch
{
    /// <summary>
    /// Disables GS_Evade's Pawn.PreApplyDamage prefix under CE.
    /// Ranged evasion → ProjectileCE.TryCollideWith dodge roll (Patch_ProjectileCE_RangedDodge)
    /// Melee evasion → StatPart_CyberneticMeleeDodge on MeleeDodgeChance
    /// </summary>
    public static class Patch_PreApplyDamage_GSEvadeDisable
    {
        public static void Apply(Harmony harmony)
        {
            MethodInfo preApplyDamage = AccessTools.Method(typeof(Pawn), "PreApplyDamage");
            if (preApplyDamage == null)
            {
                Log.Warning("[V2CEPatch] Could not find Pawn.PreApplyDamage for GS_Evade disable");
                return;
            }

            harmony.Unpatch(preApplyDamage, HarmonyPatchType.Prefix, "feaurie.GS_Core");
            Log.Message("[V2CEPatch] Disabled GS_Evade PreApplyDamage prefix (replaced by CE-native evasion)");
        }
    }
}
