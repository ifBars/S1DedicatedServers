## Save/Load

Symptoms and fixes:

- Failed to load save info: check the resolved save path and read/write access. An empty `[storage].saveGamePath` is valid and selects `UserData/DedicatedServerSave`; a custom path must identify the intended world folder.
- Load errors, player-state issues, messaging/UI exceptions, or repeat client hitching after importing a normal game save: start a fresh server-managed save first, then copy world files into it without replacing `Players\Player_0`. The dedicated server expects `Player_0` to be its loopback host data. See [Importing an existing single-player save](../configuration/save-path.md#importing-an-existing-single-player-save).
- Otherwise, open an issue on the [GitHub repository](https://github.com/ifBars/S1DedicatedServers/issues).

### Saves fail or shutdown is cancelled

Shutdown stays running when its final save fails so the loaded world remains available for a retry. Updated file timestamps do not prove a complete save: the game writes individual files and may fail partway through.

Use a current release on both server and clients. Related reports cover different failures:

| Report | Behavior | Released change |
| --- | --- | --- |
| [#71](https://github.com/ifBars/S1DedicatedServers/issues/71) / [PR #72](https://github.com/ifBars/S1DedicatedServers/pull/72) | Shutdown could tear down the world before the final save finished. | 1.1.2 waits for native completion before teardown. |
| [#74](https://github.com/ifBars/S1DedicatedServers/issues/74) / [PR #75](https://github.com/ifBars/S1DedicatedServers/pull/75) | `MelonCoroutines.Stop` could throw `routine is null` before the final save started. | 1.1.3 uses cooperative autosave cancellation. |
| [#78](https://github.com/ifBars/S1DedicatedServers/issues/78) / [PR #79](https://github.com/ifBars/S1DedicatedServers/pull/79) | Native write errors could be hidden, and partial saves could report success. | 1.1.5 exposes native errors and rejects those saves; this does not repair the affected world. |

Check the server log for `Save failed` and the following `Native persistence error` entries. The result includes:

- `nativeCompleted`: whether the game's completion event was observed.
- `nativeSaveError`: whether the game set its save-error flag.
- `persistenceErrors`: how many native persistence errors were captured during this save. Some game write failures do not set the native error flag.
- `saveManagerAvailable`: whether the native save manager still exists.

Native error output is bounded and excludes the game's appended `Save string` payload. Check the Unity player log for additional errors; the MelonLoader log alone may not contain them on older releases. Configure a writable file with Unity's `-logFile` launch argument if your host does not retain the player log. Under Wine, use a Windows path visible to the game, such as `-logFile Z:\home\container\UnityPlayer.log`.

Keep a separate copy of the current save directory and any older backups before attempting repair. A copy made while saves are running may contain files from different save attempts; preserve it as recovery evidence rather than treating it as a verified backup. Fix the specific failing path, permission, disk-space problem, or object reported by the native error, then run `save` and confirm success before retrying `shutdown`. Forcing the process to exit can discard progression that exists only in memory.

### Wine/Proton: accented NPC paths fail

[Issue #71](https://github.com/ifBars/S1DedicatedServers/issues/71) reported saves failing at `NPCs/Javier Pérez` with `LANG`/`LC_ALL` unset, and successful saves after setting both to `C.UTF-8`. The follow-up in [#78](https://github.com/ifBars/S1DedicatedServers/issues/78) identifies the same accented path on a Kinetic-hosted IL2CPP server. The provider-specific resolution is not yet confirmed.

Ask the provider to verify that the game's launch environment uses an installed UTF-8 locale. See [Linux, Wine, and hosted panels](../configuration/startup-and-deployment.md#linux-wine-and-hosted-panels) for the environment settings and log-file example. Changing the panel shell or `server_config.toml` does not change the environment of a running Wine process.

Locale changes take effect on the next launch, so do not restart a failing live world without preserving available recovery copies and considering progression that exists only in memory. Do not rename `Javier Pérez`, remove the NPC folder, or replace the whole save merely to suppress the error. If the error persists with the correct locale, check the exact path, permissions, and storage with the provider rather than assuming the cause.

### Reporting a persistent failure

Include the server/client versions and runtime, host OS and Wine/Proton version, resolved save path, and the log around one failed save, including `Native persistence error` entries. Include the Unity player log if available. Remove secrets and any serialized `Save string:` contents before sharing logs; arrange a private transfer if a full save is needed.

An unusable station or plant does not by itself identify the failing saveable. Include the native error from one failed save when reporting the problem; do not delete world objects or player data based only on the generic save-failure message.

