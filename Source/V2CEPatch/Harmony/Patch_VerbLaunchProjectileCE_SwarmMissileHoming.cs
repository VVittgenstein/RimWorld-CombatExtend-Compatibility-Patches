using System;
using System.Reflection;
using CombatExtended;
using HarmonyLib;
using Verse;

namespace V2CEPatch
{
    [HarmonyPatch]
    public static class Patch_VerbLaunchProjectileCE_SwarmMissileHoming
    {
        internal static ProjectileCE lastSpawnedMissile;

        private static FieldInfo trajectoryWorkerField;
        private static object homingWorkerInstance;
        private static bool initialized;

        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Verb_LaunchProjectileCE), "SpawnProjectile");
        }

        static void Postfix(Verb_LaunchProjectileCE __instance, ProjectileCE __result)
        {
            if (__result == null) return;

            ThingWithComps equipment = __instance.EquipmentSource;
            if (equipment?.def.defName != "VWE_Gun_SwarmMissileLauncher") return;

            if (!initialized)
            {
                trajectoryWorkerField = AccessTools.Field(typeof(ProjectileCE), "forcedTrajectoryWorker");
                var homingType = AccessTools.TypeByName("CombatExtended.HomingBulletTrajectoryWorker");
                if (homingType != null)
                {
                    var instanceProp = homingType.GetProperty("Instance",
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                    homingWorkerInstance = instanceProp?.GetValue(null);
                }
                initialized = true;
            }

            __result.homingAcceleration = 0.15f;

            if (trajectoryWorkerField != null && homingWorkerInstance != null)
            {
                trajectoryWorkerField.SetValue(__result, homingWorkerInstance);
            }

            lastSpawnedMissile = __result;
        }
    }
}
