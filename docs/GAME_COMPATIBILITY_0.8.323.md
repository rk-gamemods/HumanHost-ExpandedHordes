# Human Host 0.8.323 compatibility review

Expanded Hordes 0.2.4 needs no runtime-code change for Human Host 0.8.323,
Steam build 25986463, based on the source, serialized-catalog and installed-assembly
review completed on 2026-10-16. In-game compatibility remains unverified for this
update. The plugin version stays 0.2.4; this review does not publish a release.

The comparison starts at Human Host 0.8.319, Steam build 25752290. It covers
all 25 changed C# files, including removed assemblies, and their connections to
the mod's patch targets, reflection fields, settings and creature identities.
Generated Unity script-registration tables were reviewed as metadata.

## Patch and catalog findings

- `NPC_Horde_Mgr.Spawn_Horde_NPC` adds addressable-handle validity checks and
  guards the missing `Init.ins` cleanup path. Its parameter names, fresh-spawn
  selection boundary, horde identity registration and survivor initialization
  remain compatible with `HordeComposition` and `HordeSpecialLimits`.
- Complete decompiled member comparisons found 27 of 28 selected patch targets
  and relevant callers unchanged. The remaining member is `Spawn_Horde_NPC`.
  Unchanged members include horde scheduling, population calculation, arrival
  iteration and placement, membership and save/restore, AI sound broadcasting,
  refocus broadcasting, the HP setter, animation-speed dispatch, corpse creation
  and return, death/recycling, Admin Panel movement targets and day/night updates.
- The five group GUIDs and prefab indexes in `CreatureCatalog.Definitions` still
  resolve to the same group objects. Their serialized `TerraTop_NPC_Set` fields,
  including the prefab arrays, are unchanged. Addressables entry numbers and
  dependency hashes changed; the selected asset identities did not.
- All 11 serialized NPC biome entries keep their group membership, weights and
  resolved biome names. Their `bioType` object pointers changed. The native
  terrain width remains 1024 and `_BiomesWidthNum` remains 2, preserving the
  region calculation used by `SpecialRoster`. The inspected native horde budget,
  pioneer/growth defaults and AI allowance are unchanged.
- `NPC_Spawner_Mgr.Delay_Back_Dead_NPC_To_Pool` still yields before breaking out
  of its overflow branch. The retained death/recycling guard requires no change.

## Other changed game code

| Assemblies | Reviewed changes and mod impact |
| --- | --- |
| `Player`, `Map_System_POI`, removed `CompassPro` | Native compass/POI replacement, map photos, cache, fog and map interaction. The mod references none of these map APIs. Map rendering and terrain loading can still affect observed performance. |
| `Terrain`, `Digger.Runtime` | Spawn-load validity guards, terrain/dig recovery, map capture and player-centered terrain loading. The horde arrival iterator and placement checks are unchanged. |
| `Build_System`, `Car` | Partial building-load preservation, vehicle mass/rotor handling, deferred destruction, tree HP and vehicle damage. Vehicle impacts still reach the native HP setter used by resistance; check their changed damage during play. |
| `Creature`, `Camera` | NPC yaw/render smoothing, player aiming/boarding, skills and buffs. The mod's animation-speed hook, health boundary and recycling methods are unchanged. |
| `Trap` | Per-trap, per-character hit cooldowns replace the shared character timestamp. Multiple traps can change damage frequency; horde resistance still uses the common HP setter. |
| `G_Save`, `SaveData`, `Merchant` | World seed becomes `long` with an integer conversion helper; skill saves clone their data; POI references migrate. The mod does not read the world seed or own these save records. |
| `Hand_Tools`, `Equipment`, `Sound_FX` | Hand/armor projectile exclusions, terrain-tree HP ownership and footstep fallback. No patched signature changes. |
| `Global_Funcs`, `Use_F`, `SteamManager` | Mod-error attribution, isolated update callbacks, loot-window guards, Workshop transfer progress and replacement-source checks. Existing plugin identity and diagnostics need no adaptation. |
| `Language`, `UI`, `Optimize`, `Weather`, `com.thenakeddev.fsr.Runtime.HDRP` | POI type migration, text sizing, map-capture render-state restoration and camera-specific upscaling. The weather update used by the debug horde trigger is unchanged. |
| Removed `SingularityGroup.HotReload.Runtime.Public` | Development hot-reload API removed. The mod has no reference to it. |

## Validation

The checks ran against the installed game assemblies and BepInEx on 2026-10-16.
Build commands supplied `HumanHostDir` explicitly as described in
[BUILDING.md](BUILDING.md).

| Check | Result |
| --- | --- |
| Release plugin build | 0 warnings, 0 errors |
| Pure policy checks | 593 assertions passed |
| Policy plus installed-assembly contracts | 739 assertions passed, including the pure checks above |
| BepInEx config and ModMenu integration | 50 assertions passed |
| Admin Panel compatibility | 17 assertions passed with real Harmony dispatch and substituted game boundaries |
| Death/recycling checks | 48 assertions passed |
| Recycling negative control, `--without-guard` | Failed as expected with 8 disabled recycled objects in the 12-death fixture |

The installed contracts include arrival iterator/placement shape, the single
corpse-limit field read, collection mutations, target signatures and reflection
field types. No game assemblies were copied into the plugin build output.

These checks do not establish Unity callback order, actual game Harmony detours,
runtime catalog resolution, visual behavior, performance or compatibility with
every other mod. No deployment or game launch was performed. Follow
[TESTING.md](TESTING.md), especially movement/attraction during camera and vehicle
transitions, trap and vehicle damage with resistance enabled, map open/close
during a horde, terrain-loading transitions, and save/reload with survivors.
Confirm the plugin load line and inspect new errors or disabled-feature messages
before describing this build as play-tested.
