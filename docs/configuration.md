## Configuration

The server reads settings from `server_config.json` under the MelonLoader user data directory. The file is created automatically on first run.

### Required

- `saveGamePath`: full path to the save folder you want to host

Windows example:

```json
{
  "saveGamePath": "C:\\Users\\you\\AppData\\LocalLow\\TVGS\\Schedule I\\Saves\\<SteamID>\\SaveGame_1"
}
```

The folder must contain files such as `Game.json` and `Metadata.json`.

### Recommended

- `serverPort`
- `serverName`
- `serverDescription`
- `maxPlayers`
- `serverPassword` for private servers

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

Quick reference:

- `authProvider` options: `None`, `SteamGameServer`, `SteamWebApi`
- `SteamGameServer` is recommended for public servers
- `None` is acceptable for private LAN servers
- `SteamWebApi` is not fully implemented yet
- `requireAuthentication` is still accepted as a legacy compatibility flag, but new configs should prefer `authProvider`

See [Authentication Guide](configuration/authentication.md) for:

- provider comparison
- configuration examples
- troubleshooting
- security guidance

### Client Mod Verification

Client mod verification runs after authentication and before a player is fully admitted to the server.

```json
{
  "modVerificationEnabled": true,
  "modVerificationTimeoutSeconds": 20,
  "blockKnownRiskyClientMods": true,
  "allowUnpairedClientMods": true,
  "strictClientModMode": false,
  "modPolicyPath": ""
}
```

Quick reference:

- `modVerificationEnabled` defaults to `true`
- paired companion mods are discovered automatically from installed server mods
- unpaired client-only mods are allowed by default
- known risky client mods are blocked by default
- `strictClientModMode` is available for hardened/private servers

See [Client Mod Verification](configuration/client-mod-verification.md) for:

- the full config and `client_mod_policy.json` format
- deny-list examples
- strict mode behavior
- command-line overrides

### Messaging Backend

Choose how custom server-client messages are transmitted:

```json
{
  "messagingBackend": "FishNetRpc"
}
```

Recommended:

- `FishNetRpc` for Mono builds
- `SteamNetworkingSockets` for IL2CPP and dedicated server deployments

Avoid:

- `SteamP2P` for dedicated server hosting; it is not the recommended path here

See [Messaging Backends Guide](configuration/messaging-backends.md) for:

- backend comparison
- deployment guidance
- configuration examples
- security and performance notes

### Access Control

- `operators`: full permissions and all commands
- `admins`: limited commands controlled by config
- `bannedPlayers`: SteamID64 strings blocked from joining

### Auto-Save

- `autoSaveEnabled`
- `autoSaveIntervalMinutes`
- `autoSaveOnPlayerJoin`
- `autoSaveOnPlayerLeave`

See [Auto-Save](configuration/autosave.md) for details.

### Gameplay

- `ignoreGhostHostForSleep`
- `allowSleeping`
- `pauseGameWhenEmpty`
- `timeProgressionMultiplier`

Current builds already force headless time progression to remain positive at runtime, so there is no separate `timeNeverStops` toggle.

See [Gameplay](configuration/gameplay.md) for details.

### TCP Console

Enable remote console access via TCP:

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

- Use `127.0.0.1` for local-only access
- Use a password before exposing it remotely
- Keep the listener bound narrowly unless you really need remote administration

### Host Console

Enable panel-friendly stdin/stdout console support:

```json
{
  "stdioConsoleMode": "Auto"
}
```

- `Disabled`: never start the stdio host console transport
- `Auto`: start stdio console only when stdin is redirected
- `Enabled`: always start stdio console
- `Auto` is the default because hosted panels usually inject commands through stdin, while local desktop runs should avoid stealing the interactive console unless you explicitly opt in

See [Host Console](host-console.md) for deployment guidance and runtime behavior.

### Performance

```json
{
  "targetFrameRate": 60,
  "vSyncCount": 0
}
```

- `targetFrameRate`: 30-60 is the normal dedicated-server range
- `vSyncCount`: should usually stay `0`

### Starting The Server

- Use `start_server.bat` from the release package to launch the dedicated server install.
- The server refuses to start if `saveGamePath` is not set.

### Command-Line Arguments

All configuration options can be overridden via command line arguments. These take precedence over values in `server_config.json`.

#### Server Startup

| Argument | Description | Example |
|----------|-------------|---------|
| `--dedicated-server` or `--server` | Enable dedicated server mode and auto-start | `--dedicated-server` |
| `--server-name <name>` | Set server name | `--server-name "My Server"` |
| `--max-players <count>` | Set maximum players | `--max-players 16` |
| `--server-password <password>` | Set server password | `--server-password "secret"` |

#### Authentication

| Argument | Description | Example |
|----------|-------------|---------|
| `--require-authentication` or `--require-auth` | Require authentication for players | `--require-authentication` |
| `--auth-provider <provider>` | Set auth provider | `--auth-provider SteamGameServer` |
| `--auth-timeout <seconds>` | Authentication timeout in seconds | `--auth-timeout 30` |
| `--steam-gs-anonymous` | Use anonymous Steam server login | `--steam-gs-anonymous` |
| `--steam-gs-token <token>` | Set Steam game server token | `--steam-gs-token "xxxxx"` |

#### Client Mod Verification

| Argument | Description | Example |
|----------|-------------|---------|
| `--mod-verification` | Enable client mod verification | `--mod-verification` |
| `--no-mod-verification` | Disable client mod verification | `--no-mod-verification` |
| `--mod-verification-timeout <seconds>` | Set mod verification timeout | `--mod-verification-timeout 30` |
| `--strict-client-mod-mode` | Enable strict client mod mode | `--strict-client-mod-mode` |
| `--allow-unpaired-client-mods <true|false>` | Allow client-only mods without a paired server mod | `--allow-unpaired-client-mods true` |
| `--block-known-risky-client-mods <true|false>` | Enable built-in risky client mod blocking | `--block-known-risky-client-mods true` |
| `--mod-policy-path <path>` | Override the policy file location | `--mod-policy-path "client_mod_policy.json"` |

#### Networking

| Argument | Description | Example |
|----------|-------------|---------|
| `--messaging-backend <backend>` | Set messaging backend | `--messaging-backend SteamNetworkingSockets` |
| `--steam-p2p-relay <true/false>` | Allow Steam P2P relay | `--steam-p2p-relay true` |
| `--steam-p2p-channel <channel>` | Set Steam P2P channel | `--steam-p2p-channel 0` |
| `--server-steamid <steam_id>` | Set Steam P2P server SteamID | `--server-steamid 90000000000000000` |

#### Host Console

| Argument | Description | Example |
|----------|-------------|---------|
| `--stdio-console` | Always enable stdio host console | `--stdio-console` |
| `--no-stdio-console` | Disable stdio host console | `--no-stdio-console` |
| `--stdio-console-auto` | Enable stdio host console only when stdin is redirected | `--stdio-console-auto` |

#### Permissions

| Argument | Description | Example |
|----------|-------------|---------|
| `--add-operator <steam_id>` | Add operator by SteamID | `--add-operator 76561198000000001` |
| `--add-admin <steam_id>` | Add admin by SteamID | `--add-admin 76561198000000001` |

#### TCP Console

| Argument | Description | Example |
|----------|-------------|---------|
| `--tcp-console` | Enable TCP console | `--tcp-console` |
| `--tcp-console-port <port>` | Set TCP console port | `--tcp-console-port 38466` |
| `--tcp-console-max-connections <limit>` | Set maximum TCP console connections | `--tcp-console-max-connections 3` |
| `--tcp-console-bind <address>` | Set TCP bind address | `--tcp-console-bind 0.0.0.0` |
| `--tcp-console-password <password>` | Set TCP console password | `--tcp-console-password "adminpass"` |

#### Performance

| Argument | Description | Example |
|----------|-------------|---------|
| `--target-framerate <fps>` | Set target framerate | `--target-framerate 60` |
| `--vsync <count>` | Set VSync count | `--vsync 0` |

#### Debug

| Argument | Description | Example |
|----------|-------------|---------|
| `--debug` | Enable debug mode | `--debug` |
| `--verbose` | Enable verbose logging | `--verbose` |

Example:

```batch
start_server.bat --dedicated-server --server-name "My Server" --auth-provider SteamGameServer --mod-verification --block-known-risky-client-mods true
```

### Networking (Port Forwarding)

- Forward the value of `serverPort` from your router to the server machine if players are connecting from outside your LAN.
- Forward both TCP and UDP.
- Ensure the OS firewall also allows inbound traffic on that port.

Helpful links:

- [Beginners Guide to Port Forwarding (Tinkernut)](https://www.youtube.com/watch?v=jfSLxs40sIw)
- [Router-specific port forwarding guides (PortForward.com)](https://portforward.com/router.htm)

Some ISPs use CGNAT. If port forwarding does not work, you may need a public IPv4, IPv6, or a hosted server.
