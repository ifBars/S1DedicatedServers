## Save path

Set `saveGamePath` in `server_config.toml` to the directory of the world you want to host. Leave it empty to use the server-managed default at `UserData/DedicatedServerSave`.

Example (Windows):
```
C:\Users\you\AppData\LocalLow\TVGS\Schedule I\Saves\12345678901234567\SaveGame_1
```

Notes:

- In TOML, prefer a single-quoted string for Windows paths so backslashes do not need escaping.
- When `saveGamePath` is empty, the server resolves the default save path and prepares it from the game's `DefaultSave` template plus the embedded loopback `Player_0` data.
- When `saveGamePath` points at a custom folder, the server prepares missing core save files before loading when possible.
- Startup still fails if the resolved path cannot be prepared or cannot be read as a Schedule I save.

## Validate Persistence Before Using the World

Loading successfully does not prove that the server can write every save file. Confirm that the game process can create, update, and remove files and subdirectories in the resolved save folder, and that the host has available disk space. For Wine/Proton, also check the [UTF-8 launch environment](startup-and-deployment.md#linux-wine-and-hosted-panels), including paths containing accented NPC names.

Complete a [save/shutdown/reload check](startup-and-deployment.md#verify-a-new-install-or-upgrade) before inviting players. Keep known-good backups outside the active save directory. A directory copied while a save is running can contain a mixture of old and new files; do not treat it as a verified restore point.

If any save fails or shutdown is cancelled, preserve existing backups and read [Save/Load troubleshooting](../troubleshooting/save-load.md#saves-fail-or-shutdown-is-cancelled) before moving, renaming, or deleting files.

## Importing an existing single-player save

Starting a new server-managed save is the recommended path. DedicatedServerMod runs a local loopback host player internally, and the server-managed save is prepared with the expected loopback `Player_0` data. A save copied directly from a normal client install may contain local-player state that was valid for single-player or listen-host play but is not valid for a headless dedicated server.

If you want to import an existing world, use this safer migration flow:

1. Leave `saveGamePath` empty and start the dedicated server once so it creates `UserData/DedicatedServerSave`.
2. Stop the server.
3. Back up both the server-managed save and the source save.
4. Copy world/progression files from the source save into `UserData/DedicatedServerSave`, but do not overwrite the server-created `Players\Player_0` data.
5. Start the server and check the MelonLoader log before opening the world to other players.

Avoid copying the source save folder wholesale over the dedicated server folder. In particular, replacing the server-created `Player_0` data can confuse systems that expect `Player_0` to represent the dedicated server loopback host, and can lead to save-load, UI, messaging, or player-state issues.

