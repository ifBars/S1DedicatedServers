---
title: Configuration Overview
description: Understand the DedicatedServerMod configuration files and the recommended starting points for common hosting setups.
---

# Configuration Overview

DedicatedServerMod splits configuration by responsibility so operators can tune runtime behavior, permissions, and client-mod policy without editing one oversized file.

The server creates the config files automatically on first run. If you still have a legacy `server_config.json`, the server imports it on startup and writes the current TOML-based files beside it. Legacy permission data is also migrated out of `server_config.toml` into `permissions.toml`.

## Config Files

| File | Purpose | Read this when |
| --- | --- | --- |
| `server_config.toml` | Runtime server settings such as storage, auth, messaging, gameplay, autosave, logging, and operator surfaces | You are bringing up or tuning a server |
| `permissions.toml` | Groups, bans, grants, denies, and command authorization | You are managing staff access or player restrictions |
| `client_mod_policy.toml` | Client mod verification policy overrides | You are tightening or customizing client mod policy |

## Minimum Setup

The shortest path to a working server is:

1. Decide whether to leave `[storage].saveGamePath` empty for the server-managed default save or point it at a specific Schedule I save folder.
2. For native Windows installs, make sure `steam_appid.txt` exists beside `Schedule I.exe` with only `3164500` inside it. The packaged `start_server.bat` creates it if missing, starts with `--stdio-console`, and forwards extra startup arguments; manual launch flows must provide the app ID file themselves.
3. Choose an authentication provider under `[authentication]`.
4. Pick a messaging backend under `[messaging]`.
5. Decide how you want to operate the server: local desktop, hosted panel, TCP console, or localhost web panel.
6. Review autosave and gameplay defaults before opening the server to other players.

Minimal example:

```toml
[server]
maxPlayers = 16

[storage]
saveGamePath = 'C:\Users\you\AppData\LocalLow\TVGS\Schedule I\Saves\<SteamID>\SaveGame_1'

[authentication]
authProvider = 'SteamGameServer'
authTimeoutSeconds = 60
steamGameServerLogOnAnonymous = true
steamGameServerQueryPort = 27016
steamGameServerMode = 'Authentication'

[messaging]
messagingBackend = 'FishNetRpc'
```

DedicatedServerMod does not use the vanilla Steam lobby 4-player cap for direct-IP joins; use `[server].maxPlayers` to control server capacity.

If the resolved save folder is missing core files such as `Game.json`, `Metadata.json`, or `Players`, the server attempts to prepare it from the game's `DefaultSave` template and embedded loopback host data before loading. Starting with a server-managed save is recommended. If you import an existing single-player save, start the server once first and avoid overwriting the server-created `Players\Player_0` data. See [Save Path](configuration/save-path.md#importing-an-existing-single-player-save).

## Recommended Starting Points

| Scenario | Start with |
| --- | --- |
| Local/private Mono testing | `authProvider = 'None'` or `SteamGameServer` if you still want Steam identity checks, plus `messagingBackend = 'FishNetRpc'` |
| Public native server | `authProvider = 'SteamGameServer'`, `authTimeoutSeconds = 60`, and a reviewed `permissions.toml` |
| Docker or hosted panel | `authProvider = 'SteamGameServer'`, choose `FishNetRpc` for the simplest path or `SteamNetworkingSockets` if you want Steam relay/routing behavior, and use stdio host console guidance from [Host Console](host-console.md) |
| IL2CPP native hosting | Start with `messagingBackend = 'FishNetRpc'` unless you specifically want the Steam relay/routing behavior from `SteamNetworkingSockets` |

For third-party hosts and commercial provider guidance, see [Hosting Providers](hosting-providers.md).

## Configuration Areas

Use the focused pages below when you need the full setting list, examples, or troubleshooting for one area.

| Area | What it controls | Start here |
| --- | --- | --- |
| Save path | Which world/save folder the server hosts | [Save Path](configuration/save-path.md) |
| Authentication | Steam ticket validation, query port, and provider choice | [Authentication](configuration/authentication.md) |
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

For public hosting, remember that external players commonly need more than one forwarded port: `serverPort` over UDP for gameplay, `serverPort` over TCP for DedicatedServerMod status query, and `steamGameServerQueryPort` over UDP when using `SteamGameServer` query/listing.

## Performance Defaults

The main performance settings live under `[performance]`:

```toml
[performance]
targetFrameRate = 60
vSyncCount = 0
```

`targetFrameRate` in the `30-60` range is the normal dedicated-server target. `vSyncCount` should usually stay at `0`.

## Startup and Deployment Details

This page intentionally stays high level. For startup flags, host sizing, port-forwarding, and command-line override reference, use [Startup and Deployment](configuration/startup-and-deployment.md).

## Notes

- New configs should use `authProvider` instead of the older `requireAuthentication` flag.
- Access control lives in `permissions.toml`, not `server_config.toml`.
- There is no separate `timeNeverStops` toggle; use the gameplay settings for time and sleep behavior.

## Related Documentation

- [Quick Start](../index.md)
- [Startup and Deployment](configuration/startup-and-deployment.md)
- [Docker Deployment](docker.md)
- [Troubleshooting](troubleshooting.md)
