# Code overview

This standalone repository contains one plugin. Runtime source is under `src/`;
the console checks under `tests/` share the small calculation classes directly.
Settings in `ModSettings.cs` are authoritative. This is an alpha with limited
local play-testing.

`SettingsOrder` preserves section/key identities, values and metadata while
ordering the settings for display. `SettingsPersistence` applies it only to
this plugin's `ConfigFile` after `Save`, under BepInEx's `_ioLock`, using an
atomic file replacement. BepInEx sorts sections alphabetically;
[HHMM 1.3.0 renders file order](https://github.com/xem888/HHMM/blob/cd5bfe8d7700fec5f87d26051544d19cacee6019/src/pages/ConfigEditor.tsx#L798).
Unknown entries remain intact, and malformed files are left unchanged.

| Files | Purpose |
| --- | --- |
| `Plugin`, `ModIdentity`, `ModSettings` | Startup, identity and configuration. |
| `FeatureRuntime` | Separate Harmony owners, idempotent installation and feature failure handling. |
| `Population`, `PopulationOverrides`, `HordeRules` | Total/living limits, vanilla quantity scaling and guarded restoration of owned fields. |
| `CreatureCatalog`, `HordeSetup`, `SpecialRoster`, `HordeSpecialLimits` | Creature identities, initialization, fresh-spawn selection and living special counts. |
| `HordeRunSpeed`, `HordeAttraction` | Temporary running boost and one-time lure. |
| `DamageResistance`, `CombatRules` | Health-loss reduction and category/corpse rules. |
| `CorpseRetention` | Narrow adjustment of the native corpse-limit read. |
| `PlacementLog`, `PerformanceMonitor`, `PerformanceRules` | Optional logs, selected method timings and bounded aggregates. |

## Native patch boundaries

- `NPC_Horde_Mgr._Start` resolves the catalog before native survivor restoration.
- `NPC_Horde_Mgr.Get_Plan_To_Spawn_Count`, `AI_Agen_Mgr.MyStart` and
  `SetHorde_MaxAllowActiveZombies` implement population settings while retaining
  native scheduling and spawning.
- `NPC_Horde_Mgr.Spawn_Horde_NPC` changes selection arguments for fresh async
  spawns. Restored survivors keep their saved identities. Living special-type
  limits suppress only this mod's extra selection when the corresponding count
  is at its limit. The native selection remains intact, with no culling or hard
  cap on vanilla types. Counts include vanilla members and restored survivors;
  the normal configured chance resumes below the limit. Zero means unlimited.
  Chance settings are floating-point percentages from 0 through 40; whole-number
  configuration values retain their meaning. `HordeRules.Category` uses a
  0-9,999 roll for 0.01 percentage-point resolution.
- `HordeSpecialLimits.TryCount` reads `_aliveHordeNPCs` and native
  `NPC_Input.is_Boss` before an extra selection. It includes native choices and
  restored survivors without depending on diagnostic hooks. Unavailable or
  incomplete classification suppresses the limited extra selection and logs a
  warning; native selection continues. `_inAsyncSpawnNPC` and a scoped guard
  prevent overlapping extra selections during native spawn construction.
- `NPC_Horde_Mgr.StartHordeEvent` and `AI_Agen_Mgr.Broadcast_Sound_Played` drive
  the paired one-time lure using the game's existing sound response. Both normal
  and elevated attempts run automatically; the elevated offset is fixed at 100 m.
  `Attraction Range` is the only attraction setting. Migration preserves the old
  `Hearing Radius` value unless `Attraction Range` already exists, and removes the
  obsolete `Elevated Sound Height` entry.
- The debug shortcut calls `NPC_Horde_Mgr.Try_Get_Spawn_Context` before requesting
  `StartHordeEvent(false)`. During daytime it calls
  `Enviro.EnviroTimeModule.SetTimeOfDay(19f)` and waits for
  `Weather_Controller.Update` to propagate nighttime through
  `Creature_Mgr.On_Time_Passing`. Existing nighttime is preserved.
- Death diagnostics scope `NPC_Spawner_Mgr.Back_Dead_NPC_To_Pool` to the dying
  entity, then classify its saved membership before `Remove_AliveHordeNPC`
  removes it. Confirmed removal records one category and one aggregate death.
- `C_Controller_Base.Play_Anim_BaseLayer` adjusts only the running movement
  argument while native nighttime horde spawning remains active.
- `Char_Status.set__CurrHP` reduces requested HP loss before the game's clamp.
  Native `spawned_Horde_NPCs` membership excludes initialization and pooled
  resets; no maximum-HP edit or new save format is added.
- `GPUI_Dead_Body_Mgr.Spawn_GPUI_Dead_Body` has one checked `_MaxCorpseCount`
  read. The transpiler adds max(native, configured), rejecting an unexpected
  instruction layout rather than guessing after an update.
- Optional diagnostics observe `GetValidSpawnPosition`, `Try_Get_Spawn_Context`,
  `Save_Horde_Data_To_Disk`, `Zombie_Agent._Update` and corpse creation.

Native method names and creature identities were checked against Steam build
25448142. Recheck them after game updates. The repository deliberately excludes
decompiled game sources and game assemblies; build against your own installation.

## Adding a creature type

Verify its registered group GUID and prefab index, then add an appropriate
`CreatureCatalog.Definition`. Selection and resistance share this catalog.
Do not reorder native saved arrays. The game's boss flag takes precedence;
unlisted non-bosses receive regular resistance. There is no health/name guessing
or external mod-registration framework. Late registry changes by another mod
need separate investigation and testing.

## Failure handling and profiling limits

Feature installation owns only its own patches. Detected failures disable the
affected feature and log an error; this cannot recover from every possible game
or other-mod failure. Debug hook overlap is information, not proof of conflict.
Population cleanup restores fields only when they still match this mod's last write.

Diagnostics use one main-thread collector, four reusable batch buffers and a
single background writer. A fixed histogram covers every valid frame; p95/p99
are 0.25 ms upper-bin approximations through 512 ms, with explicit overflow.
Detailed method timing is separately enabled, sampled, inclusive and limited
to synchronous execution. It is not a complete frame breakdown. Light mode
does not install diagnostic per-zombie update patches.

Memory means managed heap. Missing numeric data is -1; CPU/GPU samples are
delayed and are not aligned to the current population bucket. A writer failure
stops file output and produces a BepInEx warning; HUD/session counters continue.
Shutdown waits at most 250 ms and warns about unsaved data. See
[DIAGNOSTICS.md](DIAGNOSTICS.md) for ownership, native seams, schema and evidence.

No extra loot-preservation system, permanent ragdoll physics, attack/jump changes,
adaptive population, guaranteed boss quotas or custom event scheduler is included.
Use [BUILDING.md](BUILDING.md) and [TESTING.md](TESTING.md) before making release claims.
