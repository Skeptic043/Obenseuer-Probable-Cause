# Building Probable Cause

Requirements: Windows, Windows PowerShell 5.1 or PowerShell 7, a .NET SDK supporting the .NET Framework 4.7.2 target, `dotnet msbuild -getProperty`, an owned Obenseuer installation, and the extracted Windows x64 releases for both supported loaders:

- [BepInEx 5](https://github.com/BepInEx/BepInEx/releases/latest), using the 5.4.23.5 Windows x64 release
- [BepInEx 6 Unity Mono](https://builds.bepinex.dev/projects/bepinex_be), using the 6.0.0-be.755 Windows x64 build

The game references can come from any Obenseuer version. The default loader reference directories are `.local/dependencies/BepInEx5/BepInEx/core` and `.local/dependencies/BepInEx6-755/BepInEx/core`. Each loader assembly is checked against its pinned release hash, and the build rejects missing or different references. `LoaderTarget` accepts `BepInEx5` or `BepInEx6` and defaults to `BepInEx5`.

Build and test:

```powershell
.\scripts\Build.ps1 -LoaderTarget BepInEx5 -GamePath 'D:\Games\Obenseuer' -BepInExCorePath 'D:\Dependencies\BepInEx5\BepInEx\core'
.\scripts\Test.ps1 -LoaderTarget BepInEx5 -GamePath 'D:\Games\Obenseuer' -BepInExCorePath 'D:\Dependencies\BepInEx5\BepInEx\core'
.\scripts\Build.ps1 -LoaderTarget BepInEx6 -GamePath 'D:\Games\Obenseuer' -BepInExCorePath 'D:\Dependencies\BepInEx6\BepInEx\core'
.\scripts\Test.ps1 -LoaderTarget BepInEx6 -GamePath 'D:\Games\Obenseuer' -BepInExCorePath 'D:\Dependencies\BepInEx6\BepInEx\core'
```

Build, test, and package:

```powershell
.\scripts\Package.ps1 -GamePath 'D:\Games\Obenseuer' -BepInEx5CorePath 'D:\Dependencies\BepInEx5\BepInEx\core' -BepInEx6CorePath 'D:\Dependencies\BepInEx6\BepInEx\core'
```

To omit the paths, create the ignored file `.local/LocalPaths.props`:

```xml
<Project>
  <PropertyGroup>
    <GamePath Condition="'$(GamePath)' == ''">D:\Games\Obenseuer</GamePath>
    <BepInEx5CorePath Condition="'$(BepInEx5CorePath)' == ''">D:\Dependencies\BepInEx5\BepInEx\core</BepInEx5CorePath>
    <BepInEx6CorePath Condition="'$(BepInEx6CorePath)' == ''">D:\Dependencies\BepInEx6\BepInEx\core</BepInEx6CorePath>
  </PropertyGroup>
</Project>
```

The scripts stop on failed commands so an old DLL cannot be mistaken for a successful build. Game and loader references are never copied into the plugin output. Acquire those dependencies separately and keep them out of source control and mod packages.

The package script writes one `ProbableCause-<version>.zip` to `dist/` from an explicit file list. It puts the BepInEx 6 build at `BepInEx/plugins/ProbableCause/ProbableCause.dll`, the BepInEx 5 build beside it as `ProbableCause.BepInEx5.dll`, and the README, changelog, and license in the same plugin folder. No loader assemblies are included. Historical packages already in `dist/` are left alone.

The script checks the extracted files, assembly metadata, references, and plugin hashes. It runs each loader's real type discovery against the two-DLL folder and requires exactly one matching plugin. The BepInEx 5 test also simulates a cached upgrade from the previous single-DLL layout, overlays the universal layout, and checks that discovery moves to `ProbableCause.BepInEx5.dll`. It scans the package for local path markers, then builds and tests both targets from an internal allowlisted source stage and compares each DLL with its original candidate. The temporary source stage is removed and is not distributed. The ZIP is copied to `dist/` only after every check passes.

The tests use each loader's actual Harmony library, the installed game's methods and synthetic managed objects, and run the patched game methods outside Unity. They do not establish live police behavior or gameplay compatibility. `Crime.CheckCrimesForArrest` resolves but cannot be patched or executed offline because it reaches Unity native code. The 1.0.2 universal layout was launched under both pinned loaders with one plugin instance alongside Interaction Range, but no controlled police scenario was performed.
