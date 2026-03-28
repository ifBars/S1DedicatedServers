## Troubleshooting

### Server won’t start: save path not set
- Error mentions `saveGamePath` not configured.
- Edit `server_config.json` and set `saveGamePath` to the world folder. Use double backslashes on Windows.

### Server saves to DevSave
- Ensure you set `saveGamePath`.
- Check logs for: “Restored loaded save path: …” after the Main scene loads.
- Open an issue on the [GitHub repository](https://github.com/ifBars/S1DedicatedServers/issues).

### Clients stuck or time freezes at 4:00
- Check the dedicated server logs for `TimeManager` patch errors.
- Keep `timeProgressionMultiplier` above zero and restart after changing time-related config.
- Check the client logs for 1 minute based time syncs. (Time UI will be stuck on 4AM between 4 and 7AM)
- Open an issue on the [GitHub repository](https://github.com/ifBars/S1DedicatedServers/issues).

### Console says you lack permission
- You may need to add your SteamID64 to `operators` and/or `admins` in `server_config.json`.
- Restart the server

### Connection/port problems
- Confirm that you forwarded the correct port in `serverPort` in `server_config.json`.
- Confirm port in `serverPort` is open on firewall/NAT.
- Check logs for Tugboat startup and that the loopback client connects.

### Clients disconnect right after connecting
- If authentication is enabled (`authProvider` is not `None`), check server logs for auth failure reasons (provider mismatch, nonce mismatch, timeout, or banned player).
- Verify `authProvider` matches your intended mode (`SteamGameServer` recommended).
- If using Steam game server token login, set `steamGameServerLogOnAnonymous: false` and provide a valid `steamGameServerToken`.
- Keep `authAllowLoopbackBypass` enabled so the internal loopback host path does not get blocked.


