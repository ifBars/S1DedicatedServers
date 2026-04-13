---
title: Messaging Backends
---

DedicatedServerMod uses a pluggable backend for `CustomMessaging`, the server-client channel used by addon messaging, verification handshakes, and other dedicated-server features.

The backend changes how those messages travel between server and client. It does not change the mod-facing API. Mods still use the same `CustomMessaging` and `ModManager` hooks regardless of backend.

## Quick Recommendations

| Your setup | Recommended backend | Why |
| --- | --- | --- |
| Mono dedicated server | `FishNetRpc` | Simplest path and the default |
| IL2CPP dedicated server | `FishNetRpc` | Works well and stays on the same FishNet callback/tick path the game already uses |
| Docker or cloud hosting | `FishNetRpc` or `SteamNetworkingSockets` | Use `FishNetRpc` for simplicity; use sockets when you specifically want Steam relay/routing behavior |
| Steam-launched player-hosted workflow | `FishNetRpc` | Closest to the base game path and lowest operational overhead |

## Configuration

Messaging backend settings live under the `[messaging]` section of `server_config.toml`.

Example:

```toml
[messaging]
messagingBackend = 'FishNetRpc'
steamNetworkingSocketsVirtualPort = 0
steamNetworkingSocketsMaxPayloadBytes = 1200
steamNetworkingSocketsServerSteamId = ''
```

### Keys

| Key | Type | Default | Notes |
| --- | --- | --- | --- |
| `messagingBackend` | `string` | `'FishNetRpc'` | One of `FishNetRpc` or `SteamNetworkingSockets` |
| `steamNetworkingSocketsVirtualPort` | `int` | `0` | Virtual port used by Steam Networking Sockets |
| `steamNetworkingSocketsMaxPayloadBytes` | `int` | `1200` | Maximum Steam Networking Sockets payload size |
| `steamNetworkingSocketsServerSteamId` | `string` | `''` | Optional client-side Steam routing hint |

The config file is TOML. Message payloads sent over `CustomMessaging` are still strings. Many mods serialize JSON inside those string payloads, but that is an application-level choice, not the config format.

## Backend Summary

### FishNetRpc

Use this when you want the least operational complexity.

- Default backend
- Works in Mono and IL2CPP builds
- Uses the existing FishNet connection
- No extra Steam messaging setup required
- Reuses the FishNet callback/tick path that is already active for the normal game server flow

Tradeoffs:

- Depends on the FishNet-side message path being available
- Tied to the active FishNet session lifecycle

Recommended config:

```toml
[messaging]
messagingBackend = 'FishNetRpc'
```

### SteamNetworkingSockets

Use this when you specifically want Steam relay or Steam-side routing behavior on top of the normal dedicated-server flow.

- Modern Steam networking path
- Dedicated-server compatible
- Gives you Steam relay and Steam-side routing behavior
- Falls back to `FishNetRpc` during early bootstrap until Steam peer mapping is ready

Tradeoffs:

- More moving parts than `FishNetRpc`
- Adds its own callback and polling work on top of the FishNet work already happening every frame
- Requires Steam initialization and valid Steam-side routing

Recommended config:

```toml
[messaging]
messagingBackend = 'SteamNetworkingSockets'
steamNetworkingSocketsVirtualPort = 0
steamNetworkingSocketsMaxPayloadBytes = 1200
steamNetworkingSocketsServerSteamId = ''
```

## Bootstrap Fallback Behavior

Steam Networking Sockets cannot use Steam peer routing until the server and client have enough identity information to map the FishNet connection to the Steam peer.

That is why `SteamNetworkingSockets` temporarily uses `FishNetRpc` during early bootstrap.

Typical flow:

1. The client connects through FishNet.
2. Early dedicated-server handshake traffic uses the FishNet messaging path.
3. Steam identity mapping becomes available.
4. Messaging switches to the configured Steam backend.

This fallback is automatic. It does not require mod authors to change their send or receive code.

## How To Choose

Choose `FishNetRpc` when:

- you want the simplest setup
- you want the lowest overhead path
- you do not need Steam relay or Steam-side routing behavior

Choose `SteamNetworkingSockets` when:

- you specifically want Steam relay
- you want Steam-side peer routing semantics
- you are already committed to the additional Steam transport moving parts

## Command-Line Override

You can override the backend at startup:

```text
--messaging-backend <fishnetrpc|steamnetworkingsockets>
```

Accepted aliases include:

- `fishnet`, `fishnetrpc`, `fishnet_rpc`
- `steamsockets`, `steam_sockets`, `steamnetworkingsockets`, `steam_networking_sockets`

## Troubleshooting

### Backend will not initialize

- Confirm the `[messaging]` section in `server_config.toml` uses a valid backend name.
- For Steam-backed options, confirm Steam initialization succeeds on the active runtime.
- Temporarily switch to `FishNetRpc` to isolate whether the issue is backend-specific.

### Early messages do not arrive on Steam-backed backends

- Remember that early join/auth traffic may still be using the FishNet fallback path.
- Check logs for Steam peer mapping readiness.
- Make sure the connection has progressed far enough for dedicated-server messaging to be fully ready.

### FishNetRpc reports readiness issues

- Wait until the normal dedicated-server client lifecycle is ready before sending addon traffic.
- For mod code, prefer `ModManager.ClientPlayerReady` before client-originated traffic that depends on full readiness.

### Steam-backed messages are slow or unreliable

- Check whether Steam relay is actually needed for your deployment.
- Review `steamNetworkingSocketsMaxPayloadBytes` if you are sending large payloads.
- Keep payloads small and frequent instead of sending large blobs.

## Security Notes

Backend choice does not remove the need to validate messages. Always validate client-originated commands on the server.

Example:

```csharp
using DedicatedServerMod.API;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Shared.Networking;
using DedicatedServerMod.Shared.Permissions;

CustomMessaging.ServerMessageReceived += (conn, cmd, data) =>
{
    if (cmd != "admin_command")
    {
        return;
    }

    ConnectedPlayerInfo player = S1DS.Server.Players?.GetPlayer(conn);
    if (player == null || string.IsNullOrWhiteSpace(player.TrustedUniqueId))
    {
        return;
    }

    if (!PermissionManager.IsAdmin(player.TrustedUniqueId))
    {
        return;
    }

    // Process the authorized command.
};
```

## Related Documentation

- [Custom Messaging API](../modding/messaging.md)
- [Authentication](authentication.md)
- [Docker Deployment](../docker.md)
- [Networking Troubleshooting](../troubleshooting/networking.md)
