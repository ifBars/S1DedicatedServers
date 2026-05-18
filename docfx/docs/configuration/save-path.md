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

## Importing an existing single-player save

Starting a new server-managed save is the recommended path. DedicatedServerMod runs a local loopback host player internally, and the server-managed save is prepared with the expected loopback `Player_0` data. A save copied directly from a normal client install may contain local-player state that was valid for single-player or listen-host play but is not valid for a headless dedicated server.

If you want to import an existing world, use this safer migration flow:

1. Leave `saveGamePath` empty and start the dedicated server once so it creates `UserData/DedicatedServerSave`.
2. Stop the server.
3. Back up both the server-managed save and the source save.
4. Copy world/progression files from the source save into `UserData/DedicatedServerSave`, but do not overwrite the server-created `Players\Player_0` data.
5. Start the server and check the MelonLoader log before opening the world to other players.

Avoid copying the source save folder wholesale over the dedicated server folder. In particular, replacing the server-created `Player_0` data can confuse systems that expect `Player_0` to represent the dedicated server loopback host, and can lead to save-load, UI, messaging, or player-state issues.

