## Startup and Deployment

Use this page when you need the operational details that do not belong in the high-level configuration overview: startup flags, host sizing, operator launch behavior, and port-forwarding.

## Starting The Server

- Use `start_server.bat` from the release package to launch a native server install. It starts the game with `--stdio-console` so the MelonLoader console accepts DedicatedServerMod commands directly, and it forwards any extra arguments to `Schedule I.exe`.
- For native Windows installs, `steam_appid.txt` must exist beside `Schedule I.exe` before first launch. The packaged `start_server.bat` creates it automatically if it is missing. If you launch the executable another way, create it manually and put only `3164500` inside it. Without this file, Steam game server API initialization can fail before anonymous login or `steamGameServerToken` login starts.
- If `[storage].saveGamePath` is empty, the server uses `UserData/DedicatedServerSave` and prepares the folder before loading. Set a custom path only when you want to host a specific Schedule I save folder.
- For hosted panels that control the process through stdin/stdout, follow [Host Console](../host-console.md) and prefer `-logFile -` so Unity and MelonLoader logs stay visible to the host.

## Linux, Wine, and Hosted Panels

Set an installed UTF-8 locale in the environment that launches Wine or Proton, before the game starts. For example, if `C.UTF-8` is available:

```bash
export LANG=C.UTF-8
export LC_ALL=C.UTF-8
```

These are Linux/container environment variables, not `server_config.toml` settings. Set them in the service, container, or panel startup environment; changing an unrelated shell does not change an already-running game process. For the packaged Docker Compose deployment, put both variables in the `.env` file passed to the service. A managed host may need to configure this for you.

[Issue #71](https://github.com/ifBars/S1DedicatedServers/issues/71) reported failed writes to `NPCs/Javier Pérez` without a UTF-8 locale and successful saves after setting these variables. [Issue #78](https://github.com/ifBars/S1DedicatedServers/issues/78) later reported the same path failure on a Kinetic-hosted IL2CPP server; resolution on that service remains unconfirmed. A matching error is a reason to check the launch environment, not proof that every save failure is a locale problem. See [failed saves and recovery](../troubleshooting/save-load.md#saves-fail-or-shutdown-is-cancelled) before restarting a world whose saves are failing.

Retain both the MelonLoader log and Unity player log. If `-logFile -` does not expose Unity errors through Wine or the panel, use a writable Windows path visible to the game instead, for example `-logFile "Z:\home\container\UnityPlayer.log"`. Adapt the path to the host's filesystem mapping and ask the provider for this file if it is not exposed in the panel.

## Verify a New Install or Upgrade

Run these checks on a disposable world or a separate copy of your save before opening the server to players:

1. Confirm the startup log shows the intended S1DS version and Mono or IL2CPP runtime. Use client/server packages from the same release and matching game builds.
2. Wait for `=== DEDICATED SERVER READY (orchestrator) ===`, then connect a client and verify the expected world loaded. A running process or panel status alone does not prove world readiness.
3. Run `save` through the server console. Wait for S1DS's `Save completed: manual_save_by_console` message and check for `Native persistence error` entries. Command acceptance and updated file timestamps are not save-completion evidence.
4. Run `shutdown` and wait for `Save completed: server_shutdown`, then `=== Server Shutdown Complete ===` and process exit. If the final save fails, the server deliberately remains running; investigate the error rather than forcing a restart.
5. Restart and reconnect. Check representative player progression, owned properties/businesses, and placed objects. Keep a separate backup from a confirmed successful save.

## Stop and Restart Safely

Send the S1DS `shutdown` command over the server's stdio or TCP console before stopping a service, recreating a container, or updating game/mod files. Let the final save and process exit finish before relaunching. Saves taking more than 30 seconds emit a warning and continue waiting; do not treat that warning as permission to kill the process.

Configure panel Stop/Restart actions to request `shutdown` and allow the process to exit. Closing stdin, disconnecting a TCP session, or sending the TCP-session `exit`/`quit` commands does not request a server shutdown. Images built from the current Docker source turn SIGTERM/SIGINT into a stdio `shutdown` request and wait for game exit; Docker can still force termination when its external stop timeout expires. Older published images, including 1.1.6, terminate the game directly and require a manual console shutdown before stopping the container. See [Docker shutdown guidance](../docker.md#stopping-and-restarting).

Use a current release. The shutdown ordering defect in [#71](https://github.com/ifBars/S1DedicatedServers/issues/71) was fixed in 1.1.2; the separate autosave cancellation exception in [#74](https://github.com/ifBars/S1DedicatedServers/issues/74) was fixed in 1.1.3. Version 1.1.5 added native write-error diagnostics and rejects partial saves that could previously report success. These fixes do not repair an existing world or establish the cause of every hosted save failure.

## Updating the Game or Mod

1. Complete `shutdown` and wait for process exit before changing installed files. Preserve a known-good backup of the save and configuration.
2. Update/validate the intended Schedule I branch, then reapply the deployment's pinned MelonLoader version. Game validation can overwrite loader files.
3. For IL2CPP, remove only `MelonLoader/Dependencies/Il2CppAssemblyGenerator/Cpp2IL/cpp2il_out` while the game is stopped, before relaunching. Stale Cpp2IL output can survive a game update and break interop generation. Do not delete saves, `Mods`, or the whole `MelonLoader` directory as a routine cache repair. The official Docker entrypoint performs this targeted cleanup automatically.
4. Apply the matching S1DS server release package and update players' client packages. For Docker, pull an exact newer image tag or rebuild from that release's `Docker.zip`; updating game files alone does not replace the image's bundled mod DLLs.
5. Repeat the [save/shutdown/reload verification](#verify-a-new-install-or-upgrade) before reopening the world.

See [Docker interop recovery](../docker.md#il2cpp-interop-cache-cleanup) for details. A MelonLoader downgrade is not the primary fix for stale Cpp2IL output.

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

Client connection overrides are parsed by the client runtime, not the server config parser:

| Argument | Description | Example |
|----------|-------------|---------|
| `--server-ip <address>` | Dedicated server address for client auto-connect flows | `--server-ip 127.0.0.1` |
| `--server-port <port>` | Dedicated server port for client auto-connect flows | `--server-port 38465` |

### Authentication

| Argument | Description | Example |
|----------|-------------|---------|
| `--require-authentication` or `--require-auth` | Require authentication for players | `--require-authentication` |
| `--disable-authentication`, `--disable-auth`, or `--no-auth` | Disable authentication for development or controlled local testing | `--no-auth` |
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
| `--steam-networking-sockets-virtual-port <port>` or `--steam-sockets-virtual-port <port>` | Set Steam Networking Sockets virtual port | `--steam-networking-sockets-virtual-port 0` |
| `--steam-networking-sockets-max-payload-bytes <bytes>` or `--steam-sockets-max-payload-bytes <bytes>` | Set Steam Networking Sockets max payload size | `--steam-networking-sockets-max-payload-bytes 1200` |
| `--steam-networking-sockets-server-steam-id <steam_id>`, `--steam-sockets-server-steam-id <steam_id>`, `--server-steamid <steam_id>`, or `--server-steam-id <steam_id>` | Set Steam Networking Sockets server SteamID hint | `--steam-networking-sockets-server-steam-id 90000000000000000` |

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

You do not need to add `--stdio-console` when using the packaged batch file because it is already included. Add it only when launching `Schedule I.exe` manually.

## Networking (Port Forwarding)

- If players are connecting from outside your LAN, forward every external surface you actually use to the server machine and allow the same ports through the OS firewall.

| Surface | Config key | Default | Protocol | Forward when |
|----------|------------|---------|----------|--------------|
| Game traffic | `serverPort` | `38465` | UDP | Players connect from outside your LAN |
| DedicatedServerMod status query | `serverPort` | `38465` | TCP | You want the in-game add/favorites flow and status query to work from outside your LAN |
| Steam query/listing | `steamGameServerQueryPort` | `27016` | UDP | You use `SteamGameServer` and want Steam query/server-browser visibility |
| TCP console | `tcpConsolePort` | `4050` | TCP | You explicitly enable `[tcpConsole].tcpConsoleEnabled` and want remote admin access |
| Web panel | `webPanelPort` | `4051` | TCP | Do not forward; the built-in panel is loopback-only |

- `serverPort` is not UDP-only. DedicatedServerMod also listens on the same numeric port over TCP for its lightweight status query endpoint.
- If you only forward `serverPort` over UDP, players may still be unable to query status or save the server entry cleanly from outside your LAN.
- Keep `[tcpConsole].tcpConsoleBindAddress = '127.0.0.1'` unless you intentionally want remote socket access. If you leave it loopback-bound, do not forward `tcpConsolePort`.
- The built-in web panel is designed to stay local and only supports loopback bind addresses. Do not port-forward `webPanelPort`; use the TCP console with authentication or a hosted panel/stdin integration for remote administration.

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
