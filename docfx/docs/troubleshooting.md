---
title: Troubleshooting
description: Diagnose common DedicatedServerMod startup, connection, authentication, IL2CPP, and save-load issues.
---

# Troubleshooting

Mono and IL2CPP are both supported. If a problem only reproduces on one runtime, open an issue on the [GitHub repository](https://github.com/ifBars/S1DedicatedServers/issues) and include whether the failing runtime is `Mono_Server`, `Mono_Client`, `Il2cpp_Server`, or `Il2cpp_Client`, plus the relevant logs.

## Setup and Persistence Checks

- [Save failures, cancelled shutdowns, native error fields, and recovery precautions](troubleshooting/save-load.md#saves-fail-or-shutdown-is-cancelled)
- [UTF-8 launch environment and Unity logs for Wine/Proton hosts](configuration/startup-and-deployment.md#linux-wine-and-hosted-panels)
- [Save/shutdown/reload verification after installation or an upgrade](configuration/startup-and-deployment.md#verify-a-new-install-or-upgrade)
- [In-game console binding, permissions, and opening/reopening problems](commands.md#opening-the-in-game-console)

### Server won’t start: save path cannot be prepared
- Leaving `[storage].saveGamePath` empty is supported and uses `UserData/DedicatedServerSave`; an empty setting alone is not a startup error.
- Check the resolved path in the startup log and the specific file-access or load error. Confirm the game process can read and write that folder.
- Set a custom `saveGamePath` only when you intend to host another world folder. On Windows, a single-quoted TOML string is the easiest format for backslash paths. See [Save Path](configuration/save-path.md).

### Server saves to DevSave
- Check the resolved default or custom save path in the startup log and confirm the intended world loads. An empty `saveGamePath` should use `UserData/DedicatedServerSave`, not `DevSave`.
- Follow [Save/Load troubleshooting](troubleshooting/save-load.md) and include the startup and save logs when reporting an unexpected destination.
- Open an issue on the [GitHub repository](https://github.com/ifBars/S1DedicatedServers/issues).

### Clients stuck or time freezes at 4:00
- Check the dedicated server logs for `TimeManager` patch errors.
- Keep `timeProgressionMultiplier` above zero and restart after changing time-related config.
- Check the client logs for 1 minute based time syncs. (Time UI will be stuck on 4AM between 4 and 7AM)
- Open an issue on the [GitHub repository](https://github.com/ifBars/S1DedicatedServers/issues).

### Docker/Wine startup messages or .NET runtime installation stalls

- Wine `fixme:*`, ALSA/audio-device, and headless graphics warnings alone do not identify a mod startup failure. In [#28](https://github.com/ifBars/S1DedicatedServers/issues/28), the reporter confirmed the server was running despite those messages. Check whether the process exits, restarts, or reaches S1DS readiness, then find the first actionable exception in the server and Unity logs.
- For IL2CPP stalls at MelonLoader's .NET installer, use a current image and check `WINEPREFIX`. [#26](https://github.com/ifBars/S1DedicatedServers/issues/26) exposed an older image whose runtime was hidden by a game-directory mount. The current image installs the Windows .NET desktop runtime at `/home/steam/wineprefix`, outside `/home/steam/game`. Do not override that prefix with an empty or stale mounted prefix and expect the preinstalled runtime to remain available.
- Preserve game/save volumes when upgrading the image. See [Docker Deployment](docker.md); deleting your world or publishing the loopback web-panel port is not a startup repair.

### IL2CPP interop generation fails after a game update

- If errors such as `Pass11ComputeTypeSpecifics` follow a game update, follow the [stopped-server update sequence](configuration/startup-and-deployment.md#updating-the-game-or-mod), including the targeted `cpp2il_out` cleanup. The official Docker entrypoint already performs that cleanup.
- Keep the error log if startup still fails afterward. A similar symptom does not establish that the cache is the cause, and a MelonLoader downgrade is not the default recovery.

### Console says you lack permission
- Add your SteamID64 to `permissions.toml`, usually by assigning the built-in `operator` or `administrator` group under `[user.<steamid>]`.
- Run `reloadpermissions` or restart the server after editing the file.
- See [Permissions](configuration/permissions.md) for the live file format.

### Web panel does not open
- Confirm `[webPanel] webPanelEnabled = true` in `server_config.toml`.
- Check the server log for the localhost launch URL. Browser auto-open is best-effort only.
- Confirm `webPanelBindAddress` is still `127.0.0.1` or another loopback address and the configured `webPanelPort` is not already occupied.
- If you set `webPanelBindAddress = '0.0.0.0'`, the panel will not bind. The built-in web panel is loopback-only for security and is not reachable from other LAN machines.
- For remote administration, use the TCP console with a required password and appropriate firewall rules, or use the stdio host console path through a hosted panel such as Pterodactyl.

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
- If the server and game client run on the same PC, launch the normal Schedule I client before starting the Steam game server. Steam may block opening the client after the Steam game server is already running, while starting the client first and then `start_server.bat` can keep both running.
- Confirm the local machine is not already using the configured `serverPort` or `steamGameServerQueryPort`. Port conflicts can prevent Steam game server initialization even before router forwarding matters.

### Clients disconnect right after connecting
- If authentication is enabled (`authProvider` is not `None`), check server logs for auth failure reasons (provider mismatch, nonce mismatch, timeout, or banned player).
- Verify `authProvider` matches your intended mode (`SteamGameServer` recommended).
- If using Steam game server token login, set `steamGameServerLogOnAnonymous: false` and provide a valid `steamGameServerToken`.
- The internal loopback host path bypasses player authentication automatically.


