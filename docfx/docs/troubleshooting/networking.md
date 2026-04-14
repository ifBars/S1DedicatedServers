## Networking

Checklist:

- `serverPort` open on firewall/NAT for both UDP gameplay and TCP status query.
- Logs show Tugboat server started and loopback client connected.
- Clients use correct public IP/port to connect.
- If using `SteamGameServer`, `steamGameServerQueryPort` is also open on firewall/NAT.

### Port forwarding
- Forward `serverPort` (default `38465`) on your router to the server's local IP for both UDP and TCP.
- UDP on `serverPort` is the game traffic path.
- TCP on `serverPort` is the DedicatedServerMod status query path used by the client's server-status lookup.
- If you use `SteamGameServer`, also forward `steamGameServerQueryPort` (default `27016`) over UDP.
- If you intentionally expose the TCP console, also forward `tcpConsolePort` over TCP and require a password.
- Allow the same ports through your OS firewall.
- Verify from outside your network using an online port check tool.

If direct joins work but the server never answers status queries or appears unavailable in add/favorites flows, the most common cause is that `serverPort` was forwarded only for UDP and not for TCP.

Guides:

- [How to Forward Ports on Your Router (How‑To Geek)](https://www.howtogeek.com/66214/how-to-forward-ports-on-your-router/)
- [Router‑specific port forwarding guides (PortForward.com)](https://portforward.com/router.htm)

If you cannot forward ports (CGNAT/ISP restrictions), consider hosting on a VPS or having a friend host it for you.

### Connects then disconnects after a short delay
- This is commonly an authentication handshake timeout (`authTimeoutSeconds`, default 30s), not a raw network failure.
- Check server logs for auth-specific messages and failure reasons.
- Confirm `authProvider` and Steam game server login settings are configured correctly.
- If you use `SteamGameServer`, make sure `steamGameServerQueryPort` is reachable from the internet.
- Keep `authTimeoutSeconds` at `30` seconds minimum, and prefer `60` seconds for public servers or slower client hardware/network conditions.
- Lower values can disconnect clients before they finish the Steam/auth handshake.


