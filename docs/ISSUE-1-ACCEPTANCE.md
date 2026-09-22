# Issue 1 acceptance audit

This is an implementation audit of [issue #1](https://github.com/rk-gamemods/HumanHost-ExpandedHordes/issues/1),
not a replacement specification. The issue remains authoritative. The requested
delivery boundary is completion short of actual play-testing. That boundary
permits deferring Unity observations; it does not permit deferring code,
automated checks, report contracts, or resource bounds that can be verified here.

Status on 2026-09-22: **complete at the requested pre-play-test boundary**.
The final host suite passes 471 assertions and the plugin builds with zero
warnings/errors. This does not certify runtime safety, performance or compatibility.
The initial draft commit was `530a827`; follow-up checks exposed defects that its
334 assertions did not cover. Read this table alongside [DIAGNOSTICS.md](DIAGNOSTICS.md)
and [TESTING.md](TESTING.md). The evidence, rather than the assertion count, defines
what has been verified.

IDs follow the order of the checkboxes in issue section 9. `Host proof` means
the stated non-Unity boundary has automated evidence. Source and installed-assembly
checks complement it where a native runtime is required. `Play-test` is deliberately
deferred, not passed. `HostModeChecks` executes the production host/collector/writer
with test doubles only for Unity, game, configuration and Harmony installation.
It proves orchestration and isolation, not the behavior or cost of those doubles
in the actual game.

| ID | Requirement | Current evidence and remaining work |
| --- | --- | --- |
| C1 | Shared collector and metric contracts | One production collector supplies HUD and writer. DIAGNOSTICS defines sources, scopes, resets and unavailable values. HUD/report readback verifies empty distributions, effective state, categories, context failures, mode and availability flags. |
| C2 | Registration, restoration, death, other removal, reuse, duplicates | Host proof: `TestAccessorsAndTransitions` exercises the production membership-transition policy against a real dictionary, including failed attempts and pooled identity reuse. Native metadata checks and documented source seams establish the adapter targets. Play-test: actual Harmony/native exception and pooling behavior. |
| C3 | Fake-clock boundaries, stalls, pause/load/quit, resets | `BoundaryChecks` covers pause/resume, reload of the same horde ID, zero-duration shutdown with events, empty repeated boundaries and spanning frames. Existing fixtures cover 327 ms and multi-second stalls. Production host boundary output is also checked. |
| C4 | Event/window/horde reconciliation | Host proof: distinct totals, dropped-batch preservation, explicit discrepancies, partial-boundary sums and loss attributed to each observation's overlapping buckets. Pending final summaries acquire loss incurred after their snapshot. |
| C5 | Histogram distribution, bounds, overflow, merge and weights | Host proof: empty/invalid input, bin bounds, known distributions, overflow, long frames, merge/reset, unequal window sizes and horde frame totals. Combined FPS uses total frames/total time. |
| C6 | Independent optional accessors/timing failures | Compiled accessor tests cover missing/wrong types, null/empty/replaced collections and zero allocations. Production host tests inject inventory, engine-timing and hook-installation failures; independent components continue. Source resolves each game field separately without scans. Native execution remains play-test. |
| C7 | Sampling fractions/cap/fairness/aliasing/nesting/exceptions/disabled | Host proof: exact uncapped 1/32 fraction, rotating phases, 98-total cap and section fairness, nested/finalizer bookkeeping, cross-window ownership, disabled production entry path and report counts. Actual Harmony dispatch/finalizer execution remains play-test. |
| C8 | Installed-member contracts and Unity-free checks | Pure mode runs without game files. Installed checks cover patch signatures, singleton/static and compiled-field types, save fields, terrain getter, BepInEx inventory properties and corpse mutation IL seams. The plugin build verifies directly referenced game/Unity APIs. |
| R1 | Zero-allocation hot path and separate handoff cost | Warmed numeric/event/frame/bucket/reset, typed reads, timing tokens and production event entry/thread guard allocate zero bytes on host .NET. Handoff and forced contention are measured separately. Game hooks use numeric state and existing dictionaries; Unity Mono and detour costs remain play-test. |
| R2 | Disabled, HUD-only, debug-only component isolation | Production host tests cover all-off, HUD-only, debug-only, light profiling and combined detailed modes. They assert forbidden writer/inventory/timing/hook/draw work and zero warmed disabled-path allocations. Actual installed detours remain play-test. |
| R3 | Bucket timing distribution and 20 us target | Host measurements report median/p95/p99/max for pure bucket closure. Play-test: target-runtime complete bucket maintenance. Host measurements exclude adapters, reset and handoff and cannot certify that target. |
| R4 | Full incremental costs against allowances | Host event entry/thread guard, bucket closure, handoff/contention, HUD text and worker formatting are measured with domains labeled. Full hook/frame/UI/engine/slow-metric costs, old-profiler baseline and allowance/model comparison require matched game runs and remain play-test. |
| R5 | Reused HUD assets, refresh allocations, draw cost | Host proof: production HUD formatter tests distinguish missing values from known zeros, retain cached timing/memory across batch reset, and label window duration/age. Text formatting bytes/refresh are measured. Source: cached content/style and 4 Hz refresh. Play-test: Unity draw allocations and rendering cost. |
| R6 | Warmed workload, bursts, duration and fixed memory | Host proof: 1,000/10,000 callbacks/s, bursts, configured population inputs, 2,000 simulated seconds, saturated fixed queue, full metadata slots, repeated session/rotation operations and bounded builders. No history-sized entity collection exists. |
| R7 | Bounded writer, stalls/failures/loss, allocation and contention | Host proof: blocked/throwing sinks, real unusable path, fixed ownership, drop-newest, bounded/repeated stop, real formatter allocation and production-gate contention. Failed writes retain unsaved accounting; final loss is warned when possible. Native filesystem/Unity interaction remains play-test. |
| R8 | 15-second batches, partial shutdown, parseable rotation | Host proof: actual formatter/readback, fr-FR CSV, same-timestamp events, multi-session pairing, configurable rotation and cumulative marker/metadata losses. Empty boundaries retain reasons without invented frames. Crash-loss limitations are documented. |
| R9 | Complete working-buffer and final output bounds | Host allocation fixture is 978,344 bytes, including full metadata slots, manifest, builder, streams/encoding and first real formatting. Final representative output is 20,487 bytes/batch; extreme-width output is tested separately. Six retained files are verified. This proves the documented host fixture, not target-runtime heap usage. |
| H1 | Complete readable HUD and safe controls | HUD covers requested state/counts, remaining budget, failures/categories, distributions, delayed timings, memory, loss and disabled features. Host tests verify formatting, configurable input path, cached content/style/layout reuse and measured-height sizing. Actual legibility, placement and key conflicts remain play-test. |
| H2 | Manifest, CSV, horde/window summaries and markers | `ReportChecks`, retention and production host tests read actual files: manifest/session pairing, marker actions/times, inventory batch/time, effective horde state, shortfall duration, failures, categories, distributions, detail completion and mode/availability flags. |
| H3 | Post-load/rescan inventory, coverage, overlaps and errors | Source uses initial, five-second and explicit captures, installed original targets, exact own-owner filtering and unresolved mappings. BepInEx dependency errors are sanitized. Bounded builders and capture-failure isolation are tested. Inspected native installation/subscription surfaces cannot establish loaded content; DIAGNOSTICS names that evidence and coverage gap. Actual post-load overlaps remain play-test. |
| H4 | Shareable privacy and no upload | Adversarial fixtures cover paths, email/account-like IDs, local user/machine strings, controls, bidi text, oversized values and exception-message exclusion. Output audit found only the allowlist, numeric state and explicit user marker notes. No network client/upload was added. Notes still require user review. |
| H5 | Repeated matched A/B scenarios | Play-test. TESTING lists scenarios; no actual A/B evidence exists. |
| H6 | Whole-process/GPU/frame/allocation/spike/coverage report | Play-test for actual game measurements. Output preserves interval/frame sums, thresholds, distributions, coverage/loss, delayed timing counts, heap/GC and modes. TESTING requires external whole-process/GPU evidence, matched populations and raw measurements with variation. |
| H7 | Evaluate 0.5% mean frame increase and repeatable 1 ms hitches | Play-test. No target pass or safe entity ceiling is claimed. |
| H8 | Actual measurements, unsupported counters, gaps | DIAGNOSTICS records final host measurements and raw output, allocation domains, source/build identity, unsupported counters and deferred game measurements. No safe entity ceiling, compatibility certification or 0.5% target pass is claimed. |

## Confirmed follow-up repairs

- A test with two partial publications during a 15-second horde interval failed:
  the horde's worst-window FPS remained unavailable. Horde frame windows now
  advance independently of file publications and include a completed final
  interval before the end snapshot. A short following horde cannot inherit it.
- A timing finalizer could finish after its batch was published. Timing samples
  now carry owner and generation, reject cross-window completions, and export
  completed counts separately from selected samples. This prevents mutation of
  a writer-owned batch and false elapsed time in a replacement batch.
- A compiled count read on an uninitialized dictionary threw instead of returning
  unavailable. The production accessor now handles null, missing/wrong collection
  types, disposal and replacement without enumerating or allocating per read.
- Final pause/scene/shutdown snapshots close their partial bucket before ending
  the horde. Optional inventory failures produce a sanitized failure record
  instead of stopping the numeric collector.
- Repeating successful writer shutdown raised `ObjectDisposedException` on the
  wake handle. Shutdown now returns its completed result on repeated calls;
  tests cover successful, failed and initially timed-out workers.
- HUD startup no longer displays an unobserved population or empty distribution
  as zero. Its pure formatter labels unavailable hooks and completed-window
  age, and preserves delayed timing/memory values across buffer changes.
- Same-timestamp shutdown now preserves recorded events and pending summaries.
  Loss accounting distinguishes observation epochs across horde changes/reloads
  and discloses discarded marker notes and overwritten/dropped inventories.
- Metadata is bounded during construction, values are sanitized, loader errors
  are included, and exact Harmony ownership replaces prefix matching. Optional
  inventory failures cannot roll back installed gameplay patches.
- Retention is configurable and tested across sessions and repeated rotations.
  Host mode tests verify isolation, optional failures, cached HUD layout and
  production boundary output. Empty distributions/rates are unavailable.

The automated lifecycle fixtures prove membership-decision behavior, not that
Unity executes every hook on the expected thread. Likewise, the timing-token
exception fixture proves completion bookkeeping, not Harmony finalizer execution.
Those are separate proof boundaries and must remain explicit.
