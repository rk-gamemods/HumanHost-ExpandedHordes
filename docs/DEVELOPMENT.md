# Code overview

This standalone repository contains one plugin. Runtime source is under `src/`;
the console checks under `tests/` share the small calculation classes directly.
Settings in `ModSettings.cs` are authoritative. This is still a pre-testing alpha.

| Files | Purpose |
| --- | --- |
| `Plugin`, `ModIdentity`, `ModSettings` | Startup, identity and configuration. |
| `FeatureRuntime` | Separate Harmony owners, idempotent installation and feature failure handling. |
| `Population`, `PopulationOverrides`, `HordeRules` | Total/living limits, vanilla quantity scaling and guarded restoration of owned fields. |
| `CreatureCatalog`, `HordeSetup`, `SpecialRoster` | Creature identities, initialization and fresh-spawn selection. |
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
  spawns. Restored survivors keep their saved identities.
- `NPC_Horde_Mgr.StartHordeEvent` and `AI_Agen_Mgr.Broadcast_Sound_Played` drive
  the paired one-time lure using the game's existing sound response.
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

Profiling reports inclusive method time, not a complete frame breakdown. Timed
sections can overlap; asynchronous continuations are outside the synchronous
method timing. Frame mean/max cover all valid frames; p95 uses at most the first
8,192 frames in each window. Memory is managed heap, not total RAM or VRAM.
Missing population counts are -1. CPU/GPU timing depends on Unity exposing it,
and no per-mod GPU attribution is attempted. CSV errors disable file output while
log summaries continue. Debug/profiling default off and add no network telemetry.

No extra loot-preservation system, permanent ragdoll physics, attack/jump changes,
adaptive population, guaranteed boss quotas or custom event scheduler is included.
Use [BUILDING.md](BUILDING.md) and [TESTING.md](TESTING.md) before making release claims.
