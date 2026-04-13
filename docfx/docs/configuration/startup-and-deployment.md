## Startup and Deployment

Use this page when you need the operational details that do not belong in the high-level configuration overview: startup flags, host sizing, operator launch behavior, and port-forwarding.

## Starting The Server

- Use `start_server.bat` from the release package to launch a native server install.
- The server refuses to start if `[storage].saveGamePath` is not set.
- For hosted panels that control the process through stdin/stdout, follow [Host Console](../host-console.md) and prefer `-logFile -` so Unity and MelonLoader logs stay visible to the host.

## Host Sizing

Current baseline observation for a mostly idle server:

- `2` vCPUs at around `2.0 GHz`
- `4 GB` RAM
- `0-1` connected players
- roughly `72%` average CPU usage
- about `3.0 GB` RAM in use

Treat that as a bare-minimum starting point, not a comfortable production target. Add more CPU and memory headroom if you expect:

- multiple concurrent players
- save/load spikes
- heavier mod stacks
- hosted panel overhead
- Wine or Proton overhead
- noisy-neighbor VPS contention

## Command-Line Overrides

All `server_config.toml` settings can be overridden via command-line arguments. These take precedence over the values persisted on disk.

Permission graph data lives in `permissions.toml` and is managed separately.

### Server Startup

| Argument | Description | Example |
|----------|-------------|---------|
| `--dedicated-server` or `--server` | Enable dedicated server mode and auto-start | `--dedicated-server` |
| `--server-name <name>` | Set server name | `--server-name "My Server"` |
| `--max-players <count>` | Set maximum players | `--max-players 16` |
| `--server-password <password>` | Set server password | `--server-password "secret"` |

### Authentication

| Argument | Description | Example |
|----------|-------------|---------|
| `--require-authentication` or `--require-auth` | Require authentication for players | `--require-authentication` |
| `--auth-provider <provider>` | Set auth provider | `--auth-provider SteamGameServer` |
| `--auth-timeout <seconds>` | Authentication timeout in seconds | `--auth-timeout 30` |
| `--steam-gs-anonymous` | Use anonymous Steam server login | `--steam-gs-anonymous` |
| `--steam-gs-token <token>` | Set Steam game server token | `--steam-gs-token "xxxxx"` |

### Client Mod Verification

| Argument | Description | Example |
|----------|-------------|---------|
| `--mod-verification` | Enable client mod verification | `--mod-verification` |
| `--no-mod-verification` | Disable client mod verification | `--no-mod-verification` |
| `--mod-verification-timeout <seconds>` | Set mod verification timeout | `--mod-verification-timeout 30` |
| `--strict-client-mod-mode` | Enable strict client mod mode | `--strict-client-mod-mode` |
| `--allow-unpaired-client-mods <true|false>` | Allow client-only mods without a paired server mod | `--allow-unpaired-client-mods true` |
| `--block-known-risky-client-mods <true|false>` | Enable built-in risky client mod blocking | `--block-known-risky-client-mods true` |

### Networking

| Argument | Description | Example |
|----------|-------------|---------|
| `--messaging-backend <backend>` | Set messaging backend | `--messaging-backend SteamNetworkingSockets` |
| `--steam-networking-sockets-virtual-port <port>` | Set Steam Networking Sockets virtual port | `--steam-networking-sockets-virtual-port 0` |
| `--steam-networking-sockets-max-payload-bytes <bytes>` | Set Steam Networking Sockets max payload size | `--steam-networking-sockets-max-payload-bytes 1200` |
| `--steam-networking-sockets-server-steam-id <steam_id>` | Set Steam Networking Sockets server SteamID hint | `--steam-networking-sockets-server-steam-id 90000000000000000` |

### Host Console

| Argument | Description | Example |
|----------|-------------|---------|
| `--stdio-console` | Always enable stdio host console | `--stdio-console` |
| `--no-stdio-console` | Disable stdio host console | `--no-stdio-console` |
| `--stdio-console-auto` | Enable stdio host console only when stdin is redirected | `--stdio-console-auto` |

### Legacy Permission Bootstrap

These flags remain for compatibility with the older config-based permission flow. Prefer editing `permissions.toml` and using `reloadpermissions` for normal administration.

| Argument | Description | Example |
|----------|-------------|---------|
| `--add-operator <steam_id>` | Seed the built-in `operator` group during legacy migration/bootstrap | `--add-operator 76561198000000001` |
| `--add-admin <steam_id>` | Seed the built-in `administrator` group during legacy migration/bootstrap | `--add-admin 76561198000000001` |

### TCP Console

| Argument | Description | Example |
|----------|-------------|---------|
| `--tcp-console` | Enable TCP console | `--tcp-console` |
| `--tcp-console-port <port>` | Set TCP console port | `--tcp-console-port 38466` |
| `--tcp-console-max-connections <limit>` | Set maximum TCP console connections | `--tcp-console-max-connections 3` |
| `--tcp-console-bind <address>` | Set TCP bind address | `--tcp-console-bind 0.0.0.0` |
| `--tcp-console-password <password>` | Set TCP console password | `--tcp-console-password "adminpass"` |

### Performance

| Argument | Description | Example |
|----------|-------------|---------|
| `--target-framerate <fps>` | Set target framerate | `--target-framerate 60` |
| `--vsync <count>` | Set VSync count | `--vsync 0` |

### Debug

| Argument | Description | Example |
|----------|-------------|---------|
| `--debug` | Enable debug mode | `--debug` |
| `--verbose` | Enable verbose logging | `--verbose` |

Example:

```batch
start_server.bat --dedicated-server --server-name "My Server" --auth-provider SteamGameServer --mod-verification --block-known-risky-client-mods true
```

## Networking (Port Forwarding)

- Forward the value of `serverPort` from your router to the server machine if players are connecting from outside your LAN.
- Forward both TCP and UDP.
- Ensure the OS firewall also allows inbound traffic on that port.

Helpful links:

- [Beginners Guide to Port Forwarding (Tinkernut)](https://www.youtube.com/watch?v=jfSLxs40sIw)
- [Router-specific port forwarding guides (PortForward.com)](https://portforward.com/router.htm)

Some ISPs use CGNAT. If port forwarding does not work, you may need a public IPv4, IPv6, or a hosted server.

## Related Documentation

- [Configuration Overview](../configuration.md)
- [Authentication](authentication.md)
- [Messaging Backends](messaging-backends.md)
- [Permissions](permissions.md)
- [Host Console](../host-console.md)
- [Docker Deployment](../docker.md)
