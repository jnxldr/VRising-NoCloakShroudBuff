using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx;
using Il2CppInterop.Runtime;
using ProjectM;
using ProjectM.Gameplay.Scripting;
using ProjectM.Network;
using ProjectM.Terrain;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace NoCloakShroudBuff;

// Discovery (Dump = true): finds the prefab IDs and shows what the game does to a player in the Cursed Forest.
static class Discovery
{
    // At server start: every prefab whose name matches DumpPattern, with its components, to BepInEx/config/NoCloakShroudBuff/prefabs.txt.
    public static void WritePrefabs(World world)
    {
        var em = world.EntityManager;
        var pcs = world.GetExistingSystemManaged<PrefabCollectionSystem>();
        var lookup = pcs._PrefabLookupMap;
        var rx = new Regex(Plugin.DumpPattern.Value, RegexOptions.IgnoreCase);

        var rows = new List<(string Name, string Line)>();
        var kvs = pcs._PrefabGuidToEntityMap.GetKeyValueArrays(Allocator.Temp);
        try
        {
            for (int i = 0; i < kvs.Length; i++)
            {
                var guid = kvs.Keys[i];
                var name = lookup.GetName(guid) ?? "";
                if (!rx.IsMatch(name)) continue;
                rows.Add((name, $"{name}  {guid.GuidHash}\n    {Components(em, kvs.Values[i])}"));
            }
        }
        finally
        {
            kvs.Dispose();
        }

        var dir = Path.Combine(Paths.ConfigPath, "NoCloakShroudBuff");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "prefabs.txt");
        File.WriteAllLines(file, rows.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).Select(r => r.Line));
        Plugin.Logger.LogInfo($"Dump: {rows.Count} prefabs matching '{Plugin.DumpPattern.Value}' written to {file}");
    }

    static string Components(EntityManager em, Entity entity)
    {
        var types = em.GetComponentTypes(entity, Allocator.Temp);
        try
        {
            var names = new List<string>();
            for (int i = 0; i < types.Length; i++)
            {
                try { names.Add(types[i].ToString()); } catch { names.Add("?"); }
            }
            return string.Join(", ", names.OrderBy(n => n));
        }
        finally
        {
            types.Dispose();
        }
    }

    // Every DumpIntervalSeconds: one line per online player whenever their region, cloak, curse state or buffs change,
    // plus their unlocked V Bloods the first time they are seen.
    public class PlayerWatcher : MonoBehaviour
    {
        readonly Dictionary<ulong, string> _last = new();
        float _next;

        public PlayerWatcher(IntPtr ptr) : base(ptr) { }

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + Math.Max(1, Plugin.DumpIntervalSeconds.Value);
            var world = Plugin.ServerWorld;
            if (world == null || !world.IsCreated) return;
            try
            {
                Scan(world);
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"Dump: player scan failed: {e}");
            }
        }

        void Scan(World world)
        {
            var em = world.EntityManager;
            var lookup = world.GetExistingSystemManaged<PrefabCollectionSystem>()._PrefabLookupMap;
            string Name(PrefabGUID g) => g.IsEmpty() ? "-" : (lookup.GetName(g) ?? g.GuidHash.ToString());

            var query = em.CreateEntityQuery(new ComponentType(Il2CppType.Of<User>(), ComponentType.AccessMode.ReadOnly));
            var users = query.ToEntityArray(Allocator.Temp);
            try
            {
                for (int u = 0; u < users.Length; u++)
                {
                    var userEntity = users[u];
                    var user = em.GetComponentData<User>(userEntity);
                    if (!user.IsConnected) continue;
                    var character = user.LocalCharacter.GetEntityOnServer();
                    if (character == Entity.Null || !em.Exists(character)) continue;
                    var who = user.CharacterName.ToString();

                    if (!_last.ContainsKey(user.PlatformId))
                    {
                        _last[user.PlatformId] = "";
                        Plugin.Logger.LogInfo($"Dump: {who} unlocked V Bloods: {UnlockedVBloods(em, userEntity, character, Name)}");
                    }

                    var region = "?";
                    if (em.HasComponent<CurrentWorldRegion>(character)) region = $"char {em.GetComponentData<CurrentWorldRegion>(character).CurrentRegion}";
                    else if (em.HasComponent<CurrentWorldRegion>(userEntity)) region = $"user {em.GetComponentData<CurrentWorldRegion>(userEntity).CurrentRegion}";

                    var cloak = "-";
                    if (em.HasComponent<Equipment>(character))
                    {
                        var eq = em.GetComponentData<Equipment>(character);
                        cloak = $"{Name(eq.CloakSlot.SlotId)} / cosmetic {Name(eq.CloakCosmeticSlot.SlotId)}";
                    }

                    var buffs = new List<string>();
                    var curse = "-";
                    if (em.HasBuffer<BuffBuffer>(character))
                    {
                        var buffer = em.GetBuffer<BuffBuffer>(character);
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            buffs.Add(Name(buffer[i].PrefabGuid));
                            var b = buffer[i].Entity;
                            if (em.Exists(b) && em.HasComponent<Script_CurseAreaDebuff_DataShared>(b))
                            {
                                var c = em.GetComponentData<Script_CurseAreaDebuff_DataShared>(b);
                                curse = $"{Name(buffer[i].PrefabGuid)} inArea={c.IsInArea} stacks={c.StackSize}";
                            }
                        }
                    }
                    buffs.Sort(StringComparer.OrdinalIgnoreCase);

                    var state = $"region={region} cloak={cloak} curse={curse} buffs=[{string.Join(", ", buffs)}]";
                    if (state == _last[user.PlatformId]) continue;
                    _last[user.PlatformId] = state;
                    var pos = em.HasComponent<LocalToWorld>(character) ? em.GetComponentData<LocalToWorld>(character).Position : default;
                    Plugin.Logger.LogInfo($"Dump: {who} at ({pos.x:F0}, {pos.z:F0}) {state}");
                }
            }
            finally
            {
                users.Dispose();
                query.Dispose();
            }
        }

        static string UnlockedVBloods(EntityManager em, Entity userEntity, Entity character, Func<PrefabGUID, string> name)
        {
            foreach (var holder in new[] { userEntity, character })
            {
                if (!em.HasComponent<ProgressionMapper>(holder)) continue;
                var progression = em.GetComponentData<ProgressionMapper>(holder).ProgressionEntity.GetEntityOnServer();
                if (progression == Entity.Null || !em.HasBuffer<UnlockedVBlood>(progression)) continue;
                var buffer = em.GetBuffer<UnlockedVBlood>(progression);
                var names = new List<string>();
                for (int i = 0; i < buffer.Length; i++) names.Add($"{name(buffer[i].VBlood)} ({buffer[i].VBlood.GuidHash})");
                names.Sort(StringComparer.OrdinalIgnoreCase);
                return $"{names.Count} via {(holder == userEntity ? "user" : "character")}: {string.Join(", ", names)}";
            }
            return "not found (no ProgressionMapper → UnlockedVBlood on user or character)";
        }
    }
}
