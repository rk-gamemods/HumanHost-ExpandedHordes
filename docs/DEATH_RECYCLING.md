# Preventing disabled movement after death and reuse

Expanded Hordes 0.2.1 includes a guard for horde zombies recycled during their
own death callback. It prevents a confirmed route to living zombies with
disabled movement and gravity updates. It does not move zombies, change health,
edit saves, or repair every possible cause of airborne characters.

## Status: fixed in the game

Human Host fixed this sequence in Steam build 25606549. That build still
reports version 0.8.316.

- **The change:** the overflow branch of
  `NPC_Spawner_Mgr.Delay_Back_Dead_NPC_To_Pool` now yields once before it
  breaks. The pool return therefore runs after `NPC_Input.On_Char_Died`
  disables the controller.
- **Checked unchanged** in builds 25675256 (0.8.318), 25710579 (0.8.3181)
  and 25752290 (0.8.319); see the [current compatibility review](GAME_COMPATIBILITY.md).
- **The guard is kept as a defence.** It acts only when it sees a pool reset
  inside the same death call, so on fixed builds it changes nothing. Its cost
  per death is a short list check. It applies again if the game ever returns to
  the old order.
- **If airborne zombies return:** check whether that branch still yields before
  `break`.

## Native sequence

Inspected against Human Host 0.8.316:

1. `NPC_Input.On_Char_Died` calls `C_Controller_Base.On_Char_Died`, which
   dispatches the death event to `NPC_Spawner_Mgr.On_Char_Died`.
2. `Back_Dead_NPC_To_Pool` starts `Delay_Back_Dead_NPC_To_Pool`.
   The serialized `_maxDeadRagdolls` limit is four. If several deaths share
   the oldest pending frame, overflow can complete cleanup before its first
   yield. Unity starts a coroutine synchronously until that first yield.
3. `NPC_Horde_Mgr.Put_NPC_Back_To_Pool` resets health, removes spawn ownership,
   deactivates the object and enables its controller for reuse.
4. Control returns to `NPC_Input.On_Char_Died`, which disables the controller
   after the pool reset. `Spawn_Horde_NPC` later activates that object without
   explicitly enabling the controller. Its gravity depends on controller
   updates, so this state can produce airborne living zombies.

The settled-body setting is separate. `GPUI_Dead_Body_Mgr.Spawn_GPUI_Dead_Body`
reads `G_Save.ConfigData._MaxCorpseCount` after the overflow decision. Increasing
retained corpses does not change the four-ragdoll limit. Larger crowds and
area damage can create the simultaneous deaths that expose the ordering defect.
This does not establish which weapon caused a particular recorded burst.

## Guard contract

The prefix on `NPC_Input.On_Char_Died` opens a temporary scope for horde zombies
only. The postfix on `NPC_Horde_Mgr.Put_NPC_Back_To_Pool` records a successful
reset only if the same object is inactive, healthy, enabled, absent from the
horde spawn registry, and no longer awaiting corpse cleanup.

The death finalizer releases the scope even on failure. After successful death
processing it restores the enabled flag only if that witnessed pool reset was
subsequently overwritten and all pool-state checks still hold. An inactive
GameObject does not run `OnEnable` when its component is enabled. The next
normal activation performs the game's usual registration.

An upstream fix that leaves the controller enabled receives no write. A missing
patch target disables this feature through the existing feature installer.
This is state-based compatibility, not a guarantee against every future change
or another mod deliberately changing the same pooled controller.

No recurring scans, terrain raycasts, per-frame counters, or delayed callbacks
are added. Scope storage grows with nested death depth and releases object
references at return. Steady-state callbacks allocate no managed memory in the
host test with Unity boundaries substituted. Actual Unity/Harmony overhead and
game FPS require gameplay measurement.

## Validation

```powershell
dotnet run --project tests/ExpandedHordes.RecyclingChecks -c Release
```

The production prefix, pool postfix, finalizer, and scope tracker run against
small engine boundary substitutes. The fixture represents the recorded nested
ordering; it contains no copied game source. It checks burst sizes, future
correct ordering, no-op repeats, nested calls, exceptions and excluded states.
Passing `-- --without-guard` is a negative control and must fail with eight
disabled recycled objects in the 12-death case.

Installed-assembly checks in `ExpandedHordes.Checks` validate target signatures
and registry types. A separate local-only reproduction extracts the native
death method and initial overflow branch; it reproduces the same threshold with
retained-body limits of 50, 300 and 1,000. It substitutes the engine scheduler
and pool boundary, so it is not an in-game reproduction.

During a user-run gameplay test, confirm the startup log contains
`Horde death/recycling movement protection installed`. The first prevented
failure logs `Prevented a recycled horde zombie from retaining disabled movement`.
That warning appears at most once per session. Check normal kills, replacement
spawns and corpse retention during sustained combat. Runtime verification of
this preventative guard is pending; prior play-testing verified a separate
recovery workaround.

References: [Unity coroutine startup](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.StartCoroutine.html),
[Unity component activation](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnEnable.html),
[Harmony finalizers](https://harmony.pardeike.net/v2/articles/patching-finalizer.html).
