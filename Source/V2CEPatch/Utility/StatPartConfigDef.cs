using Verse;

namespace V2CEPatch
{
    /// <summary>
    /// Configuration def for cybernetic StatPart declarations.
    /// Loaded from Defs/GreyscytheCybergenetics/StatParts_Cybernetic.xml
    /// when the target mod is active (via LoadFolders.xml conditional loading).
    /// </summary>
    public class StatPartConfigDef : Def
    {
        public string targetStat;
        public string sourceStat;
        public float scaleFactor = 1f;
        public string geneDefName;
        public string hediffDefName;
        public float multiplier = 1f;
    }
}
