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

    Harmony _harmony;

    public override void Load()
    {
        Logger = Log;
        Enabled = Config.Bind("General", "Enabled", true, "Set to false to turn the mod off (everything stays as in the base game).");

        // Server only: buffs are applied on the server and sync to the clients.
        if (!Paths.ProcessName.Equals("VRisingServer", StringComparison.OrdinalIgnoreCase))
        {
            Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} is a server mod; nothing to do on the client.");
            return;
        }
        _harmony = Harmony.CreateAndPatchAll(typeof(ServerReadyPatch), MyPluginInfo.PLUGIN_GUID);
        Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded on server (Enabled={Enabled.Value})");
    }

    public override bool Unload()
    {
        _harmony?.UnpatchSelf();
        return true;
    }

    // Called once, after the server world has loaded its prefabs.
    internal static void OnServerReady(World world)
    {
        if (!Enabled.Value) return;
        try
        {
            Logger.LogInfo($"Server ready (world {world.Name})");
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
