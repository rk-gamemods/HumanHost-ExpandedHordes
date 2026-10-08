# Game compatibility review

Expanded Hordes 0.2.4 needs no runtime-code change for Human Host 0.8.319,
Steam build 25752290, based on the source and installed-assembly review completed
on 2026-10-07. This is a static compatibility result. No new in-game test or
release was performed for this update.

The review compared all 12 changed source files with game 0.8.3182,
Steam build 25712240, then checked their connections to the mod's patch targets,
reflection fields, creature registry and scene lifecycle. The horde and AI
algorithms, patched death/recycling sequence and special-creature definitions
are unchanged. All expected installed patch contracts still pass.

The changes that can affect observations during play are:

- Native camera smoothing shifts rendered bones and camera targets temporarily.
  The mod's arrival and refocus logic reads root positions during game updates.
  Attraction reads the player's head position; check its behavior while moving.
  Expanded Hordes does not install a smoothing component or disable the native one.
- Native scene-unload cleanup clears missing managers. The mod already clears
  refocus state, cancels pending debug requests and closes its diagnostics window
  on scene unload. Its manager reads and restoration paths check live objects.
- Scene-prop loading skips missing child spawners. The four-ray readiness counter
  remains, and the debug trigger also asks the native horde manager for a valid
  spawn context. Check terrain-loading transitions before starting a horde.
- Train movement, vehicle boarding and ride-camera transitions changed.
  Check arrival, attraction and refocus while riding and after dismounting.

Validation used the installed game assemblies and BepInEx 5.4.23.5:

| Check | Result |
| --- | --- |
| Release plugin build | 0 warnings, 0 errors |
| Policy and installed-assembly contracts | 739 assertions passed |
| Death/recycling checks | 48 assertions passed |
| BepInEx config and ModMenu integration | 50 assertions passed |
| Admin Panel compatibility with real Harmony dispatch and substituted game boundaries | 17 assertions passed |

These checks do not prove Unity callback order, actual game Harmony detours,
visual behavior, performance or compatibility with every other mod. Follow
[TESTING.md](TESTING.md), with particular attention to moving attraction,
vehicle transitions and repeated world exit/reload. Confirm the plugin's load
line and check for new errors or disabled-feature messages before treating this
build as play-tested.
