using RimWorld;
using Verse;

namespace V2CEPatch
{
    public static class ModDetection
    {
        public static bool DoorsExpandedActive { get; private set; }
        public static bool VWEHeavyWeaponsActive { get; private set; }
        public static bool GreyscytheCybergeneticsActive { get; private set; }
        public static bool VQEAncientsActive { get; private set; }

        public static void Init()
        {
            DoorsExpandedActive = ModsConfig.IsActive("jecrell.doorsexpanded");
            VWEHeavyWeaponsActive = ModsConfig.IsActive("VanillaExpanded.VWEHW");
            GreyscytheCybergeneticsActive = ModsConfig.IsActive("feaurie.GreyscytheGenes");
            VQEAncientsActive = ModsConfig.IsActive("VanillaExpanded.VQEA");
        }
    }
}
