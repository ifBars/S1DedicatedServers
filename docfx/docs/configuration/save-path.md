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


