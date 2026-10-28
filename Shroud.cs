using System;
using Il2CppInterop.Runtime;
using ProjectM;
using ProjectM.Network;
using ProjectM.Shared;
using ProjectM.Terrain;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace NoCloakShroudBuff;

// Gives the Shroud of the Forest buff to players in the Cursed Forest who have killed Ben the Old Wanderer, without the cloak.
// The vanilla curse script only checks for the buff: with it the curse debuff stays at 1 stack (Phase 1, 2026-10-28).
public class ShroudKeeper : MonoBehaviour
{
    // Game 1.1.15 prefabs; names are checked at start (Prefabs.Check).
    internal static readonly PrefabGUID ShroudBuff = new(1504279833);  // EquipBuff_ShroudOfTheForest
    internal static readonly PrefabGUID ShroudCloak = new(1063517722); // Item_Cloak_Main_ShroudOfTheForest
    internal static readonly PrefabGUID Ben = new(109969450);          // CHAR_Villager_CursedWanderer_VBlood

    float _next;

    public ShroudKeeper(IntPtr ptr) : base(ptr) { }

    void Update()
    {
        if (Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + Math.Max(1, Plugin.CheckIntervalSeconds.Value);
        var world = Plugin.ServerWorld;
        if (world == null || !world.IsCreated) return;
        try
        {
            CheckPlayers(world);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Player check failed: {e}");
        }
    }

    static void CheckPlayers(World world)
    {
        var em = world.EntityManager;
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

                // With the cloak on, the game manages the buff itself. Without it, any Shroud buff the player has is ours.
                if (WearsCloak(em, character)) continue;
                var want = Plugin.Enabled.Value && InCursedForest(em, userEntity) && KilledBen(em, userEntity);
                var buff = FindBuff(em, character, ShroudBuff);
                var who = user.CharacterName.ToString();

                if (want && buff == Entity.Null)
                {
                    var des = world.GetExistingSystemManaged<DebugEventsSystem>();
                    des.ApplyBuff(new FromCharacter { User = userEntity, Character = character }, new ApplyBuffDebugEvent { BuffPrefabGUID = ShroudBuff });
                    if (Plugin.LogChanges.Value) Plugin.Logger.LogInfo($"{who}: Shroud of the Forest applied (Cursed Forest, Ben killed)");
                }
                else if (!want && buff != Entity.Null)
                {
                    DestroyUtility.Destroy(em, buff, DestroyDebugReason.TryRemoveBuff, null, 0);
                    if (Plugin.LogChanges.Value) Plugin.Logger.LogInfo($"{who}: Shroud of the Forest removed (left the Cursed Forest, or mod disabled)");
                }
            }
        }
        finally
        {
            users.Dispose();
            query.Dispose();
        }
    }

    static bool WearsCloak(EntityManager em, Entity character) =>
        em.HasComponent<Equipment>(character) && em.GetComponentData<Equipment>(character).CloakSlot.SlotId == ShroudCloak;

    // The region is on the user entity (Phase 1).
    static bool InCursedForest(EntityManager em, Entity userEntity) =>
        em.HasComponent<CurrentWorldRegion>(userEntity) && em.GetComponentData<CurrentWorldRegion>(userEntity).CurrentRegion == WorldRegionType.CursedForest;

    // Unlocked V Bloods are on the progression entity of the user (Phase 1).
    static bool KilledBen(EntityManager em, Entity userEntity)
    {
        if (!em.HasComponent<ProgressionMapper>(userEntity)) return false;
        var progression = em.GetComponentData<ProgressionMapper>(userEntity).ProgressionEntity.GetEntityOnServer();
        if (progression == Entity.Null || !em.HasBuffer<UnlockedVBlood>(progression)) return false;
        var buffer = em.GetBuffer<UnlockedVBlood>(progression);
        for (int i = 0; i < buffer.Length; i++)
            if (buffer[i].VBlood == Ben) return true;
        return false;
    }

    static Entity FindBuff(EntityManager em, Entity character, PrefabGUID guid)
    {
        if (!em.HasBuffer<BuffBuffer>(character)) return Entity.Null;
        var buffer = em.GetBuffer<BuffBuffer>(character);
        for (int i = 0; i < buffer.Length; i++)
            if (buffer[i].PrefabGuid == guid && em.Exists(buffer[i].Entity)) return buffer[i].Entity;
        return Entity.Null;
    }

    // Logs a warning and returns false if a prefab is missing or renamed (e.g. after a game patch).
    internal static bool CheckPrefabs(World world)
    {
        var lookup = world.GetExistingSystemManaged<PrefabCollectionSystem>()._PrefabLookupMap;
        var ok = true;
        foreach (var (guid, name) in new[] { (ShroudBuff, "EquipBuff_ShroudOfTheForest"), (ShroudCloak, "Item_Cloak_Main_ShroudOfTheForest"), (Ben, "CHAR_Villager_CursedWanderer_VBlood") })
        {
            var actual = lookup.GetName(guid);
            if (actual == name) continue;
            Plugin.Logger.LogWarning($"Prefab {guid.GuidHash} should be {name} but is '{actual ?? "missing"}'; the mod stays off. A game update may have changed it.");
            ok = false;
        }
        return ok;
    }
}
