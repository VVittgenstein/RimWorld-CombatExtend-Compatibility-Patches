using System;
using System.Reflection;
using CombatExtended;
using HarmonyLib;
using Verse;

namespace V2CEPatch
{
    [HarmonyPatch(typeof(Verb_LaunchProjectileCE), nameof(Verb_LaunchProjectileCE.TryCastShot))]
    public static class Patch_VerbLaunchProjectileCE_DurabilityLoss
    {
        private static Type heavyWeaponType;
        private static FieldInfo hpDeductionField;
        private static bool initialized;

        static void Postfix(Verb_LaunchProjectileCE __instance, bool __result)
        {
            if (!__result) return;

            ThingWithComps equipment = __instance.EquipmentSource;
            if (equipment == null) return;

            if (!initialized)
            {
                heavyWeaponType = AccessTools.TypeByName("VEF.Weapons.HeavyWeapon");
                if (heavyWeaponType != null)
                    hpDeductionField = AccessTools.Field(heavyWeaponType, "weaponHitPointsDeductionOnShot");
                initialized = true;
            }

            if (heavyWeaponType == null || hpDeductionField == null) return;

            var ext = equipment.def.modExtensions?.Find(e => heavyWeaponType.IsInstanceOfType(e));
            if (ext == null) return;

            int deduction = (int)hpDeductionField.GetValue(ext);
            if (deduction <= 0) return;

            equipment.HitPoints -= deduction;
            if (equipment.HitPoints <= 0)
            {
                equipment.HitPoints = 0;
                equipment.Destroy(DestroyMode.Vanish);
                if (__instance.CasterIsPawn)
                {
                    __instance.CasterPawn.jobs.StopAll(false, true);
                }
            }
        }
    }
}
