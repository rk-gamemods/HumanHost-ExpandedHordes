# Human Host 0.8.326 compatibility review

No update-specific runtime-code fix was identified for either published Expanded
Hordes 0.2.2 or development source 0.2.4 when reviewing Human Host 0.8.326,
Steam build 26180432, on 2026-10-24. Source inspection, serialized-catalog
comparisons and installed-assembly contracts support this result. In-game
compatibility remains unverified. This review changes no plugin version and
publishes no release.

## Versions reviewed

- Published 0.2.2: source commit
  [`8081276`](https://github.com/rk-gamemods/HumanHost-ExpandedHordes/commit/808127642343989dec7339dbe64a55509bec35ee),
  also available as [GitHub release v0.2.2](https://github.com/rk-gamemods/HumanHost-ExpandedHordes/releases/tag/v0.2.2).
  The distribution audit on 2026-10-24 identified 0.2.2 as the version on
  [Steam Workshop item 3806576633](https://steamcommunity.com/sharedfiles/filedetails/?id=3806576633).
- Development 0.2.4: `develop` commit
  [`8a63526`](https://github.com/rk-gamemods/HumanHost-ExpandedHordes/commit/8a635262f96db7c67e71ff34afb1f4edbf73c24a).
  Arrival rate/directions and idle-horde refocus are present in this source but
  absent from published 0.2.2. Merging source preparation did not publish them.

The game comparison starts at 0.8.323, Steam build 25986463. The complete
capture diff contains 54 changed paths, including eight changed C# files.
All eight files were reviewed against both mod versions' patch targets,
reflection fields, relevant callers, configuration and creature identities.
Generated Unity script-registration changes were treated as metadata.
The [0.8.323 report](GAME_COMPATIBILITY_0.8.323.md) is a historical review of
development 0.2.4, not evidence of a 0.2.4 release.

## Patch and catalog findings

The shared gameplay and diagnostic patch implementations are identical between
the two reviewed mod revisions. Their native boundaries remain compatible:

- `Terrain` source is unchanged, including `NPC_Horde_Mgr` scheduling,
  population calculation, spawning, placement, membership, save/restore and
  focus broadcasting. `NPC_Spawner_Mgr` death/pool handling is unchanged.
  The overflow branch in `Delay_Back_Dead_NPC_To_Pool` still yields before
  returning the dead NPC to the pool.
- `AI` source is unchanged, including allowance setup, sound broadcasting,
  focus state and sorting fields. `Player`, `Weather`, `Enviro3.Runtime`,
  `G_Save` and `Global_Funcs` source is unchanged. The debug trigger's time,
  loading and native-start boundaries therefore need no adaptation.
- Changes in `Creature` leave `C_Controller_Base.Play_Anim_BaseLayer`,
  `Input_WSAD`, `DetermineControllerState`, `NPC_Input.On_Char_Died`,
  `Char_Status.set__CurrHP` and the `GPUI_Dead_Body_Mgr` creation/return
  boundaries unchanged. The corpse-limit hook still has one native
  `_MaxCorpseCount` read; corpse diagnostics still observe the same dictionary
  additions and successful removals.
- Development 0.2.4's arrival iterator retains its checked wait, placement
  calls and rejection constants. Its refocus broadcast, membership and AI
  fields are unchanged. These extra paths do not exist in published 0.2.2;
  their absence is not a regression introduced by this game update.
- All five `CreatureCatalog` group GUIDs still resolve to the same serialized
  group objects. Those objects' files are unchanged, preserving the selected
  prefab arrays and indexes. All 11 serialized NPC biome entries retain their
  group membership, weights and non-`bioType` data. `bioType` reference IDs
  moved; the mod resolves its creature groups by GUID rather than those IDs.
  Native horde and AI scalar/event configuration is unchanged. Terrain grid
  width remains 1024 and the serialized biome width remains two grids.

## Changed game-code files

| Assembly | Reviewed change and mod impact |
| --- | --- |
| `AdvancedCullingSystem.Runtime` | Dynamic renderer enable/occlusion handling changes. No mod hook targets this code. Visibility and frame-time effects still need gameplay checks. |
| `Assembly-CSharp` | Adds `NonConvexMeshCollider` geometry helpers, removes unused fields and refreshes script registration. Neither mod version uses the new helper. Native collider changes can still affect placement and combat in play. |
| `Build_System` | Vehicle/chunk duplicate-load protection, prop/tree save handling, deferred destruction, map-capture deduplication and new `SysHouse_Smash_Mgr` mesh carving. Vehicle-hit guards still reach `Smash_Fallen_Manager.Minus_Char_HP`, which uses the HP setter patched by resistance. Culled-tree cleanup now reaches `TopOnHit.Recover_DeadBodies_On_Tree` and the existing corpse-pool return method. Retention raises capacity; it does not veto that removal. |
| `Car` | Nested-rotor drive/link state and build-mode cleanup. The mod changes neither rotor state nor vehicle save records. Check vehicle impacts and movement during combat. |
| `Creature` | Resets fall-height tracking, uses skill-reduced fall damage for the bone-break threshold and removes player aiming chromatic-aberration handling. Damage still enters the same HP setter. Shared movement, death and corpse patch targets are unchanged. |
| `Hand_Tools` | Adds native melee-miss diagnostics and dispatch observations, adjusts player attribution for building hits, and guards arrow impact/parent cleanup. Successful creature damage still reaches `Char_Status._CurrHP`. Native diagnostic reports do not establish an Expanded Hordes failure, and their performance cost has not been measured. |
| `UI` | Rejects pickup stacks of zero or less in `Item_Slot_Mgr.PickItemIn_Bag_Belt`. No mod patch or settings contract depends on that method. |
| `UIResource` | Avoids redundant color/sprite updates in DuloGames UI components. The mod's IMGUI HUD and optional ModMenu registration require no source adaptation; visual checks remain pending. |

## Validation

Both source revisions were built separately against installed 0.8.326
assemblies and BepInEx 5.4.23.5. The 0.2.2 build is a rebuild of its published
source revision, not execution of the distributed binary. No fresh Workshop
download, deployment or game launch was performed.

| Check | Published 0.2.2 source | Development 0.2.4 source |
| --- | --- | --- |
| Release plugin build | 0 warnings, 0 errors | 0 warnings, 0 errors |
| Pure policy checks | 488 assertions passed | 593 assertions passed |
| Policy plus installed-assembly contracts | 589 assertions passed, including pure checks | 739 assertions passed, including pure checks |
| BepInEx config and ModMenu integration | 32 assertions passed | 50 assertions passed |
| Admin Panel compatibility | 17 assertions passed | 17 assertions passed |
| Death/recycling | 48 assertions passed | 48 assertions passed |

Admin Panel checks use real Harmony dispatch with substituted game boundaries.
Recycling checks also substitute game boundaries. Both revisions' recycling
negative control, `--without-guard`, failed as expected with eight disabled
recycled objects in the 12-death fixture. Plugin output contained only the
mod DLL, PDB and dependency manifest, with no copied game assemblies.

From each source checkout, the commands were:

```powershell
dotnet build src/ExpandedHordes.csproj -c Release '-p:HumanHostDir=<game directory>'
dotnet run --project tests/ExpandedHordes.Checks -c Release
dotnet run --project tests/ExpandedHordes.Checks -c Release -- '<game directory>/Human Host_Data/Managed'
dotnet run --project tests/ExpandedHordes.ConfigChecks -c Release '-p:HumanHostDir=<game directory>'
dotnet run --project tests/ExpandedHordes.CompatibilityChecks -c Release '-p:HumanHostDir=<game directory>'
dotnet run --project tests/ExpandedHordes.RecyclingChecks -c Release
```

The negative control adds `-- --without-guard` to the final command.
See [BUILDING.md](BUILDING.md) for local path configuration.

## Runtime and release decision

The evidence does not justify a game-update hotfix for either reviewed version.
Keep published 0.2.2 and source 0.2.4 distinct. A future release of the existing
arrival/refocus work is a separate decision; this compatibility review does not
authorize or certify it.

The checks do not establish actual Unity Harmony detours, callback ordering,
runtime catalog resolution, visuals, performance, save safety or arbitrary mod
combinations. Follow [TESTING.md](TESTING.md), especially melee and arrow hits,
vehicle/rotor impacts, fall damage, tree removal with settled bodies, map
capture and terrain transitions, and survivor save/reload. Test arrival/refocus
only when using development source. Confirm the plugin's version/load line and
inspect new errors or disabled-feature messages before calling either version
play-tested on 0.8.326.
