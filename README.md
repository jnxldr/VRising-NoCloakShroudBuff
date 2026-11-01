# NoCloakShroudBuff

> *What Ben could only hold back with cloak and lantern, his blood now holds back for you.*

Once you have killed **Ben the Old Wanderer**, you are protected from the **Curse of the Forest** in the Cursed Forest,
just as if you were wearing the **Shroud of the Forest**. Wear any cloak you like.

## What changes
- In the **Cursed Forest**, a player who has killed Ben gets the **Shroud of the Forest** buff, the same one the cloak gives.
  The curse stays at 1 stack instead of building up.
- The buff comes off as soon as you leave the Cursed Forest.
- Wearing the Shroud of the Forest works as in the base game; you never get the buff twice.
- Players who haven't killed Ben yet still need the cloak.

Players who killed Ben before the mod was installed are included: the game remembers every V Blood you have defeated.

## Installing
Requires [BepInExPack V Rising](https://thunderstore.io/c/v-rising/p/BepInEx/BepInExPack_V_Rising/).

- **Server:** install NoCloakShroudBuff on the server. On a dedicated server, copy `NoCloakShroudBuff.dll` into
  `BepInEx/plugins/` while the server is stopped, then start it.
- **Players:** nothing to install. The buff comes from the server.

## Settings
The config file `BepInEx/config/NoCloakShroudBuff.cfg` is created the first time the server starts with the mod.

| Setting | Default | What it does |
|---|---|---|
| `Enabled` | true | Set to `false` to turn the mod off. Buffs it gave are removed within a second |
| `CheckIntervalSeconds` | 1 | How often online players are checked |
| `LogChanges` | true | Write a line to the log each time the buff is given or removed |

### Advanced settings
| Setting | Default | What it does |
|---|---|---|
| `Dump` | false | Writes prefabs matching `DumpPattern` to `BepInEx/config/NoCloakShroudBuff/prefabs.txt` at start, and logs each online player's region, cloak, curse and buffs when they change. For finding new IDs after a game update |
| `DumpPattern` | `Shroud\|Curse\|Wanderer\|Forest\|_VBlood$` | Which prefab names `Dump` writes |
| `DumpIntervalSeconds` | 3 | How often players are logged while `Dump` is on |

## Good to know
- To check it works, look in `BepInEx/LogOutput.log` for `Shroud of the Forest applied` when a player walks into the Cursed Forest.
- A game update can change the IDs the mod uses. If the log says `Prefab … should be …`, the mod has turned itself off and needs an update.
- To remove the mod, delete `NoCloakShroudBuff.dll` (and `NoCloakShroudBuff.cfg`) and restart.

## Credits
- Project setup (BepInEx packages and NuGet feeds) modelled on [ScarletBrazier](https://thunderstore.io/c/v-rising/p/ScarletMods/ScarletBrazier/) by markvaaz.
- The "server has finished loading" hook (a Harmony postfix on `SpawnTeamSystem_OnPersistenceLoad`) is a common pattern
  in V Rising mods, e.g. [KindredCommands](https://thunderstore.io/c/v-rising/p/odjit/KindredCommands/) by odjit, [ScarletCore](https://thunderstore.io/c/v-rising/p/ScarletMods/ScarletCore/) and [Bloodcraft](https://thunderstore.io/c/v-rising/p/zfolmt/Bloodcraft/) by zfolmt.
- Giving and removing a buff (`DebugEventsSystem.ApplyBuff`, `DestroyUtility` with `TryRemoveBuff`) follows the buff helpers
  in [KindredCommands](https://thunderstore.io/c/v-rising/p/odjit/KindredCommands/) by odjit.
- Built with [BepInExPack V Rising](https://thunderstore.io/c/v-rising/p/BepInEx/BepInExPack_V_Rising/) (BepInEx, HarmonyX,
  Il2CppInterop).

No code was copied from these mods; thanks to their authors for showing the way.

## Transparency
This mod was made with [Claude Code](https://claude.com/claude-code), Anthropic's AI coding assistant. Claude worked out
how the game handles the Curse of the Forest and the Shroud, and wrote the code and documentation; I decided what the mod
should do, reviewed the changes, and tested each version in game on my own server.

Made for V Rising 1.1.15 with BepInEx 6 (BepInExPack V Rising 1.733.2).
