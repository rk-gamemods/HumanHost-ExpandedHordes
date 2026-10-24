# Human Host 0.8.326 compatibility

Reviewed on 2026-10-24 against Steam build 26180432. The intended mod version is
0.2.4. No update-specific runtime change or version bump was required.

The review covered the complete 54-path game-capture diff and all eight changed
C# files: AdvancedCullingSystem.Runtime, Assembly-CSharp, Build_System, Car,
Creature, Hand_Tools, UI and UIResource. Direct patch targets, relevant callers,
configuration and creature catalogs were checked against the installed game.

Shared patch boundaries remain unchanged. The five creature identities and all
11 biome group/weight configurations remain compatible. Changed damage callers
still use the patched HP setter. Tree cleanup still reaches the observed native
corpse-return boundary. Compilation alone was not used to establish compatibility.

## Validation

The runtime source and tests at `8a635262f96db7c67e71ff34afb1f4edbf73c24a`
passed against the installed game and BepInEx 5.4.23.5:

| Check | Result |
| --- | --- |
| Release build | 0 warnings, 0 errors |
| Pure policy | 593 assertions passed |
| Policy and installed contracts, including pure policy | 739 assertions passed |
| Config and ModMenu | 50 assertions passed |
| Admin Panel | 17 assertions passed |
| Recycling | 48 assertions passed |

The recycling `--without-guard` negative control failed as expected: eight
disabled recycled objects in the 12-death fixture. Documentation-only release
preparation preserves that tested runtime source and test suite.

The arrival controls and idle-refocus implementation were already integrated
and accepted before this review. Issues #17 and #19 are closed as completed;
PR #22 records the user's successful gameplay test of their integrated code.
Publishing that existing implementation completes prior release work rather
than adding a fix for this game update.

Actual Unity detours, gameplay, performance and save safety have not been freshly
verified on 0.8.326. The automated checks do not replace the
[runtime checklist](TESTING.md). The earlier
[0.8.323 report](GAME_COMPATIBILITY_0.8.323.md) is historical evidence.
