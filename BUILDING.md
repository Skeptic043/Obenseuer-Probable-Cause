# Building Probable Cause

Requirements: Windows, Windows PowerShell 5.1 or PowerShell 7, a .NET SDK supporting the .NET Framework 4.7.2 target and `dotnet msbuild -getProperty`, an owned Obenseuer installation, and the extracted Windows x64 [BepInEx 5.4.23.5](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5) release.

The game references can come from any Obenseuer version. The default loader reference directory is `.local/dependencies/BepInEx5/BepInEx/core`. Each loader assembly is checked against the pinned release hash, and the build rejects missing or different references.

Build and test:

```powershell
.\scripts\Build.ps1 -GamePath 'D:\Games\Obenseuer' -BepInExCorePath 'D:\Dependencies\BepInEx5\BepInEx\core'
.\scripts\Test.ps1 -GamePath 'D:\Games\Obenseuer' -BepInExCorePath 'D:\Dependencies\BepInEx5\BepInEx\core'
```

Build, test, and package:

```powershell
.\scripts\Package.ps1 -GamePath 'D:\Games\Obenseuer' -BepInExCorePath 'D:\Dependencies\BepInEx5\BepInEx\core'
```

To omit the paths, create the ignored file `.local/LocalPaths.props`:

```xml
<Project>
  <PropertyGroup>
    <GamePath Condition="'$(GamePath)' == ''">D:\Games\Obenseuer</GamePath>
    <BepInEx5CorePath Condition="'$(BepInEx5CorePath)' == ''">D:\Dependencies\BepInEx5\BepInEx\core</BepInEx5CorePath>
  </PropertyGroup>
</Project>
```

The scripts stop on failed commands so an old DLL cannot be mistaken for a successful build. Game and loader references are never copied into the plugin output. Acquire those dependencies separately and keep them out of source control and mod packages.

The package script writes `ProbableCause-<version>-BepInEx5.zip` and a source ZIP to `dist/` from explicit file lists. The plugin ZIP contains exactly one `ProbableCause.dll` and no loader assemblies. The script checks the extracted files and plugin hash, scans both packages for local path markers, then builds and tests from a fresh source extraction and compares the DLL with the original candidate.

The tests use the loader's actual Harmony library, the installed game's methods and synthetic managed objects, and run the patched game methods outside Unity. They do not establish live police behavior or gameplay compatibility.
