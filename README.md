# Expanded Hordes for Human Host

**Alpha with limited local play-testing. Compatibility and save safety have not
been established across game versions and mod combinations.**
Back up your saves and use a disposable test world if you try it.

Expanded Hordes is designed to make Horde Night more dangerous. Choose how many
zombies can arrive, how many can be alive together, how fast they run, and how
much damage they resist. Add chances for large zombies and bosses, or keep more
bodies around after the fighting.

This repository contains only Expanded Hordes. It is a source-code alpha, not a
Steam Workshop release or a tested finished mod. The current plugin version is
0.2.0. See the performance notes below for observations from local play-testing.

## What it changes

### More zombies, on your schedule

The game still controls whether Horde Nights happen and whether they occur every
night, every other night, every third night, and so on. The mod keeps the game's
normal start timing and dawn cutoff.

Two settings control the horde's size:

- **Total Spawn Budget** is how many zombies the whole night can create,
  including replacements after kills. The default is 1,000 before the game's
  Horde Quantity percentage. At 800%, that means up to 8,000 zombies over the
  night, not 8,000 alive together. Dawn can stop spawning before the budget is used.
- **Living Horde Target** is how many horde zombies can be alive at once. The
  default is 75, with a maximum setting of 500. Killing one makes room for
  another while the budget lasts.

The budget setting accepts 1-50,000 **before** the game's multiplier. At 800%,
50,000 becomes a budget of 400,000. These are setting limits, not tested safe
populations or a promise that a night can spawn that many.

For comparison, vanilla starts at 15 total zombies at 100% Horde Quantity, or
120 at 800%, then adds 2 for each previous horde. Its simultaneous living limit
reaches 60. The mod uses your selected budget instead of that per-horde growth.

### Faster runners and tougher enemies

Running speed defaults to **100%**, the game's normal speed, and accepts
25-150%. Changes apply only while nighttime horde spawning is active. Normal
speed returns when spawning finishes or dawn arrives, whichever comes first. Walking and attacks
keep their normal speed; walking zombies are not forced to run.

Damage resistance makes horde zombies harder to kill without increasing their
maximum health or health-based kill XP:

| Zombie category | Default damage prevented |
| --- | ---: |
| Regular zombies | 0% |
| Large non-boss zombies | 0% |
| Bosses | 0% |

Each category can be set from 0% to 95%. At 50% resistance, half the incoming
health damage gets through. At 0%, this mod adds no resistance.

Unlike the running boost, resistance is intended to stay with surviving horde
zombies after dawn and save/load, using the current settings. Ordinary zombies
attracted into the fight do not receive horde resistance. This behavior still
needs gameplay testing.

### Large zombies, bosses and nearby attackers

By default, each newly spawned horde zombie has a **20% chance** to be replaced
with a large non-boss and a **5% chance** to be replaced with a boss, once its
region gate is reached. Each starting attacker and each replacement gets its
own chance. These are not guaranteed numbers or one roll for the whole night.
The game's normal choices can also include special types.

Extra additions pause when **1 boss** or **5 large non-boss zombies** are already
alive in the horde, by default. Vanilla spawns and restored survivors count
toward these limits. The limits only stop this mod's extra selections: they do
not remove enemies or change a vanilla selection, so actual counts can exceed
them. Normal chances resume when the living count falls below the limit.
Set either living limit to **0 for unlimited extra additions**.

Both chance settings accept fractions in 0.01% steps, including **0.5%, 0.25%
and 0.01%**.
Existing whole-number settings remain valid. Enter 0 to disable that extra chance.

The two gates default to region 11, the second cycle of ten biome regions as
you move outward. Set a gate to 1 to allow those extras near the starting area.
Returning inward closes the extra chances again. Saved survivors keep their type.

At horde start, the mod automatically tries to draw nearby zombies toward you.
**Attraction Range** controls how far the lure reaches. It makes no audible sound
and only attracts zombies already nearby. Walls and normal pursuit rules can
still prevent a response.

### More bodies left behind

**Retained Corpse Limit** defaults to **300**, matching the vanilla menu's maximum,
and can be raised to 5,000. The higher of this setting and the game's current
limit is used. It affects the shared pool of settled corpses, including ordinary
zombies.

Corpses do not occupy living horde slots. The game normally removes an older
settled body to make room when the retention target is reached. Its usual
expiry and distance hiding remain in effect. This is not an exact count of all
bodies in the world, and it does not keep physical ragdolls active forever or
guarantee that bodies form solid, climbable piles. Removed corpses can lose loot.

## Settings at a glance

Settings are grouped in this order: Population, Composition, Movement,
Resistance, Corpses, Attraction, Diagnostics. Diagnostics starts with Debug
Mode, Performance Profiling and Detailed Method Timings. Profiling can run
with Debug Mode off; detailed timings require profiling.

| Setting | Default | Allowed range |
| --- | ---: | --- |
| Total Spawn Budget | 1,000 | 1-50,000 before Horde Quantity |
| Living Horde Target | 75 | 1-500 |
| Shared AI Allowance | 90 | 1-1,000 |
| Horde Run Speed Percentage | 100% | 25-150% |
| Regular Zombie Resistance | 0% | 0-95% |
| Large Zombie Resistance | 0% | 0-95% |
| Boss Resistance | 0% | 0-95% |
| Extra Large Zombie Begin Biome | 11 | 1-1,000,000 |
| Extra Boss Zombie Begin Biome | 11 | 1-1,000,000 |
| Extra Large Zombie Percentage | 20% | 0-40%, including fractions |
| Extra Boss Zombie Percentage | 5% | 0-40%, including fractions |
| Large Zombie Living Limit | 5 | 0-500; 0 means unlimited extra additions |
| Boss Zombie Living Limit | 1 | 0-500; 0 means unlimited extra additions |
| Attraction Range | 250 m | 1-250 m |
| Retained Corpse Limit | 300 | 1-5,000 |
| Debug Mode | Off | On / Off |
| Performance Profiling | Off | On / Off |
| Detailed Method Timings | Off | Requires profiling; sampled, adds hook overhead |
| Report File MiB | 5 | Per-stream rotation threshold, 1-64 MiB; keeps current and previous files |

Shared AI Allowance controls how many loaded zombies can focus on targets when
responding to sounds. Vanilla uses 60. Keep it at least as high as Living Horde
Target; otherwise it lowers that target too. This shared setting also affects
ordinary zombies between hordes. Nearby ordinary attackers are additional to
the living horde target.

## Requirements and installation

- Human Host on Windows, using its Unity Mono build.
- BepInEx 5. The alpha was built against BepInEx 5.4.23.5 and Human Host Steam
  build 25448142. Other game builds have not been verified.
- No additional gameplay mod is required. HHMM can edit the standard BepInEx
  config; actual HHMM UI behavior for this alpha still needs testing.

There is no prebuilt download attached to this initial source publication.
See [Building from source](docs/BUILDING.md) to produce the DLL.

1. Close the game.
2. Create `BepInEx/plugins/ExpandedHordes/` inside the game's installation folder.
3. Copy your built `ExpandedHordes.dll` into that folder. Keep only one copy enabled.
4. Launch once to create
   `BepInEx/config/rkgamemods.humanhost.expandedhordes.cfg`, then close the game.
5. Edit **Expanded Hordes** in HHMM, or edit that config file with a text editor.
6. Restart the game after changing settings.

To remove it, close the game and disable or remove the ExpandedHordes plugin
folder. The mod adds no custom save fields. Keep your backup regardless; that
does not make an untested alpha safe for valuable saves.

## Performance, compatibility and bug reports

More living enemies increase CPU load and can cause stutter. One local test at
**200 living enemies and 100% speed** averaged **64.5 FPS**, with middle minutes
around **60-63 FPS** and noticeable stutter, on a Ryzen 7 9800X3D, RTX 4070 Ti
and 64 GB RAM. The tester found that size felt like a full horde, including
large enemies and bosses. This is one system and scenario, not an FPS guarantee.
Other mods changing the same spawning, movement, health or corpse behavior may
conflict. Direct forced-death effects can bypass resistance, and some damage
displays may show the hit amount before reduction. Native survivor-health
restoration and save/load limitations remain.

Debug Mode and Performance Profiling enable nominal 100 ms telemetry buckets.
One background writer flushes CSV and readable summaries every 15 seconds into
`diagnostics/` beside the DLL. Debug Mode also shows a cached overlay, refreshed
at 4 Hz, with configurable position and scale. The compact panel prioritizes
spawning status, living horde enemies and average FPS.
Detailed counters and individual compatibility findings stay in the reports.
Reports split confirmed horde-enemy deaths into regular, large, boss and unknown
types, including deaths from any cause rather than claiming player kill credit.
Turn Debug Mode off in mod settings to hide it. While Debug Mode is enabled,
the configurable Start Horde Now shortcut starts a new horde without waiting
for its random start time. The HUD displays the current binding (default
Ctrl + Shift + Pause). This advances the wave and, if needed, sets the clock
to night; see [trigger behavior](docs/DIAGNOSTICS.md#using-it).
Compatibility inventories refresh automatically at startup, five seconds after
load and when observed bindings change. They check loaded BepInEx key settings
for configured or possible overlaps, naming both owners and settings. Hardcoded,
native, external and custom string bindings remain unknown; see the
[coverage limits](docs/DIAGNOSTICS.md#using-it).
Detailed Method Timings is a separate advanced option, off by default. Light
mode does not install the per-zombie timing hook. All diagnostics default off.

Reports label missing values and delayed CPU/GPU samples. Each data/log file is
bounded to a current and previous file per stream, with a configurable threshold
of 5 MiB by default and up to one batch of overshoot. Errors remain in
the BepInEx log. See [diagnostics semantics and validation](docs/DIAGNOSTICS.md)
for measurement limits and remaining runtime checks. No in-game overhead target
has been certified.

Use the [testing checklist](docs/TESTING.md) when trying the alpha. For a bug report,
include your game build, mod settings, other mods, what happened and steps to
repeat it. Share only relevant log excerpts, and remove usernames, personal file
paths or other private information before posting them.

Developers can read the [code overview](docs/DEVELOPMENT.md). This repository
contains the mod's own source and tests, not game assemblies or decompiled game code.

## License

[MIT](LICENSE). Copyright (c) 2026 rk-gamemods.
The software is provided as-is, without warranty. This license covers the code
and documentation in this repository, not Human Host, BepInEx or other external
software. Not affiliated with Virtual Matrix Studio or Valve.
