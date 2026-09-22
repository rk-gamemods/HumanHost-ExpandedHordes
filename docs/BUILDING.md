# Building from source

This produces an **untested alpha**, not a certified working release.
You need a .NET SDK capable of building .NET 8 projects, your own Human Host
installation and BepInEx 5 installed in that game folder. PowerShell examples:

```powershell
Copy-Item GamePaths.local.props.example GamePaths.local.props
```

Edit `GamePaths.local.props` and replace the example path with your Human Host
folder. This file is ignored by Git. Do not commit local paths or game files.

From the repository root:

```powershell
dotnet build src/ExpandedHordes.csproj -c Release
dotnet run --project tests/ExpandedHordes.Checks -c Release -- 'C:/Path/To/Human Host/Human Host_Data/Managed'
```

The installed-assembly checks also inspect `BepInEx/core/BepInEx.dll` in the
corresponding game root. Replace the test command's example path too. Alternatively, supply the game path
directly when building:

```powershell
dotnet build src/ExpandedHordes.csproj -c Release '-p:HumanHostDir=C:/Path/To/Human Host'
```

The plugin is written to `src/bin/Release/ExpandedHordes.dll`. Copy only that DLL
to `BepInEx/plugins/ExpandedHordes/` with the game closed. Build commands do not
install the mod or launch the game. Configuration is created on the first launch.

The plugin targets .NET Standard 2.1. Tests target .NET 8 and inspect game assembly
metadata without launching Unity. All game/BepInEx references use `Private=false`
so they are not copied into the plugin output. No game files are distributed here.

The initial alpha build passed with zero warnings/errors and 245 policy and
installed-assembly checks against Steam build 25448142. Those checks cover rules
and expected patch targets, not actual Harmony detours, spawning, combat,
rendering, HHMM UI or performance. Follow [TESTING.md](TESTING.md) for runtime checks.

The issue #1 diagnostics work adds a pure mode that needs no game files:

```powershell
dotnet run --project tests/ExpandedHordes.Checks -c Release
```

It prints collector and writer microbenchmarks with explicit host-runtime limits.
See [DIAGNOSTICS.md](DIAGNOSTICS.md) for the metric contract and validation gaps.
