## Save path

Set `saveGamePath` in `server_config.json` to the directory of the world you want to host.

Example (Windows):
```
C:\\Users\\you\\AppData\\LocalLow\\TVGS\\Schedule I\\Saves\\12345678901234567\\SaveGame_1
```

Notes:

- Use double backslashes in JSON.
- The server will abort startup if `saveGamePath` is empty or invalid.
- The server will start a new game if the save path is empty.


