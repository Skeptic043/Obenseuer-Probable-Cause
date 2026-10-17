# Probable Cause

Police need a reason. Cops no longer sense stolen goods in your pockets. They come after you for your bounty.

## Install

### Manual installation

1. Install either [BepInEx 5.4.23.5](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5) (`BepInEx_win_x64_5.4.23.5.zip`) or [BepInEx 6.0.0-be.755 Unity Mono](https://builds.bepinex.dev/projects/bepinex_be/755/BepInEx-Unity.Mono-win-x64-6.0.0-be.755%2B3fab71a.zip) in your Obenseuer game folder.
2. Download the Probable Cause ZIP that matches your installed BepInEx version, then extract it into the Obenseuer game folder. The DLL should end up at `BepInEx/plugins/ProbableCause/ProbableCause.dll`.
3. Launch Obenseuer normally.

## How it works

In the base game, a cop who spots you can question you, chase you, or have you arrested in your sleep just because you're carrying stolen items, even if nobody saw you take them and you have no bounty.

With Probable Cause, police only go by your bounty. Get seen stealing and the theft goes on your record as usual, and the police will respond to it. Items you took unseen stay your business until the police actually stop you. If you are questioned, fined or arrested, they still find and confiscate stolen goods as normal.

Witnesses still report crimes they see, and scripted checks in specific places work as they do in the base game.

## Configuration

None.

## Compatibility

Probable Cause has separate Windows x64 builds for BepInEx 5.4.23.5 and BepInEx 6.0.0-be.755 Unity Mono. Both builds passed offline member, Harmony installation, cleanup, and package checks against Obenseuer 0.5.08. The BepInEx 6 build has not been tested in-game.

No specific incompatibilities are known. If a game update changes the police code this mod relies on, the mod switches itself off and police use the base game's rules.

## AI Use

AI was used to write all of the code in this project. The original design direction, testing, debugging, and release decisions are my own. If you prefer not to use mods developed with AI assistance, I understand and respect that choice.

## Issues and links

Report issues through the Bugs tab on Nexus Mods and include `BepInEx/LogOutput.log`. Check the log for local paths or other personal information before uploading it.

Probable Cause is released under the MIT License. [Support on Ko-fi](https://ko-fi.com/skeptic043) · skeptic043
