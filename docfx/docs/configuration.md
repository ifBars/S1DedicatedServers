## Configuration Overview

DedicatedServerMod splits configuration by responsibility so operators do not need to manage one giant file.

The server creates the config files automatically on first run. If you still have a legacy `server_config.json`, the server imports it on startup and writes the current TOML-based files beside it. Legacy permission data is also migrated out of `server_config.toml` into `permissions.toml`.

## Config Files

| File | Purpose | Read this when |
| --- | --- | --- |
| `server_config.toml` | Runtime server settings such as storage, auth, messaging, gameplay, autosave, logging, and operator surfaces | You are bringing up or tuning a server |
| `permissions.toml` | Groups, bans, grants, denies, and command authorization | You are managing staff access or player restrictions |
| `client_mod_policy.toml` | Client mod verification policy overrides | You are tightening or customizing client mod policy |

## Minimum Setup

The shortest path to a working server is:

1. Set `[storage].saveGamePath` to a valid Schedule I save folder.
2. Choose an authentication provider under `[authentication]`.
3. Pick a messaging backend under `[messaging]`.
4. Decide how you want to operate the server: local desktop, hosted panel, TCP console, or localhost web panel.
5. Review autosave and gameplay defaults before opening the server to other players.

Minimal example:

```toml
[storage]
saveGamePath = 'C:\Users\you\AppData\LocalLow\TVGS\Schedule I\Saves\<SteamID>\SaveGame_1'

[authentication]
authProvider = 'SteamGameServer'
authTimeoutSeconds = 60
authAllowLoopbackBypass = true
steamGameServerLogOnAnonymous = true
steamGameServerQueryPort = 27016
steamGameServerMode = 'Authentication'

[messaging]
messagingBackend = 'FishNetRpc'
```

The save folder must already contain files such as `Game.json` and `Metadata.json`.

## Recommended Starting Points

| Scenario | Start with |
| --- | --- |
| Local/private Mono testing | `authProvider = 'None'` or `SteamGameServer` if you still want Steam identity checks, plus `messagingBackend = 'FishNetRpc'` |
| Public native server | `authProvider = 'SteamGameServer'`, `authTimeoutSeconds = 60`, and a reviewed `permissions.toml` |
| Docker or hosted panel | `authProvider = 'SteamGameServer'`, choose `FishNetRpc` for the simplest path or `SteamNetworkingSockets` if you want Steam relay/routing behavior, and use stdio host console guidance from [Host Console](host-console.md) |
| IL2CPP native hosting | Start with `messagingBackend = 'FishNetRpc'` unless you specifically want the Steam relay/routing behavior from `SteamNetworkingSockets` |

## Configuration Areas

Use the focused pages below when you need the full setting list, examples, or troubleshooting for one area.

| Area | What it controls | Start here |
| --- | --- | --- |
| Save path | Which world/save folder the server hosts | [Save Path](configuration/save-path.md) |
| Authentication | Steam ticket validation, query port, loopback bypass, and provider choice | [Authentication](configuration/authentication.md) |
| Client mod verification | Companion-mod pairing, timeout, deny-list behavior, and strict mode | [Client Mod Verification](configuration/client-mod-verification.md) |
| Messaging backend | FishNet vs Steam-backed dedicated-server messaging | [Messaging Backends](configuration/messaging-backends.md) |
| Permissions | Staff groups, bans, grants, denies, and command access | [Permissions](configuration/permissions.md) |
| Auto-save | Timed saves and player-triggered save hooks | [Auto-Save](configuration/autosave.md) |
| Gameplay | Sleep, pause-when-empty, time flow, and fresh-save quest bootstrap | [Gameplay](configuration/gameplay.md) |
| Web panel | Local browser-based operator UI | [Web Panel](configuration/web-panel.md) |
| Hosted/panel console | stdin/stdout operator flow for hosted environments | [Host Console](host-console.md) |

## Operator Surfaces

DedicatedServerMod supports three common operator surfaces:

- TCP console under `[tcpConsole]` for a separate remote admin socket
- stdio host console under `[tcpConsole].stdioConsoleMode` for hosted panels and stdin/stdout process control
- localhost web panel under `[webPanel]` for local or home-hosted administration

Keep the web panel loopback-bound unless you intentionally understand the exposure model. For hosted providers, prefer the stdio host console path described in [Host Console](host-console.md).

## Performance Defaults

The main performance settings live under `[performance]`:

```toml
[performance]
targetFrameRate = 60
vSyncCount = 0
```

`targetFrameRate` in the `30-60` range is the normal dedicated-server target. `vSyncCount` should usually stay at `0`.

## Startup And Deployment Details

This page intentionally stays high level. For startup flags, host sizing, port-forwarding, and command-line override reference, use [Startup and Deployment](configuration/startup-and-deployment.md).

## Notes

- New configs should use `authProvider` instead of the older `requireAuthentication` flag.
- Access control lives in `permissions.toml`, not `server_config.toml`.
- Current builds already force headless time progression to remain positive at runtime, so there is no separate `timeNeverStops` toggle.

## Related Documentation

- [Quick Start](../index.md)
- [Startup and Deployment](configuration/startup-and-deployment.md)
- [Docker Deployment](docker.md)
- [Troubleshooting](troubleshooting.md)
