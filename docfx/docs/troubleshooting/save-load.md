## Save/Load

Symptoms and fixes:

- Failed to load save info: set `saveGamePath`; make sure the path isn't invalid.
- Load errors, player-state issues, messaging/UI exceptions, or repeat client hitching after importing a normal game save: start a fresh server-managed save first, then copy world files into it without replacing `Players\Player_0`. The dedicated server expects `Player_0` to be its loopback host data. See [Importing an existing single-player save](../configuration/save-path.md#importing-an-existing-single-player-save).
- Otherwise, open an issue on the [GitHub repository](https://github.com/ifBars/S1DedicatedServers/issues).

### Saves fail or shutdown is cancelled

Shutdown stays running when its final save fails so the loaded world remains available for a retry. Updated file timestamps do not prove a complete save: the game writes individual files and may fail partway through.

Check the server log for `Save failed` and the following `Native persistence error` entries. The result includes:

- `nativeCompleted`: whether the game's completion event was observed.
- `nativeSaveError`: whether the game set its save-error flag.
- `persistenceErrors`: how many native persistence errors were captured during this save. Some game write failures do not set the native error flag.
- `saveManagerAvailable`: whether the native save manager still exists.

Native error output is bounded and excludes the game's appended `Save string` payload. Check the Unity player log for additional errors; the MelonLoader log alone may not contain them on older releases. Configure a writable file with Unity's `-logFile` launch argument if your host does not retain the player log. Under Wine, use a Windows path visible to the game, such as `-logFile Z:\home\container\UnityPlayer.log`.

Keep a separate copy of the current save directory and any older backups before attempting repair. A copy made while saves are running may contain files from different save attempts; preserve it as recovery evidence rather than treating it as a verified backup. Fix the specific failing path, permission, disk-space problem, or object reported by the native error, then run `save` and confirm success before retrying `shutdown`. Forcing the process to exit can discard progression that exists only in memory.

For Wine hosts, [issue #71](https://github.com/ifBars/S1DedicatedServers/issues/71) reported accented NPC paths failing without a UTF-8 locale. If the native error identifies that problem, ask the provider to check `LANG` and `LC_ALL` in the process environment and use an installed UTF-8 locale, such as `C.UTF-8`, for the next launch. This is a reported configuration cause, not a diagnosis for every save failure.

An unusable station or plant does not by itself identify the failing saveable. Include the native error from one failed save when reporting the problem; do not delete world objects or player data based only on the generic save-failure message.

