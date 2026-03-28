## Configuration

The server reads settings from `server_config.json` (created on first run under the MelonLoader user data directory).

### Required
- `saveGamePath`: Full path to the save folder to host.
  - Windows: escape backslashes: `C:\\Users\\you\\AppData\\LocalLow\\TVGS\\Schedule I\\Saves\\<SteamID>\\SaveGame_1`
  - Must contain files like `Game.json`, `Metadata.json`, etc.

### Recommended
- `serverPort` (default 38465)
- `serverName`, `serverDescription`
- `maxPlayers`
- `serverPassword` (set only if you want a private server)

### Authentication

Use Steam ticket authentication on public servers to verify player identities:

```json
{
  "authProvider": "SteamGameServer",
  "authTimeoutSeconds": 15,
  "authAllowLoopbackBypass": true,
  "steamGameServerLogOnAnonymous": true,
  "steamGameServerQueryPort": 27016,
  "steamGameServerMode": "Authentication"
}
```

**Quick reference:**
- `authProvider` options: `None`, `SteamGameServer`, `SteamWebApi`
- `SteamGameServer` is **recommended** for public servers
- `None` is acceptable for private LAN servers
- `SteamWebApi` is not fully implemented (use `SteamGameServer` instead)
- `requireAuthentication` is still accepted as a legacy compatibility flag, but new configs should prefer `authProvider`

**📖 See [Authentication Guide](configuration/authentication.md)** for:
- Detailed comparison of all auth providers with pros/cons
- Configuration examples for each provider
- Troubleshooting authentication issues
- Security best practices

### Messaging Backend

Choose how custom server-client messages are transmitted:

```json
{
  "messagingBackend": "FishNetRpc"
}
```

**Recommended backends:**
- `FishNetRpc` (default) - **Recommended for Mono builds** - Simple, works out-of-the-box
- `SteamNetworkingSockets` - **Recommended for IL2CPP/dedicated servers** - Modern Steam sockets, production-ready

**Not recommended:**
- `SteamP2P` - ⚠️ **Only works with Steam client launch** (not dedicated server builds). Use `SteamNetworkingSockets` or `FishNetRpc` instead.

**📖 See [Messaging Backends Guide](configuration/messaging-backends.md)** for:
- Detailed comparison of all backends with pros/cons
- Why SteamP2P should be avoided for dedicated servers
- When to use each backend
- Configuration examples
- Performance and security considerations

### Access control
- `operators`: Full permissions and all commands.
- `admins`: Limited commands defined by `allowedCommands`; denied those in `restrictedCommands`.
- `bannedPlayers`: SteamID64 strings blocked from joining.

### Auto-save
- `autoSaveEnabled`: true/false
- `autoSaveIntervalMinutes`: minutes between periodic saves
- `autoSaveOnPlayerJoin` / `autoSaveOnPlayerLeave`

### Gameplay
- `ignoreGhostHostForSleep`: Ignore loopback client in sleep checks.
- `allowSleeping`: Allow or block bed-based sleep skipping.
- `pauseGameWhenEmpty`: Pause simulation when no real players are connected.
- `timeProgressionMultiplier`: 1.0 is normal speed.

Current builds already force headless time progression to remain positive at runtime, so there is no separate `timeNeverStops` toggle in the active configuration surface.

### TCP Console

Enable remote console access via TCP for server administration:

```json
{
  "tcpConsoleEnabled": false,
  "tcpConsoleBindAddress": "127.0.0.1",
  "tcpConsolePort": 38466,
  "tcpConsoleMaxConnections": 3,
  "tcpConsoleRequirePassword": false,
  "tcpConsolePassword": ""
}
```

- `tcpConsoleBindAddress`: Use `"127.0.0.1"` for local-only access, `"0.0.0.0"` for all interfaces
- `tcpConsoleMaxConnections`: Maximum number of concurrent TCP console clients. Default: 3.
- Enable password protection for remote access
- Bind to localhost (`127.0.0.1`) by default for security

### Performance Settings

Configure server performance parameters:

```json
{
  "targetFrameRate": 60,
  "vSyncCount": 0
}
```

- `targetFrameRate`: Target FPS for server (30-60 recommended for dedicated servers, -1 for unlimited)
- `vSyncCount`: VSync setting (should be 0 for dedicated servers)

### Starting the server
- Use `start_server.bat` from `server.zip` to launch the dedicated server install.

The server will refuse to start if `saveGamePath` is not set.

### Command Line Arguments

All configuration options can be overridden via command line arguments. These take precedence over values in `server_config.json`.

#### Server Startup
| Argument | Description | Example |
|----------|-------------|---------|
| `--dedicated-server` or `--server` | Enable dedicated server mode and auto-start | `--dedicated-server` |
| `--server-name <name>` | Set server name | `--server-name "My Server"` |
| `--max-players <count>` | Set maximum players (1-32) | `--max-players 16` |
| `--server-password <password>` | Set server password | `--server-password "secret"` |

#### Authentication
| Argument | Description | Example |
|----------|-------------|---------|
| `--require-authentication` or `--require-auth` | Require authentication for players | `--require-authentication` |
| `--auth-provider <provider>` | Set auth provider (None, SteamGameServer, SteamWebApi) | `--auth-provider SteamGameServer` |
| `--auth-timeout <seconds>` | Authentication timeout in seconds | `--auth-timeout 30` |
| `--steam-gs-anonymous` | Use anonymous Steam server login | `--steam-gs-anonymous` |
| `--steam-gs-token <token>` | Set Steam game server token | `--steam-gs-token "xxxxx"` |

#### Networking
| Argument | Description | Example |
|----------|-------------|---------|
| `--messaging-backend <backend>` | Set messaging backend (FishNetRpc, SteamNetworkingSockets, SteamP2P) | `--messaging-backend SteamNetworkingSockets` |
| `--steam-p2p-relay <true/false>` | Allow Steam P2P relay | `--steam-p2p-relay true` |
| `--steam-p2p-channel <channel>` | Set Steam P2P channel | `--steam-p2p-channel 0` |
| `--server-steamid <steam_id>` | Set Steam P2P server SteamID | `--server-steamid 90000000000000000` |

#### Permissions
| Argument | Description | Example |
|----------|-------------|---------|
| `--add-operator <steam_id>` | Add operator by SteamID (can be repeated) | `--add-operator 76561198000000001` |
| `--add-admin <steam_id>` | Add admin by SteamID (can be repeated) | `--add-admin 76561198000000001` |

#### TCP Console
| Argument | Description | Example |
|----------|-------------|---------|
| `--tcp-console` | Enable TCP console | `--tcp-console` |
| `--tcp-console-port <port>` | Set TCP console port | `--tcp-console-port 38466` |
| `--tcp-console-max-connections <limit>` | Set maximum number of allowed TCP Console connections | `--tcp-console-max-connections 3`
| `--tcp-console-bind <address>` | Set TCP bind address | `--tcp-console-bind 0.0.0.0` |
| `--tcp-console-password <password>` | Set TCP console password | `--tcp-console-password "adminpass"` |

#### Performance
| Argument | Description | Example |
|----------|-------------|---------|
| `--target-framerate <fps>` | Set target framerate | `--target-framerate 60` |
| `--vsync <count>` | Set VSync count (0-2) | `--vsync 0` |

#### Debug
| Argument | Description | Example |
|----------|-------------|---------|
| `--debug` | Enable debug mode | `--debug` |
| `--verbose` | Enable verbose logging | `--verbose` |

#### Example Usage

```batch
start_server.bat --dedicated-server --server-name "My Server" --max-players 16 --require-authentication --auth-provider SteamGameServer --tcp-console --tcp-console-password "adminpass"
```


### Networking (port forwarding)
- To allow players outside your local network to connect, you must port forward the value of `serverPort` (default `38465`) from your router to the server machine's local IP.
- Protocol: forward both TCP and UDP.
- Ensure your OS firewall allows inbound connections on the forwarded port.
- Players should connect using your public IP and the forwarded port.

Helpful guides:

- [Beginners Guide to Port Forwarding (Tinkernut)](https://www.youtube.com/watch?v=jfSLxs40sIw)
- [Router‑specific port forwarding guides (PortForward.com)](https://portforward.com/router.htm)

Notes:

- Some ISPs use CGNAT; port forwarding may not work. In that case, request a public IPv4, use IPv6, or host on a VPS.

