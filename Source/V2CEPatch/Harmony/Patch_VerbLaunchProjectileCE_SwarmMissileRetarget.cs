using System.Collections.Generic;
using CombatExtended;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace V2CEPatch
{
    [HarmonyPatch(typeof(Verb_LaunchProjectileCE), nameof(Verb_LaunchProjectileCE.TryCastShot))]
    [HarmonyAfter("v2modpack.cepatch.durability")]
    public static class Patch_VerbLaunchProjectileCE_SwarmMissileRetarget
    {
        private static readonly Dictionary<Thing, HashSet<Thing>> launcherAssignedTargets = new();

        static void Postfix(Verb_LaunchProjectileCE __instance, bool __result)
        {
            if (!__result) return;

            ThingWithComps equipment = __instance.EquipmentSource;
            if (equipment?.def.defName != "VWE_Gun_SwarmMissileLauncher") return;

            ProjectileCE rocket = Patch_VerbLaunchProjectileCE_SwarmMissileHoming.lastSpawnedMissile;
            Patch_VerbLaunchProjectileCE_SwarmMissileHoming.lastSpawnedMissile = null;
            if (rocket == null) return;

            Thing launcher = __instance.Caster;
            if (!launcherAssignedTargets.ContainsKey(launcher))
                launcherAssignedTargets[launcher] = new HashSet<Thing>();

            HashSet<Thing> assigned = launcherAssignedTargets[launcher];

            Thing currentTarget = rocket.intendedTarget.Thing;
            if (currentTarget != null && assigned.Contains(currentTarget))
            {
                float searchRange = Mathf.Clamp(
                    __instance.verbProps.range * 0.66f, 2f, 20f);

                IAttackTarget altTarget = AttackTargetFinder.BestAttackTarget(
                    (IAttackTargetSearcher)launcher,
                    TargetScanFlags.NeedReachable | TargetScanFlags.NeedThreat,
                    x => x is Thing t && !assigned.Contains(t),
                    0f, searchRange);

                if (altTarget is Thing altThing)
                {
                    rocket.intendedTarget = new LocalTargetInfo(altThing);
                }
            }

            if (rocket.intendedTarget.Thing != null)
                assigned.Add(rocket.intendedTarget.Thing);

            int burstShotsLeft = (int)(AccessTools.Field(typeof(Verb), "burstShotsLeft")?.GetValue(__instance) ?? 0);
            if (burstShotsLeft <= 1)
                launcherAssignedTargets.Remove(launcher);
        }
    }
}
