# Probable Cause

Tired of police having pocket vision, sensing when you have stolen goods? Put a stop to that with Probable Cause.

## Install

### Manual installation

1. Install either [BepInEx 5.4.23.5](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5) or [BepInEx 6.0.0-be.755 Unity Mono](https://builds.bepinex.dev/projects/bepinex_be/755/BepInEx-Unity.Mono-win-x64-6.0.0-be.755%2B3fab71a.zip) in your Obenseuer game folder.
2. Download Probable Cause, then extract the ZIP into the Obenseuer game folder. The same ZIP supports both BepInEx versions.
3. Launch Obenseuer normally.

## How it works

In the base game, a cop who spots you can question you, chase you, or have you arrested in your sleep just because you're carrying stolen items, even if nobody saw you take them and you have no bounty. With Probable Cause, police only go by your bounty. Get seen stealing and the theft goes on your record as usual, and the police will respond to it. Items you took unseen stay your business until the police actually stop you. If you are questioned, fined or arrested, they still find and confiscate stolen goods as normal.

Witnesses still report crimes they see, and scripted checks in specific places work as they do in the base game.

## Compatibility

Probable Cause supports BepInEx 5.4.23.5 and BepInEx 6.0.0-be.755 Unity Mono on Windows x64 in one download. The version 1.0.2 two-DLL layout was tested under both loaders, and each loader initialized its matching DLL once alongside Interaction Range. If a game update changes the police code this mod relies on, the mod switches itself off and police use the base game's rules.

## AI Use

AI was used to write all of the code in this project. The original design direction, testing, debugging, and release decisions are my own. If you prefer not to use mods developed with AI assistance, I understand and respect that choice.

## Issues and links

[Report an issue](https://github.com/Skeptic043/Obenseuer-Probable-Cause/issues) with `BepInEx/LogOutput.log`. Check the log for local paths or other personal information before uploading it.

[Source code](https://github.com/Skeptic043/Obenseuer-Probable-Cause) · [Changelog](https://github.com/Skeptic043/Obenseuer-Probable-Cause/blob/main/CHANGELOG.md) · [Build instructions](https://github.com/Skeptic043/Obenseuer-Probable-Cause/blob/main/BUILDING.md) · [MIT License](https://github.com/Skeptic043/Obenseuer-Probable-Cause/blob/main/LICENSE) · [Support on Ko-fi](https://ko-fi.com/skeptic043) · skeptic043
