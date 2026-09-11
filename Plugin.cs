using BepInEx;

namespace ValheimDraft
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.braidedcable.valheimdraft";
        public const string PluginName = "ValheimDraft";
        public const string PluginVersion = "0.1.0";

        private void Awake()
        {
            // TODO: on scene load, iterate ZNetScene.instance.m_prefabs, read each
            // Piece's bounds + child snap-point transforms, and serialize to JSON
            // matching the schema in valheim-planner-plan.md. This is the
            // go/no-go gate spike, not part of scaffolding.
            Logger.LogInfo($"{PluginName} loaded.");
        }
    }
}
