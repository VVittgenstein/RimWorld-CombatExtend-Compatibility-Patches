using System;
using System.Reflection;
using CombatExtended;
using HarmonyLib;
using Verse;

namespace V2CEPatch
{
    [HarmonyPatch]
    public static class Patch_CollisionVertical_CalculateHeightRange
    {
        private static Type doorExpandedType;
        private static PropertyInfo openProperty;
        private static bool initialized;

        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(CollisionVertical), "CalculateHeightRange");
        }

        static bool Prefix(Thing thing, ref FloatRange heightRange, ref float shotHeight)
        {
            if (!initialized)
            {
                doorExpandedType = AccessTools.TypeByName("DoorsExpanded.Building_DoorExpanded");
                if (doorExpandedType != null)
                    openProperty = AccessTools.Property(doorExpandedType, "Open");
                initialized = true;
            }

            if (doorExpandedType == null || !doorExpandedType.IsInstanceOfType(thing))
                return true;

            bool isOpen = openProperty != null && (bool)openProperty.GetValue(thing);

            if (isOpen)
            {
                heightRange = new FloatRange(0f, 0f);
                shotHeight = 0f;
            }
            else
            {
                heightRange = new FloatRange(0f, CollisionVertical.WallCollisionHeight);
                shotHeight = CollisionVertical.WallCollisionHeight;
            }

            return false;
        }
    }
}
