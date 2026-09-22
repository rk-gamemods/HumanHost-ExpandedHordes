# Expanded Hordes for Human Host

**Pre-testing alpha: this mod has not been play-tested. There is no guarantee
that it works correctly, works with other mods, or is safe for an existing save.**
Back up your saves and use a disposable test world if you try it.

Expanded Hordes is designed to make Horde Night more dangerous. Choose how many
zombies can arrive, how many can be alive together, how fast they run, and how
much damage they resist. Add chances for large zombies and bosses, or keep more
bodies around after the fighting.

This repository contains only Expanded Hordes. It is a source-code alpha, not a
Steam Workshop release or a tested finished mod. The current plugin version is
0.2.0. Build and automated checks have passed; actual gameplay is still unverified.

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
  default is 75. Killing one makes room for another while the budget lasts.

The budget setting accepts 1-50,000 **before** the game's multiplier. At 800%,
50,000 becomes a budget of 400,000. These are setting limits, not tested safe
populations or a promise that a night can spawn that many.

For comparison, vanilla starts at 15 total zombies at 100% Horde Quantity, or
120 at 800%, then adds 2 for each previous horde. Its simultaneous living limit
reaches 60. The mod uses your selected budget instead of that per-horde growth.

### Faster runners and tougher enemies

Horde zombies run at **125%** of their normal running speed by default. This
applies only while nighttime horde spawning is active. Normal speed returns when
spawning finishes or dawn arrives, whichever comes first. Walking and attacks
keep their normal speed; walking zombies are not forced to run.

Damage resistance makes horde zombies harder to kill without increasing their
maximum health or health-based kill XP:

| Zombie category | Default damage prevented |
| --- | ---: |
| Regular zombies | 25% |
| Large non-boss zombies | 40% |
| Bosses | 50% |

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

The two gates default to region 11, the second cycle of ten biome regions as
you move outward. Set a gate to 1 to allow those extras near the starting area.
Returning inward closes the extra chances again. Saved survivors keep their type.

At horde start, a one-time silent lure also tries to draw nearby zombies toward
you. It acts like a noise at your position and another above you to help reach
past obstacles. You hear no sound. It only reaches zombies already loaded nearby;
it does not create distant enemies or load more of the map. Walls and normal
pursuit rules can still prevent a response.

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

| Setting | Default | Allowed range |
| --- | ---: | --- |
| Total Spawn Budget | 1,000 | 1-50,000 before Horde Quantity |
| Living Horde Target | 75 | 1-1,000 |
| Shared AI Allowance | 90 | 1-1,000 |
| Horde Run Speed Percentage | 125% | 25-300% |
| Regular Zombie Resistance | 25% | 0-95% |
| Large Zombie Resistance | 40% | 0-95% |
| Boss Resistance | 50% | 0-95% |
| Extra Large Zombie Begin Biome | 11 | 1-1,000,000 |
| Extra Boss Zombie Begin Biome | 11 | 1-1,000,000 |
| Extra Large Zombie Percentage | 20% | 0-40% |
| Extra Boss Zombie Percentage | 5% | 0-40% |
| Elevated Sound Height | 100 m | 0-100 m |
| Hearing Radius | 250 m | 1-250 m |
| Retained Corpse Limit | 300 | 1-5,000 |
| Debug Mode | Off | On / Off |
| Performance Profiling | Off | On / Off |

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

More attackers, faster arrivals, bosses and extra corpses can reduce frame rate.
No safe maximum, FPS result or broad compatibility claim has been established.
Other mods changing the same spawning, movement, health or corpse behavior may
conflict. Direct forced-death effects can bypass resistance, and some damage
displays may show the hit amount before reduction. Native survivor-health
restoration and save/load limitations remain.

Debug Mode adds event and placement details to `BepInEx/LogOutput.log`.
Performance Profiling writes a summary every 15 seconds and `performance.csv`
beside the DLL. It reports frame times, selected code timings and CPU/GPU timing
when the game exposes it. Missing data is marked unavailable; a timing hint
does not prove what caused a slowdown. Profiling adds overhead. Both options
default off, and CSV storage is limited to a current and previous file of roughly
5 MiB each. Errors remain logged when debug mode is off.

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
