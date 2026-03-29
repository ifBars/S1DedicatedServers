## Save path

Set `saveGamePath` in `server_config.toml` to the directory of the world you want to host.

Example (Windows):
```
C:\Users\you\AppData\LocalLow\TVGS\Schedule I\Saves\12345678901234567\SaveGame_1
```

Notes:

- In TOML, prefer a single-quoted string for Windows paths so backslashes do not need escaping.
- The server will abort startup if `saveGamePath` is empty or invalid.
- The server will start a new game if the save path is empty.


