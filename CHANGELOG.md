# Changelog

## Version 1.0.1

- Added a separate build for BepInEx 6.0.0-be.755 Unity Mono.
- Retained the BepInEx 5.4.23.5 build, plugin identity, and police behavior.
- Both builds passed offline member and Harmony patch checks. The BepInEx 6 build has not been tested in-game.

## Version 1.0.0

Initial release.

- Police questioning, chases, and sleep arrests use the player's bounty instead of carried stolen goods.
- Witnessed crimes still add to the player's bounty, and police continue to respond to them normally.
- Once the player is questioned, fined, or arrested, the game's normal stolen-goods checks and confiscation remain unchanged.
