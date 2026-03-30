## Troubleshooting

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
- Confirm that you forwarded the correct port in `serverPort` in `server_config.toml`.
- Confirm port in `serverPort` is open on firewall/NAT.
- Check logs for Tugboat startup and that the loopback client connects.

### Clients disconnect right after connecting
- If authentication is enabled (`authProvider` is not `None`), check server logs for auth failure reasons (provider mismatch, nonce mismatch, timeout, or banned player).
- Verify `authProvider` matches your intended mode (`SteamGameServer` recommended).
- If using Steam game server token login, set `steamGameServerLogOnAnonymous: false` and provide a valid `steamGameServerToken`.
- Keep `authAllowLoopbackBypass` enabled so the internal loopback host path does not get blocked.


