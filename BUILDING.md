# Building NoCloakShroudBuff

Requires the .NET SDK 6 or later. NuGet feeds are in `nuget.config`.

## Game interop assemblies
The plugin compiles against the interop assemblies BepInEx generates for a game build. Either use the
`VRising.Unhollowed.Client` NuGet package (automatic, but may lag the game), or copy a dedicated server's
`BepInEx/interop/` folder into this repo as `interop/` (it is gitignored). After a game update, copy it again and rebuild.

## Commands
From the repo root:

| Command | Builds against |
|---|---|
| `dotnet build -c Release` | the NuGet package |
| `dotnet build -c Release -p:InteropDir=interop -o bin/server --no-incremental` | `interop/` |

## Thunderstore package
`python -I thunderstore/pack.py` builds the DLL (against `interop/` by default) and writes
`dist/NoCloakShroudBuff-<version>.zip` with `manifest.json`, `README.md`, `CHANGELOG.md`, `icon.png` and the DLL, all at the
zip root. The version comes from `NoCloakShroudBuff.csproj`; the description and dependency are in `pack.py`. The icon is
drawn by `thunderstore/make_icon.py`.

## How it works
- Once the server has loaded (`SpawnTeamSystem_OnPersistenceLoad`), it checks the three prefabs it uses by name, then checks
  online players every `CheckIntervalSeconds`.
- For a player **not** wearing `Item_Cloak_Main_ShroudOfTheForest` (1063517722):
  - region from `CurrentWorldRegion` on the **user** entity is `WorldRegionType.CursedForest`, and
  - `CHAR_Villager_CursedWanderer_VBlood` (109969450) is in the `UnlockedVBlood` buffer of the user's progression entity
    (`ProgressionMapper`)
  → apply `EquipBuff_ShroudOfTheForest` (1504279833) with `DebugEventsSystem.ApplyBuff` if it is missing; otherwise remove it
  if present (`DestroyUtility.Destroy`). Without the cloak, any Shroud buff the player has must be the mod's, so nothing is
  tracked or saved.
- The curse (`Buff_General_CurseOfTheForest_Area`) checks for the Shroud buff while it runs: with it, the debuff stays at 1 stack.
- Tested in game on 1.1.15: the buff stays on without the cloak, equipping/unequipping the cloak never gives two copies.
