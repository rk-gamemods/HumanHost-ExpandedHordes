# Issue 1 acceptance audit

This is an implementation audit of [issue #1](https://github.com/rk-gamemods/HumanHost-ExpandedHordes/issues/1),
not a replacement specification. The issue remains authoritative. The requested
delivery boundary is completion short of actual play-testing. That boundary
permits deferring Unity observations; it does not permit deferring code,
automated checks, report contracts, or resource bounds that can be verified here.

Status on 2026-09-22: **not yet complete before play-testing**. The initial draft
commit was `530a827`. Follow-up checks below exposed defects that its 334 passing
assertions did not cover. Read this table alongside [DIAGNOSTICS.md](DIAGNOSTICS.md)
and [TESTING.md](TESTING.md). A passing assertion count does not certify the table.

IDs follow the order of the checkboxes in issue section 9. `Host proof` means
the stated non-Unity boundary has automated evidence. `Open` includes missing
or indirect evidence. `Play-test` is deliberately deferred, not passed.

| ID | Requirement | Current evidence and remaining work |
| --- | --- | --- |
| C1 | Shared collector and metric contracts | Host structure: `PerformanceMonitor` supplies `TelemetryCollector` to HUD and writer; DIAGNOSTICS defines sources and scopes. Open: reconcile every unavailable/disabled field across the three output views. |
| C2 | Registration, restoration, death, other removal, reuse, duplicates | Host proof: `TestAccessorsAndTransitions` exercises the production membership-transition policy against a real dictionary, including failed attempts and pooled identity reuse. Native metadata checks and documented source seams establish the adapter targets. Play-test: actual Harmony/native exception and pooling behavior. |
| C3 | Fake-clock boundaries, stalls, pause/load/quit, resets | Host proof: 327 ms and multi-second stalls, partial stop and reset cases. Open: explicit pause/resume/load host sequences and same-timestamp publications; source-level callback order alone is insufficient. |
| C4 | Event/window/horde reconciliation | Host proof: distinct totals, dropped-batch lifetime preservation and explicit discrepancies. Open: audit horde loss attribution and every partial-boundary total. |
| C5 | Histogram distribution, bounds, overflow, merge and weights | Host proof: bin boundaries, overflow, long frames, merge/reset and horde frame totals. Open: expand known-distribution, empty/invalid input and weighted-FPS cases beyond the current small fixtures. |
| C6 | Independent optional accessors/timing failures | Host proof: production compiled dictionary accessor rejects missing/wrong types, distinguishes null from empty, and follows replacement collections. Inventory exceptions are contained separately. Open: integration proof for independent hook/accessor/optional timing failures. |
| C7 | Sampling fractions/cap/fairness/aliasing/nesting/exceptions/disabled | Host proof: rotating phases and per-section quotas; timing-token tests cover nested inclusive durations, exception-path completion, inactive samples and cross-window ownership. Open: complete-mode/Harmony dispatch checks and measured fractions in output. |
| C8 | Installed-member contracts and Unity-free checks | Host proof: the checks run with or without a Managed-directory argument; actual assembly metadata checks cover game seams. Open: audit the inventory against every new required member rather than assuming the existing list is exhaustive. |
| R1 | Zero-allocation hot path and separate handoff cost | Host proof: warmed numeric event/frame/bucket/reset loop, typed count reads, and timing-token capture/completion allocate zero bytes on .NET. Open: adapter/hook work audit and separately measured handoff contention. Play-test: Unity Mono allocation and dispatch costs. |
| R2 | Disabled, HUD-only, debug-only component isolation | Source evidence: initial disabled return and conditional writer/timing installation. Open: automated mode-level negative checks; source inspection alone is too weak. |
| R3 | Bucket timing distribution and 20 us target | Host measurements report median/p95/p99/max for pure bucket closure. Play-test: target-runtime complete bucket maintenance. Host measurements exclude adapters, reset and handoff and cannot certify that target. |
| R4 | Full incremental costs against allowances | Open: host-testable dispatch/handoff/formatting costs and baseline comparison. Play-test: full Unity event hooks, frame path, HUD, engine timing and slow metrics. |
| R5 | Reused HUD assets, refresh allocations, draw cost | Host proof: production HUD formatter tests distinguish missing values from known zeros, retain cached timing/memory across batch reset, and label window duration/age. Text formatting bytes/refresh are measured. Source: cached content/style and 4 Hz refresh. Play-test: Unity draw allocations and rendering cost. |
| R6 | Warmed workload, bursts, duration and fixed memory | Host proof: 1,000/10,000 callbacks per simulated second, bursts, configured population inputs, 2,000 simulated seconds, fixed arrays, saturated queue. Open: full collector/writer retained-memory bound across repeated metadata and session operations. |
| R7 | Bounded writer, stalls/failures/loss, allocation and contention | Host proof: blocked and throwing sinks, a real unusable filesystem path, fixed pool, drop-newest, ownership, bounded/repeated shutdown, worker formatting allocation measurement. Open: contended handoff measurement and full failure-lifecycle audit. |
| R8 | 15-second batches, partial shutdown, parseable rotation | Host proof: actual formatter/readback, invariant CSV under fr-FR, headers and partial-stop reason. Open: same-timestamp shutdown, multi-session pairing, configurable retention and marker/metadata loss disclosure. |
| R9 | Complete working-buffer and final output bounds | Measured numeric buffers and representative output only. Open: bounded metadata construction and full writer/encoding/queue accounting, extreme row-size and retained-file bounds. The 1 MiB target is not yet proven. |
| H1 | Complete readable HUD and safe controls | Host proof: `TestHud` checks initial unavailable fields, known zeros, hook availability, cached delayed timings, memory and window ages, and marker identity. Source provides configurable keys and wrapped cached text. Open: full requested-field audit. Play-test: legibility, hotkey conflicts, display placement and normal controls. |
| H2 | Manifest, CSV, horde/window summaries and markers | Host proof: files and basic schema. Open: parse/readback marker timeline, environment association and full horde summary fields/mode information. |
| H3 | Post-load/rescan inventory, coverage, overlaps and errors | Source: initial/delayed/explicit inventory and unresolved owner labels. Open: relevant loader dependency errors, exact owner filtering, bounded construction and isolated-inventory tests. Native registry support needs investigation before declaring coverage unavailable. |
| H4 | Shareable privacy and no upload | Source: allowlisted metadata and local file sink. Open: automated adversarial metadata/error sanitation tests and complete output/path audit. User-supplied marker notes remain explicitly reviewable content. |
| H5 | Repeated matched A/B scenarios | Play-test. TESTING lists scenarios; no actual A/B evidence exists. |
| H6 | Whole-process/GPU/frame/allocation/spike/coverage report | Play-test for actual game measurements. Open: ensure output and test instructions preserve the evidence needed to analyze those runs. |
| H7 | Evaluate 0.5% mean frame increase and repeatable 1 ms hitches | Play-test. No target pass or safe entity ceiling is claimed. |
| H8 | Actual measurements, unsupported counters, gaps | DIAGNOSTICS separates host results from Unity claims. Open until this audit's non-play-test gaps are resolved and the final evidence/report references are refreshed. |

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

The automated lifecycle fixtures prove membership-decision behavior, not that
Unity executes every hook on the expected thread. Likewise, the timing-token
exception fixture proves completion bookkeeping, not Harmony finalizer execution.
Those are separate proof boundaries and must remain explicit.
