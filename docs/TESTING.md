# Alpha play-test checklist

**Alpha with limited local play-testing.** Back up saves
and use a disposable world. Record the game build, mod version and other mods.

- Confirm the game loads the plugin and its log names Expanded Hordes 0.2.0.
  Check for errors or disabled-feature messages. Confirm HHMM shows the friendly
  name and current settings. Enable Debug Mode and restart to see the overlay
  and catalog/event details.
- Try base budget 10 and living target 3. At Horde Quantity 100%, expect a total
  budget of 10; at 200%, expect 20. Living target should stay 3. Confirm kills
  allow replacements and no new spawns arrive after the budget is exhausted.
- Check the game's disabled-horde and every-N-nights choices. With budget left,
  spawning should end around native dawn, allowing the game's polling delay.
- Compare running at 100% and 125%. Check ordinary zombies, walking and attacks
  stay normal, and survivors lose the running boost when spawning ends or at dawn.
- Temporarily set both extra-type region gates to 1 and chances to 40%. Check
  large types and bosses for appearance, movement, combat, death and loot.
  Random chances do not guarantee every type in a short test. Restore settings.
- Test living special limits of 1 boss and 5 large enemies. Vanilla spawns and
  restored survivors must count. At the limit, extra selections must stop while
  the original vanilla choice stays unchanged. After a death or removal reduces
  the count, the normal chance must resume. Setting 0 must remove that extra-type
  limit without removing existing enemies. Vanilla special spawns may exceed it.
- Enter fractional chances such as 0.5%, 0.25% and 0.01%; save, restart and verify
  the values remain unchanged. Existing integer values must still load. Check
  fractional selection with deterministic tests; a short play-test cannot prove
  a rare probability.
- Verify new defaults: 100% speed, 0% resistance for all three categories, and
  living target 75. The living target accepts at most 500 and speed at most 150%.
  Shared AI Allowance still defaults to 90 and accepts up to 1,000.
- Compare the same type/level and same weapon/hit location at 0% and 50%
  resistance. Actual HP loss should halve while maximum HP and kill XP stay the
  same. Damage text may show the unreduced hit. Test the three categories
  separately, healing and a hit that remains lethal after reduction.
- Confirm ordinary attracted zombies do not gain resistance. Leave horde
  survivors after dawn and save/reload them: resistance should remain, using
  current settings. Later ordinary spawns must not inherit the horde effect.
- Check sustained kills keep allowing living replacements when corpse retention
  fills. Try 300, then 500 retained bodies, with enough corpse lifetime to observe
  the difference. Check expiry, distance hiding, looting and reload. Temporary
  ragdolls and hidden saved bodies are not the same as visible settled corpses.
- Turn profiling on, restart and collect several 15-second samples during idle,
  a horde and heavy corpse accumulation. Missing CPU/GPU data must say unavailable.
  Turn Debug Mode and profiling off and restart; the overlay, extra summaries
  and new CSV rows should stop.
- Repeat relevant checks with other mods you normally use. A clean build does
  not establish that two mods work together or that high settings perform well.

Before reporting results, remove private information from logs and screenshots.
The [README performance note](../README.md#performance-compatibility-and-bug-reports)
records one observed 200-enemy run. It does not establish a safe maximum or replace
the remaining checks above.

## Issue #1 runtime gate

Use [DIAGNOSTICS.md](DIAGNOSTICS.md) to interpret the counters. Automated host
results are separate from this gate; none of the following is checked off yet.

- [ ] With all diagnostics off, verify no diagnostic-only hooks/writer are active.
  With light profiling on, verify `Zombie_Agent._Update` has no diagnostic patch.
  Enable detailed timings separately and confirm observed/timed/cap-skipped counts.
- [ ] Verify the overlay appears with Debug Mode and disappears when it is off.
  Check HUD position/scale, text refresh and draw cost separately. Confirm
  the Start Horde Now hint follows its configured binding and other mods'
  controls remain unchanged. With Debug Mode off, the shortcut must do nothing.
- [ ] Trigger a horde during daytime and at night. Confirm one new wave starts,
  daytime changes to night, and repeated presses cannot restart an active wave.
  Test loading, pause, menus, disabling Debug Mode during the pending night
  transition, and changing worlds: no delayed trigger may fire afterward.
- [ ] Compare failed spawn attempts, successful registration, saved restoration,
  confirmed death, live pool return, duplicate notification and reused identities.
  Verify the fresh/restored/death/other totals against visible/native state. Check
  regular, large and boss deaths separately, including restored enemies; category
  death totals must sum to total deaths. A live pool return must add no death.
  Unknown classifications must remain labeled unknown. Check
  corpse pool adds/removes including restored bodies and distance hiding.
- [ ] Test pause, a long load/stall, scene unload and orderly quit. Verify actual
  durations, gap counts, partial summaries and unavailable CPU/GPU values. Verify
  horde summaries at spawning stop and later survivor events without double-counting
  cumulative snapshots. Check native budget/effective corpse limits with features
  deliberately unavailable and with another mod setting a higher value.
- [ ] Inspect automatic startup and five-second post-load inventories. Check
  hotkey findings with two loaded plugins sharing a configured key and with
  different modifiers. Change a binding and verify the inventory refreshes.
  Findings must name both owners/settings and distinguish configured from
  possible overlaps. Unreadable or hardcoded keys must remain unknown. Harmony
  owner mappings must remain unresolved unless an exact plugin GUID matches.
  Review report privacy before sharing.
- [ ] Run diagnostics off; light profiling without Debug Mode; debug + profiling;
  and detailed timing. Match game/build/settings/mods, warm-up, camera, scenario,
  population trajectory and frame cap. Use at least three repetitions of each.
  Measure frame distributions and whole-process/GPU impact externally or through
  a separate batched measurement. Do not use only the collector's own callback
  timing as the overhead estimate.
- [ ] Record host/runtime, raw measurements, run variation, allocation domains,
  achieved sample rate, gaps, writer losses and flush-boundary behavior. Resolve
  the proposed <=0.5% mean frame-time increase and absence of repeatable 1 ms
  telemetry-induced hitches, or report that noise prevents a conclusion. Measure
  detailed mode separately, including installed-but-skipped hooks.
- [ ] Measure total numeric/metadata/encoding working buffers and UI/runtime
  allocations on Unity Mono. Exercise unwritable/full disk and a stalled writer
  in a disposable test location. Confirm gameplay continues and shutdown remains
  bounded. The pure host tests do not prove the native integration.
