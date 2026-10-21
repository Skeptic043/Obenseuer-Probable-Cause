# Changelog

## Version 1.0.2

- One download now supports both BepInEx 5 and BepInEx 6. Existing installations can be upgraded by extracting the new ZIP over the game folder.
- Documentation files now stay inside the Probable Cause plugin folder instead of sharing names at the game root.
- Police behavior is unchanged from version 1.0.1.

## Version 1.0.1

Initial release.

- Police questioning, chases, and sleep arrests use the player's bounty instead of carried stolen goods
- Witnessed crimes still add to the player's bounty, and police continue to respond to them normally
- Once the player is questioned, fined, or arrested, the game's normal stolen-goods checks and confiscation remain unchanged
- Separate builds are provided for BepInEx 5 and BepInEx 6
- If you install Probable Cause while a police officer is already pursuing you, the current pursuit is not cancelled and may continue or restart until it ends normally.
