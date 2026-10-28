using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using ProjectM;
using Unity.Entities;

namespace NoCloakShroudBuff;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    internal static ManualLogSource Logger;
    internal static ConfigEntry<bool> Enabled;
    internal static ConfigEntry<int> CheckIntervalSeconds;
    internal static ConfigEntry<bool> LogChanges;
    internal static ConfigEntry<bool> Dump;
    internal static ConfigEntry<string> DumpPattern;
    internal static ConfigEntry<int> DumpIntervalSeconds;
    internal static World ServerWorld;

    internal static Plugin Instance;
    Harmony _harmony;

    public override void Load()
    {
        Logger = Log;
        Instance = this;
        Enabled = Config.Bind("General", "Enabled", true, "Set to false to turn the mod off (everything stays as in the base game).");
        CheckIntervalSeconds = Config.Bind("General", "CheckIntervalSeconds", 1, "How often (seconds) online players are checked.");
        LogChanges = Config.Bind("General", "LogChanges", true, "Log a line each time the buff is applied or removed.");
        Dump = Config.Bind("Discovery", "Dump", false,
            "Write matching prefabs to BepInEx/config/NoCloakShroudBuff/prefabs.txt at start, and log each online player's region, cloak, curse and buffs when they change.");
        DumpPattern = Config.Bind("Discovery", "DumpPattern", "Shroud|Curse|Wanderer|Forest|_VBlood$", "Regular expression for the prefab names to write (case-insensitive).");
        DumpIntervalSeconds = Config.Bind("Discovery", "DumpIntervalSeconds", 3, "How often online players are checked while Dump is on.");

        // Server only: buffs are applied on the server and sync to the clients.
        if (!Paths.ProcessName.Equals("VRisingServer", StringComparison.OrdinalIgnoreCase))
        {
            Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} is a server mod; nothing to do on the client.");
            return;
        }
        _harmony = Harmony.CreateAndPatchAll(typeof(ServerReadyPatch), MyPluginInfo.PLUGIN_GUID);
        Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded on server (Enabled={Enabled.Value}, Dump={Dump.Value})");
    }

    public override bool Unload()
    {
        _harmony?.UnpatchSelf();
        return true;
    }

    // Called once, after the server world has loaded its prefabs.
    internal static void OnServerReady(World world)
    {
        try
        {
            ServerWorld = world;
            Logger.LogInfo($"Server ready (world {world.Name})");
            if (ShroudKeeper.CheckPrefabs(world)) Instance.AddComponent<ShroudKeeper>();
            if (Dump.Value)
            {
                Discovery.WritePrefabs(world);
                Instance.AddComponent<Discovery.PlayerWatcher>();
            }
        }
        catch (Exception e)
        {
            Logger.LogError($"Server-ready handler failed: {e}");
        }
    }
}

[HarmonyPatch(typeof(SpawnTeamSystem_OnPersistenceLoad), nameof(SpawnTeamSystem_OnPersistenceLoad.OnUpdate))]
static class ServerReadyPatch
{
    static bool _done;

    static void Postfix(SpawnTeamSystem_OnPersistenceLoad __instance)
    {
        if (_done) return;
        var world = __instance.World;
        if (world == null || world.Name != "Server") return;
        var prefabs = world.GetExistingSystemManaged<PrefabCollectionSystem>();
        if (prefabs == null || !prefabs.GameDataInitialized) return;
        _done = true;
        Plugin.OnServerReady(world);
    }
}
