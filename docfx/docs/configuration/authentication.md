## Authentication

DedicatedServerMod can require each remote client to complete a Steam ticket handshake before join flow is finalized. This ensures only authorized Steam users can connect to your server.

`authProvider` is the canonical switch for authentication in current builds. The older `requireAuthentication` flag is still parsed for backward compatibility, but new configs should use `authProvider` directly.

Keep `authTimeoutSeconds` at a minimum of `30` seconds, and prefer `60` seconds for public or modded servers. Slower internet connections, heavier client startup, or lower-end hardware can otherwise cause players to time out before authentication finishes.

## Authentication Providers

The `authProvider` setting determines how Steam tickets are validated. Three options are available.

### None

**When to use:** Private LAN servers, local testing, or fast iteration where Steam identity validation is unnecessary.

**Pros:**
- No external Steam validation dependency.
- Fastest join flow.
- Works for offline or local-only setups.

**Cons:**
- No player identity verification.
- Not suitable for public servers.

**Configuration:**

```toml
[authentication]
authProvider = 'None'
```

### SteamGameServer (Recommended)

**When to use:** Public dedicated servers, Docker deployments, and normal production hosting.

**How it works:** The server logs into Steam as a game server and validates client tickets through the game server API.
DedicatedServerMod now always reports its own build version to Steam automatically; server owners no longer configure that string manually.

**Pros:**
- Recommended by Steam for dedicated servers.
- Low-latency validation path.
- Works with anonymous or persistent game server login.
- Best fit for public hosting.

**Cons:**
- Requires Steam backend reachability.
- Requires valid Steam game server setup.

**Configuration (anonymous login):**

```toml
[authentication]
authProvider = 'SteamGameServer'
authTimeoutSeconds = 60
authAllowLoopbackBypass = true
steamGameServerLogOnAnonymous = true
steamGameServerQueryPort = 27016
steamGameServerMode = 'Authentication'
```

**Configuration (persistent token):**

```toml
[authentication]
authProvider = 'SteamGameServer'
authTimeoutSeconds = 60
authAllowLoopbackBypass = true
steamGameServerLogOnAnonymous = false
steamGameServerToken = 'YOUR_GAME_SERVER_TOKEN_HERE'
steamGameServerQueryPort = 27016
steamGameServerMode = 'Authentication'
```

**Steam Game Server Mode options:**

| Mode | Description | Use Case |
|------|-------------|----------|
| `NoAuthentication` | Do not authenticate users and do not list in the server browser | Private testing only |
| `Authentication` | Authenticate users and list in the server browser | Recommended for most servers |
| `AuthenticationAndSecure` | Authenticate users, list in the server browser, and enable secure mode | Higher-security public servers |

### SteamWebApi

**Status:** Available in configuration, but not fully implemented in the current version.

**When to use:** Not recommended for production today.

**Configuration (future-facing only):**

```toml
[authentication]
authProvider = 'SteamWebApi'
authTimeoutSeconds = 60
authAllowLoopbackBypass = true
steamWebApiKey = 'YOUR_STEAM_WEB_API_KEY'
steamWebApiIdentity = 'DedicatedServerMod'
```

Use `SteamGameServer` instead unless you are explicitly testing this incomplete path.

## Configuration Keys

### Core Authentication Settings

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `authProvider` | `string` | `"SteamGameServer"` | Authentication backend: `None`, `SteamWebApi`, `SteamGameServer` |
| `authTimeoutSeconds` | `int` | `30` | Timeout for handshake completion (1-120 seconds). Keep this at `30` seconds minimum; `60` seconds is recommended so slower clients can finish auth reliably. |
| `authAllowLoopbackBypass` | `bool` | `true` | Allow local loopback/ghost host to bypass auth |

### Steam Game Server Settings

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `steamGameServerLogOnAnonymous` | `bool` | `true` | Use anonymous Steam game server login |
| `steamGameServerToken` | `string` | `""` | Game server login token when anonymous login is disabled |
| `steamGameServerQueryPort` | `int` | `27016` | Steam query/listing port |
| `steamGameServerMode` | `string` | `"Authentication"` | Mode: `NoAuthentication`, `Authentication`, `AuthenticationAndSecure` |

### Steam Web API Settings

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `steamWebApiKey` | `string` | `""` | Web API key for ticket validation |
| `steamWebApiIdentity` | `string` | `"DedicatedServerMod"` | Identity string for Web API flows |

## Runtime Behavior

### Authentication Flow

1. Remote client connects to the server.
2. Server sends an auth challenge with nonce and provider metadata.
3. Client generates a Steam auth ticket.
4. Client submits the ticket with the nonce.
5. Server validates the ticket with the configured provider.
6. If validation fails (invalid ticket, timeout, ban, provider error), the connection is rejected.
7. While not authenticated, server-side command execution is rejected.

### Loopback Bypass

When `authAllowLoopbackBypass` is enabled, the local ghost host connection bypasses authentication. This is required for normal dedicated-server operation. Disabling it can break server startup and headless gameplay flow.

### Ban System

Players with ban entries in `permissions.toml` are rejected during authentication, even if their Steam tickets are otherwise valid.

```toml
[ban.76561198012345678]
subjectId = '76561198012345678'
createdAtUtc = '2026-03-29T12:00:00.0000000Z'
createdBy = 'console'
reason = 'Repeated griefing'
```

## Command-Line Overrides

You can override authentication settings via command-line arguments:

```bash
--require-authentication
--require-auth
--disable-authentication
--disable-auth
--no-auth
--auth-provider <none|steam_web_api|steam_game_server>
--auth-timeout <seconds>
--steam-gs-anonymous
--steam-gs-token <token>
```

**Example:**

```bash
ScheduleI.exe --require-authentication --auth-provider steam_game_server --auth-timeout 60
```

The `--require-authentication` flag is a convenience alias. New persisted configs should still prefer `authProvider`.

## Troubleshooting

- Clients disconnecting shortly after connect are often hitting an auth timeout that is set too low.
- Do not set `authTimeoutSeconds` below `30` seconds unless you are deliberately optimizing a controlled local test environment.
- For public servers, mixed hardware, or heavier mod stacks, use `60` seconds.
- If the failure appears only on IL2CPP, include that runtime detail when filing the GitHub issue so it can be triaged separately from Mono behavior.

### Authentication always times out

**Symptoms:** Clients cannot finish connecting and fail after the timeout window.

**Checks:**
1. Confirm Steam is running on client machines.
2. Verify the server can reach Steam backend services.
3. Ensure `steamGameServerQueryPort` is not blocked.
4. Raise `authTimeoutSeconds` to at least `30` seconds, or `60` seconds for slower environments.
5. Review MelonLoader logs for provider-specific failures.

### Players cannot connect after enabling auth

**Checks:**
1. Ensure clients are using the Steam version of the game.
2. Verify clients have valid Steam sessions.
3. Check that `authProvider` is not set to `None`.
4. Confirm firewall rules are not blocking Steam auth traffic.
5. Temporarily disable auth to isolate whether the problem is auth-specific or network-specific.

### SteamWebApi provider errors

If you see messages about `SteamWebApi` not being implemented, switch `authProvider` back to `"SteamGameServer"` or `"None"`.

## Best Practices

### Public servers

1. Use `authProvider: "SteamGameServer"`.
2. Do not set `authProvider` to `None`.
3. Keep `authAllowLoopbackBypass: true`.
4. Use `steamGameServerMode: "Authentication"` or stricter.
5. Use a persistent token for long-lived production hosting.
6. Keep `authTimeoutSeconds` at `60` seconds unless you have a measured reason to lower it.
7. For Docker or cloud hosting, make sure the container or host can reach Steam backend services, expose `steamGameServerQueryPort` correctly, and keep tokens out of version control.
8. Follow [Docker Deployment](../docker.md) for the release package and container build flow when deploying this way.

### Private servers

1. Use `authProvider: "None"` only when the trust model is acceptable.
2. Consider keeping Steam auth enabled anyway for accountability.
3. Use `permissions.toml` groups, direct user rules, and bans if you need a coarse whitelist or staff-only environment.

## Security Considerations

- Never commit Steam API keys or game server tokens.
- Store sensitive values in environment variables or secure local configuration.
- Keep `authAllowLoopbackBypass` enabled unless you fully understand the consequences.
- Maintain ban entries in `permissions.toml` using SteamID64 subject IDs.

## Related Documentation

- [Permissions System](permissions.md) - Groups, bans, and command permissions
- [Server Commands](../commands.md) - Admin and player commands
- [Networking](../troubleshooting/networking.md) - Connection troubleshooting
