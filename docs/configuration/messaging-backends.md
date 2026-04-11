---
title: Messaging Backends
---

DedicatedServerMod uses a pluggable backend for `CustomMessaging`, the server-client channel used by addon messaging, verification handshakes, and other dedicated-server features.

The backend changes how those messages travel between server and client. It does not change the mod-facing API. Mods still use the same `CustomMessaging` and `ModManager` hooks regardless of backend.

## Quick Recommendations

| Your setup | Recommended backend | Why |
| --- | --- | --- |
| Mono dedicated server | `FishNetRpc` | Simplest path and the default |
| IL2CPP dedicated server | `SteamNetworkingSockets` | Best dedicated-server fit |
| Docker or cloud hosting | `SteamNetworkingSockets` | Better dedicated-server and Steam relay support |
| Steam-launched player-hosted workflow | `FishNetRpc` or `SteamNetworkingSockets` | Both can work; `FishNetRpc` is simpler |

Avoid `SteamP2P` for normal dedicated-server deployments. It is a legacy compatibility option and should not be your first choice.

## Configuration

Messaging backend settings live under the `[messaging]` section of `server_config.toml`.

Example:

```toml
[messaging]
messagingBackend = 'FishNetRpc'
steamP2PAllowRelay = true
steamP2PChannel = 0
steamP2PMaxPayloadBytes = 1200
steamP2PServerSteamId = ''
```

### Keys

| Key | Type | Default | Notes |
| --- | --- | --- | --- |
| `messagingBackend` | `string` | `'FishNetRpc'` | One of `FishNetRpc`, `SteamP2P`, `SteamNetworkingSockets` |
| `steamP2PAllowRelay` | `bool` | `true` | Used by `SteamP2P` |
| `steamP2PChannel` | `int` | `0` | Used by Steam-backed messaging paths |
| `steamP2PMaxPayloadBytes` | `int` | `1200` | Used by Steam-backed messaging paths |
| `steamP2PServerSteamId` | `string` | `''` | Optional client-side Steam routing hint |

The config file is TOML. Message payloads sent over `CustomMessaging` are still strings. Many mods serialize JSON inside those string payloads, but that is an application-level choice, not the config format.

## Backend Summary

### FishNetRpc

Use this when you want the least operational complexity.

- Default backend
- Works in Mono and IL2CPP builds
- Uses the existing FishNet connection
- No extra Steam messaging setup required

Tradeoffs:

- Depends on the FishNet-side message path being available
- Tied to the active FishNet session lifecycle

Recommended config:

```toml
[messaging]
messagingBackend = 'FishNetRpc'
```

### SteamNetworkingSockets

Use this for dedicated-server-focused deployments, especially IL2CPP, Docker, or hosted environments where Steam server integration is already part of the plan.

- Modern Steam networking path
- Dedicated-server compatible
- Better fit for hosted environments and Steam relay support
- Falls back to `FishNetRpc` during early bootstrap until Steam peer mapping is ready

Tradeoffs:

- More moving parts than `FishNetRpc`
- Requires Steam initialization and valid Steam-side routing

Recommended config:

```toml
[messaging]
messagingBackend = 'SteamNetworkingSockets'
steamP2PChannel = 0
steamP2PMaxPayloadBytes = 1200
steamP2PServerSteamId = ''
```

### SteamP2P

This exists for legacy compatibility and specialized Steam-launched scenarios. It is not the recommended dedicated-server path.

- Legacy Steam P2P API
- Can use Steam relay
- Falls back to `FishNetRpc` during early bootstrap

Tradeoffs:

- Not the preferred path for dedicated servers
- More operational complexity than `FishNetRpc`
- Superseded by `SteamNetworkingSockets` for most Steam-based deployments

Example config:

```toml
[messaging]
messagingBackend = 'SteamP2P'
steamP2PAllowRelay = true
steamP2PChannel = 0
steamP2PMaxPayloadBytes = 1200
steamP2PServerSteamId = ''
```

## Bootstrap Fallback Behavior

Steam-backed messaging cannot use Steam peer routing until the server and client have enough identity information to map the FishNet connection to the Steam peer.

That is why `SteamP2P` and `SteamNetworkingSockets` temporarily use `FishNetRpc` during early bootstrap.

Typical flow:

1. The client connects through FishNet.
2. Early dedicated-server handshake traffic uses the FishNet messaging path.
3. Steam identity mapping becomes available.
4. Messaging switches to the configured Steam backend.

This fallback is automatic. It does not require mod authors to change their send or receive code.

## How To Choose

Choose `FishNetRpc` when:

- you want the simplest setup
- you are on Mono
- you do not need Steam-backed relay behavior

Choose `SteamNetworkingSockets` when:

- you are running a dedicated server as a long-lived hosted service
- you are on IL2CPP
- you want the modern Steam-backed path

Choose `SteamP2P` only when:

- you have a specific legacy requirement for it
- you understand that it is no longer the preferred dedicated-server backend

## Command-Line Override

You can override the backend at startup:

```text
--messaging-backend <fishnetrpc|steamp2p|steamnetworkingsockets>
```

Accepted aliases include:

- `fishnet`, `fishnetrpc`, `fishnet_rpc`
- `steamp2p`, `steam_p2p`
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

- Check whether Steam relay is being used.
- Review `steamP2PMaxPayloadBytes` if you are sending large payloads.
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
