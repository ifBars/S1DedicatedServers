## Troubleshooting

Current beta builds continue initial IL2CPP support. If a problem only reproduces on IL2CPP, open an issue on the [GitHub repository](https://github.com/ifBars/S1DedicatedServers/issues) and include whether the failing runtime is `Il2cpp_Server` or `Il2cpp_Client`, plus the relevant logs.

### Server won’t start: save path not set
- Error mentions `saveGamePath` not configured.
- Edit `server_config.toml` and set `saveGamePath` to the world folder. On Windows, a single-quoted TOML string is the easiest format for backslash paths.

### Server saves to DevSave
- Ensure you set `saveGamePath`.
- Check logs for `Restored loaded save path:` after the Main scene loads.
- Open an issue on the [GitHub repository](https://github.com/ifBars/S1DedicatedServers/issues).

### Clients stuck or time freezes at 4:00
- Check the dedicated server logs for `TimeManager` patch errors.
- Keep `timeProgressionMultiplier` above zero and restart after changing time-related config.
- Check the client logs for 1 minute based time syncs. (Time UI will be stuck on 4AM between 4 and 7AM)
- Open an issue on the [GitHub repository](https://github.com/ifBars/S1DedicatedServers/issues).

### Console says you lack permission
- Add your SteamID64 to `permissions.toml`, usually by assigning the built-in `operator` or `administrator` group under `[user.<steamid>]`.
- Run `reloadpermissions` or restart the server after editing the file.
- See [Permissions](configuration/permissions.md) for the live file format.

### Web panel does not open
- Confirm `[webPanel] webPanelEnabled = true` in `server_config.toml`.
- Check the server log for the localhost launch URL. Browser auto-open is best-effort only.
- Confirm `webPanelBindAddress` is still `127.0.0.1` and the configured `webPanelPort` is not already occupied.
- If you are running under a hosted panel, leave the web panel disabled and use the stdio host console path instead.

### Connection/port problems
- Confirm that `serverPort` is forwarded correctly for both UDP gameplay and TCP status query.
- If using `SteamGameServer`, confirm `steamGameServerQueryPort` is also open on firewall/NAT over UDP.
- If using the TCP console remotely, confirm `tcpConsolePort` is forwarded separately over TCP.
- Check logs for Tugboat startup and that the loopback client connects.

### Steam game server API fails to initialize
- This usually appears as `Authentication backend initialization failed: Failed to initialize Steam game server API (gamePort=38465, queryPort=27016, mode=eServerModeAuthentication) and no logged-in Steam user API fallback was available`.
- This failure happens while initializing the Steam game server API, before the server attempts anonymous login or `steamGameServerToken` login. Do not assume the token itself was rejected until initialization succeeds.
- Confirm the server is launched from the Schedule I game folder, where `Schedule I.exe` is located. The Steamworks app ID lookup depends on the process working directory when launching directly.
- Confirm `steam_appid.txt` exists beside `Schedule I.exe` and contains only the Schedule I app ID:

  ```text
  3164500
  ```

- Confirm the normal Steam API plugin exists at `Schedule I_Data\Plugins\x86_64\steam_api64.dll`. The Goldberg emulator file `steam_api64.dll.emu`, `steam_settings`, and `userdata` folders are local testing artifacts and are not expected in a normal Steam install.
- If you are relying on an active Steam client context, confirm Steam is running under the same Windows user as the server process.
- Confirm the local machine is not already using the configured `serverPort` or `steamGameServerQueryPort`. Port conflicts can prevent Steam game server initialization even before router forwarding matters.

### Clients disconnect right after connecting
- If authentication is enabled (`authProvider` is not `None`), check server logs for auth failure reasons (provider mismatch, nonce mismatch, timeout, or banned player).
- Verify `authProvider` matches your intended mode (`SteamGameServer` recommended).
- If using Steam game server token login, set `steamGameServerLogOnAnonymous: false` and provide a valid `steamGameServerToken`.
- Keep `authAllowLoopbackBypass` enabled so the internal loopback host path does not get blocked.


