# Play-test diagnostics

This implementation works toward [issue #1](https://github.com/rk-gamemods/HumanHost-ExpandedHordes/issues/1).
The code builds and its pure/native-metadata checks pass. Unity execution and
the issue's end-to-end performance acceptance remain unverified. Keep the issue
open until the runtime checklist and gaps below are resolved.
The [acceptance audit](ISSUE-1-ACCEPTANCE.md) also tracks unfinished requirements
that can be resolved before play-testing.

## Using it

Enable Debug Mode, Performance Profiling and Debug HUD, then restart. Leave
Detailed Method Timings off for ordinary tests. F8 toggles the overlay, F9 alternates
numbered start/stop markers, F10 captures another environment/overlap inventory,
and F11 explicitly opens the local report folder after its first write. Test
Marker Note supplies an optional note, read at key press and capped at 80 characters.
Rebind these keys if another mod uses them. HUD-only mode collects frames and
events without starting the writer or CPU/GPU/memory sampling. Debug-only mode
enables reports and memory sampling; profiling also polls Unity frame timings.

Reports are local in `diagnostics/` beside the DLL:

- `performance.csv`: actual bucket intervals and numeric event/population data.
- `diagnostics.log`: window summaries, cumulative horde observations, sampled
  method timings, data-loss counts, and explicit inventory rescans.
- `environment.txt`: session settings, versions, assembly MVID build identity,
  hardware capacities, graphics configuration and BepInEx plugin inventory.

Each CSV/log retains at most a current and previous file, rotating after a batch
at the Report File MiB threshold (default 5, configurable from 1 to 64). A file
can exceed that threshold by one batch. Each new session also rotates the current data/log files. Two
environment manifests are retained. Rotation can discard older history; session
IDs identify records, but a retained row's old manifest may have rotated away.
No report is uploaded. Share only after reviewing the files. The metadata
allowlist excludes account/device IDs, machine/user names, absolute paths and
third-party config/log contents. Metadata values containing paths, email-like
identifiers, local user/machine names or account-like digit sequences are omitted.
Control and directional-format characters are replaced. Values are capped at
256 characters; the complete inventory is capped during construction at 16,384
characters, including explicit truncation flags. Marker notes are user-supplied
content and must still be reviewed before sharing.

Inventory includes BepInEx plugins and sanitized `Chainloader.DependencyErrors`.
Harmony overlaps use exact installed owner IDs and original patch targets;
unresolved owners remain labeled as unresolved. Optional inventory failure does
not roll back installed gameplay features. An overlap does not prove a conflict.

Native registry investigation used Steam build 25448142. `Mod_Mgr.Categories`
describes category metadata. `WorkshopMgr` manages Steam queries and installation.
`ModBrowserBridge.RefreshInstalledIds` reads installation records;
`DetectManualInstalledMods` checks subscriptions, install locations and file
presence. None establishes which content is active in the running game. Native
and Workshop loaded-content coverage therefore remains explicitly unavailable.

## Ownership and failure behavior

`PerformanceMonitor` owns Unity access, the monotonic clock, cached configuration,
HUD refresh, slow metrics and teardown. `GameTelemetry` resolves typed field and
count delegates once. It reads private singleton fields directly, avoiding the
game's scene-searching singleton getters. Each missing accessor independently
returns unavailable; there is no scene-scan fallback.

`TelemetryCollector` owns numeric session/horde totals, buckets and histograms.
The host supplies milliseconds relative to session start from `Stopwatch`.
Events use the last rendered-frame timestamp for horde boundaries. Their timing
precision is therefore frame-level; event counts do not imply sub-frame timing.
Buckets close once per rendered frame when at least 100 ms has elapsed. A 327 ms
stall produces one 327 ms observation with two missed boundaries, not three
invented samples. The full frame belongs to the bucket in which it finishes,
including across explicit pause/scene/quit boundaries. Frame-time sums can
therefore differ from bucket duration at such boundaries. No catch-up loop runs.

`TelemetryWriter` owns file creation, formatting, rotation and flushing. The
main thread fills one of four preallocated buffers. A zero-wait `Monitor.TryEnter`
handoff exchanges it for a free buffer and queues the completed one. The worker
removes queued buffers and returns them only after writing. It never holds the
handoff gate during I/O. A full/contended queue drops the newest detail batch;
session and active-horde totals/histograms remain. One pending horde summary is
retained for retry on the next batch; replacing that pending snapshot is counted.
Neither queue nor numeric buffers grow with history.

Routine flushing uses buffered streams without a durable disk flush. Disk errors
stop the writer; a startup/error warning goes to BepInEx, while routine summaries
stay on the worker in the mod-owned log. No Unity logger/listener is called by
the writer. Shutdown attempts a final partial batch and waits at most 250 ms.
It reports queue loss or unfinished writes. A stalled worker keeps its buffers;
they are never reclaimed while it could still access them. Crashes can lose a
pending batch, queued data and OS-buffered writes.

## Metric contract

Game seams were inspected in Steam build **25448142**, local source revision
`75505302bbe546b6f783690e4d97230c537edda0`. No game source is distributed here.
Native-contract checks verify names and the two corpse dictionary mutation seams.
Runtime detour behavior still needs an actual game session.

| Metric | Source and meaning | Classification/reset |
| --- | --- | --- |
| Fresh/restored | Successful membership transition in `NPC_Horde_Mgr.Add_AliveHordeNPC`. Entry into `Spawn_Horde_NPC` is never counted. `Restore_Horde_NPCs` uses synchronous `async:false` spawns and brackets restoration. | Registered entities, not proof of completed rendering. Bucket deltas; session and horde totals. |
| Death | A membership removal inside `NPC_Spawner_Mgr.Back_Dead_NPC_To_Pool`. Duplicate removal of an absent entity contributes nothing. | Confirmed native death notification; no player kill attribution. |
| Other removal | Successful `Remove_AliveHordeNPC` membership removal outside the death notification. | Other/unknown reason, including live pool return. Never inferred from spawned minus alive. |
| Large/boss | `CreatureCatalog` identity classification on fresh registration. | Known catalog categories only; new native/mod bosses outside that catalog are not covered. |
| Living | `_aliveHordeNPCs`, concrete `Dictionary<GameObject,Horde_NPC_Info>.Count`. | O(1), sampled once per bucket. Window min/max aggregate those samples; not continuous extrema. |
| Corpse pool | `_ActiveDeadBodies.Count`; checked dictionary `Add` and successful `Remove` call sites in `Spawn_GPUI_Dead_Body` / `Put_Back_Body_To_Pool`. | Active settled pool, including restored bodies. Adds/removes are pool transitions, not new deaths, all world bodies, loot loss or active ragdolls. Removal reasons unavailable. |
| Placement/context | `GetValidSpawnPosition` sentinel and `Try_Get_Spawn_Context` result. | Calls and failed calls; not internal attempts. Later caller distance rejection is not included. |
| Horde/budget | Native save wave ID/emitted count, `_HordeZombieAll`, `_ZombiesPerWaveAdd`, native quantity factor and `_MaxAllowActiveZombies`. | Observed native values. Budget minus emitted can be derived; reload keeps emitted separate from fresh registrations. |
| Spawning | Presence of `_corHordeSpawn`. | Observed state, not proof of forward progress. Unknown is -1. |
| Region/game time | Region of saved `spawnStartRealPos` using verified terrain dimensions; `_G_Info._totalGameMinutes`. | Sampled spawn region, not current player biome; game minutes. |
| Reconciliation | First valid count establishes a baseline adjusted for events already seen. Later count minus baseline-plus-registrations-minus-removals. | Discrepancy exported; never rewritten as kills. Scene disposal or external mutation can explain differences. |
| Below target | Sum of intervals whose end sample has spawning active, remaining budget, and living below target. | Explicit sampled interval estimate, not exact occupancy duration. |
| Frames | Every rendered-frame monotonic delta; count/sum/max, thresholds >16.7/33.3/50 ms. | All valid frames; histogram 2,048 bins at 0.25 ms plus overflow. Upper-bin p95/p99, `out_of_range` above 512 ms. |
| CPU/GPU | Unity `FrameTimingManager`, polled at most 5 Hz; duplicate source timestamps rejected. | Delayed window averages and counts. Last raw source timestamp and read time exported; clock domains are not aligned. |
| Memory/GC | `GC.GetTotalMemory(false)` and generation 0/1/2 collection counts at 1 Hz. | Managed heap only; no forced collection. Hardware RAM/GPU fields are capacity, not utilization. |
| Method timings | About 1/32 calls per section with rotating phase and 14 timings per section per bucket (98 total). | Observed/selected/completed/cap-skipped calls, sampled inclusive elapsed/max. Cross-window completions are discarded and counted; they cannot mutate a writer-owned batch. No extrapolation or sum across nested sections. |
| Quality | Missing fields, rejected off-main-thread events, sample gaps, reconciliation, writer lag/failures/loss. | Numeric loss is disclosed; unsupported hooks do not become authoritative zeros. |

Hook paths use numeric bookkeeping and existing membership dictionaries. No
unbounded identity history is retained. Main-thread guards reject and count
unexpected off-thread callbacks instead of mutating main-thread counters. Source
paths use Unity coroutine/update/object operations; cross-mod thread behavior
must still be tested. A rejected callback makes event coverage incomplete.

Horde reports are **cumulative observations** keyed by session, native horde ID
and observation start. They snapshot at observed spawning stop and finalize on
horde change, scene unload or shutdown. A spawning stop is not labeled dawn,
budget exhaustion, all enemies dead, or a player victory. Survivors can continue
contributing to the same observation afterward. Do not sum repeated cumulative
snapshots. Partial windows spanning horde transitions can mix states; raw bucket
state is sampled at its end. Horde histograms track frames throughout the
observation. Worst completed window FPS uses the horde's own nominal 15-second
frame intervals, independent of partial file publications. Full spanning frames
are retained; a short observation with no full interval reports unavailable.
Pause emits a partial cumulative snapshot. A bucket spanning observation changes
counts as lost for each observation it overlaps if its batch is rejected; these
per-observation losses are not additive session totals. Same-timestamp events can
produce a zero-duration bucket, while empty explicit boundaries carry only their
reason and pending summary/metadata. Neither invents frames or elapsed time.
Timing finalizers that finish after a publication do not contribute duration to
either window. Selected and completed counts expose that incomplete coverage.

## CSV schema 1

Column names and order are defined by `TelemetryFileSink.Header`.

- `schema`, `session`, `batch`, `seq` identify the schema, session, attempted batch
  and real bucket. Gaps in sequence numbers accompany lost detail.
- `start_ms`, `duration_ms`, `frame_sum_ms`, `frame_max_ms` use milliseconds.
  `frames`, `gt16_7`, `gt33_3`, `gt50`, `missed` are counts. Divide frame sum by
  frame count for mean; combined FPS is `1000 * sum(frames) / sum(frame_sum_ms)`.
- `horde`, `spawning`, `budget`, `emitted`, `alive_sample`, `corpse_pool_sample`,
  `living_target`, `corpse_limit`, `game_minutes`, `spawn_region` are end samples.
  `spawning` is 1/0/-1. Other unavailable numeric fields are -1.
- `fresh`, `restored`, `deaths`, `other_removed`, `corpse_added`, `corpse_removed`,
  `placement`, `placement_failed`, `context`, `context_failed`, `large`, `boss`
  are bucket event deltas with the scopes in the table above.
- `marker` is the latest numbered marker pressed during the bucket (0 if none).
  The log retains up to 16 timestamped marker notes per batch; extra notes are
  counted as lost. Odd marker IDs start a test phase; even IDs stop it.
  `dropped_batches` and `dropped_buckets` are cumulative and
  known at publication; final unsaved data is also reported in BepInEx warnings.

The log exports cumulative `lost_marker_notes_session` (overflow and rejected
batches), `lost_metadata_snapshots_session` (overwritten or rejected inventories),
and `lost_horde_summaries` (overwritten pending snapshots). Inventory records carry
their capture time and publication batch. Each batch labels actual HUD/detail
state, configured debug/profiling state and hook/engine-timing availability.
Empty aggregate frame distributions are `unavailable`; a zero raw frame count or
sum is still a valid count. Remaining budget is `max(0, budget - emitted)` only
when both native values are known. Horde reports also include game time, region,
context failures and sampled time below target.

Histogram percentiles live in window/horde summaries, never individual buckets.
They must not be averaged. The histogram includes long frames in count/sum/max
even when their percentile falls into overflow.

## Measured validation and remaining gates

Run without Unity:

```powershell
dotnet run --project tests/ExpandedHordes.Checks -c Release
```

Add the installed Managed directory for native-contract checks, and build with
your game path as described in [BUILDING.md](BUILDING.md). Tests exercise regular
buckets, boundary/stall/partial-stop behavior, histogram merge/overflow/reset,
separate event counters, horde snapshots, reconciliation, sampler fairness and
phase coverage, queue saturation, I/O failure, bounded shutdown and real CSV
rotation/readback under a non-English culture. Production host mode tests use
test doubles at the Unity/game/configuration/Harmony-installation boundary while
executing the actual host, collector and file writer. They verify forbidden
component work, optional-failure isolation and cached HUD asset/layout reuse;
they do not execute or benchmark native detours or Unity GUI rendering.

On 2026-09-22, an AMD Ryzen 7 9800X3D Windows host, .NET 8 check target built with
SDK 10.0.401, passed 471 assertions and a zero-warning plugin build against
Unity 2022.3 / BepInEx 5.4.23.5. Representative synthetic results:

| Check | Observed result |
| --- | --- |
| Warmed event/frame/bucket/batch-reset loop | 0 allocated bytes on the calling host thread. |
| 1,000 and 10,000 event callbacks/s, 2,000 simulated seconds each | Fixed memory; population inputs 1,000 living and 5,000 corpses. No game entities created. |
| Pure `CloseBucket` timing, 10,000 retained samples | Median below 0.1 us clock resolution, p95/p99 0.1/0.1 us; observed maxima varied between runs. This excludes game adapters, histogram reset and handoff. |
| Numeric collector, four batches and sampler | 347,864 allocated bytes; bucket layout 264 bytes on host. Excludes metadata, streams, thread/runtime and UI assets. |
| One warmed synthetic 150-bucket serialization plus buffered flush | About 0.244 ms and 152,304 allocated bytes. The worker is not allocation-free. Filesystem timing is not a worst-case bound. |
| Same synthetic batch CSV + summary | About 20 KB per batch, roughly 4.9 MB/hour at 240 batches/hour. Manifests/horde snapshots are extra; final workload values change row sizes. |
| Warmed HUD text formatting, 100 refreshes | 3,768 allocated bytes/refresh; mean 3.009 us/refresh on host .NET. Excludes Unity content/style/layout/draw, game adapters and engine effects. HUD mode is not allocation-free. |
| Production handoff, 1,000 attempts | Median/p95/p99/max 1.2/1.8/3.6/42.7 us; 999 accepted and one immediate rejection. Forced contention rejected in 0.3 us. Includes producer scheduling noise, excludes Unity dispatch; the observed maximum exceeded the issue's assumed 20 us allowance, while p95 did not. |
| Collector/writer host allocation envelope | 978,344 bytes: 559,960 producer construction/full-buffer bytes plus 418,384 first real writer-batch bytes. Includes four 16,384-character metadata snapshots, manifest, 64 marker notes, concurrent metadata builder, streams, encoding and first-batch formatting. Excludes Unity adapters/UI assets and runtime-specific dispatch. This conservative allocation fixture is below 1 MiB; it is not whole-process RAM. |
| Production event entry, thread guard and collector | Mean 0.008 us/call over 10,000 warmed calls, zero allocated bytes. Excludes Harmony dispatch, native registration and Unity Mono. |
| Extreme numeric-width/Unicode batch | 306,535 CSV bytes and 62,059 log bytes. The fixture checks below 1 MiB CSV and 256 KiB log per batch; this is a retention stress case, not an expected hourly rate. |

The [captured host output](diagnostics-host-checks-2026-09-22.txt) records that run;
the check program prints fresh values. These observations do **not** establish
the issue's 20 us Unity bucket target, 0.5% frame-time budget, a safe entity limit
or absence of 1 ms hitches. No old-profiler Unity baseline or matched A/B runs
were captured. Full incremental Harmony dispatch, skipped-hook cost, UI refresh
and draw, engine timing capture, slow metrics and whole-process
GC/GPU effects still require several matched game runs. Source/native-contract
checks do not execute detours, prove exception/pooling semantics under other mods,
or measure Mono allocations. Metadata/stream/UI working memory also needs a
target-runtime measurement against the full 1 MiB buffer goal.

Remaining coverage gaps: authoritative native/Workshop loaded-content registry;
classification of uncatalogued bosses and removal reasons. These are
not silently inferred. Review [TESTING.md](TESTING.md) before release.

API references: [Unity 2022.3 frame timing](https://docs.unity3d.com/2022.3/Documentation/Manual/frame-timing-manager.html),
[BepInEx 5 plugin inventory](https://docs.bepinex.dev/v5.4.16/api/BepInEx.Bootstrap.Chainloader.html),
[Harmony typed accessors](https://harmony.pardeike.net/v2/api/HarmonyLib.AccessTools.html).
