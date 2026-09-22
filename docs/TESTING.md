# Alpha play-test checklist

**Not yet play-tested. No guarantee of function or compatibility.** Back up saves
and use a disposable world. Record the game build, mod version and other mods.

- Confirm the game loads the plugin and its log names Expanded Hordes 0.2.0.
  Check for errors or disabled-feature messages. Confirm HHMM shows the friendly
  name and 16 settings. Enable Debug Mode and restart to see catalog/event details.
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
  Turn debug/profiling off and restart; extra summaries and new CSV rows should stop.
- Repeat relevant checks with other mods you normally use. A clean build does
  not establish that two mods work together or that high settings perform well.

Before reporting results, remove private information from logs and screenshots.
No performance ceiling or publication-ready status should be inferred from this
checklist until the actual tests have been completed and their results recorded.
