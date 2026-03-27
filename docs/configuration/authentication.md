## Authentication

DedicatedServerMod can require each remote client to complete a Steam ticket handshake before join flow is finalized. This ensures only authorized Steam users can connect to your server.

`authProvider` is the canonical switch for authentication in current builds. The older `requireAuthentication` flag is still parsed for backward compatibility, but new configs should use `authProvider` directly.

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

```json
{
  "authProvider": "None"
}
```

### SteamGameServer (Recommended)

**When to use:** Public dedicated servers, Docker deployments, and normal production hosting.

**How it works:** The server logs into Steam as a game server and validates client tickets through the game server API.

**Pros:**
- Recommended by Steam for dedicated servers.
- Low-latency validation path.
- Works with anonymous or persistent game server login.
- Best fit for public hosting.

**Cons:**
- Requires Steam backend reachability.
- Requires valid Steam game server setup.

**Configuration (anonymous login):**

```json
{
  "authProvider": "SteamGameServer",
  "authTimeoutSeconds": 15,
  "authAllowLoopbackBypass": true,
  "steamGameServerLogOnAnonymous": true,
  "steamGameServerQueryPort": 27016,
  "steamGameServerVersion": "0.2.1-beta",
  "steamGameServerMode": "Authentication"
}
```

**Configuration (persistent token):**

```json
{
  "authProvider": "SteamGameServer",
  "authTimeoutSeconds": 15,
  "authAllowLoopbackBypass": true,
  "steamGameServerLogOnAnonymous": false,
  "steamGameServerToken": "YOUR_GAME_SERVER_TOKEN_HERE",
  "steamGameServerQueryPort": 27016,
  "steamGameServerVersion": "0.2.1-beta",
  "steamGameServerMode": "Authentication"
}
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

```json
{
  "authProvider": "SteamWebApi",
  "authTimeoutSeconds": 15,
  "authAllowLoopbackBypass": true,
  "steamWebApiKey": "YOUR_STEAM_WEB_API_KEY",
  "steamWebApiIdentity": "DedicatedServerMod"
}
```

Use `SteamGameServer` instead unless you are explicitly testing this incomplete path.

## Configuration Keys

### Core Authentication Settings

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `authProvider` | `string` | `"None"` | Authentication backend: `None`, `SteamWebApi`, `SteamGameServer` |
| `authTimeoutSeconds` | `int` | `15` | Timeout for handshake completion (1-120 seconds) |
| `authAllowLoopbackBypass` | `bool` | `true` | Allow local loopback/ghost host to bypass auth |

### Steam Game Server Settings

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `steamGameServerLogOnAnonymous` | `bool` | `true` | Use anonymous Steam game server login |
| `steamGameServerToken` | `string` | `""` | Game server login token when anonymous login is disabled |
| `steamGameServerQueryPort` | `int` | `27016` | Steam query/listing port |
| `steamGameServerVersion` | `string` | `"0.2.1-beta"` | Version string announced to Steam |
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

Players in `bannedPlayers` are rejected during authentication, even if their Steam tickets are otherwise valid.

```json
{
  "bannedPlayers": [
    "76561198012345678",
    "76561198087654321"
  ]
}
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
ScheduleI.exe --require-authentication --auth-provider steam_game_server --auth-timeout 30
```

The `--require-authentication` flag is a convenience alias. New persisted configs should still prefer `authProvider`.

## Troubleshooting

### Authentication always times out

**Symptoms:** Clients cannot finish connecting and fail after the timeout window.

**Checks:**
1. Confirm Steam is running on client machines.
2. Verify the server can reach Steam backend services.
3. Ensure `steamGameServerQueryPort` is not blocked.
4. Increase `authTimeoutSeconds` slightly if your environment is slow.
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

### Private servers

1. Use `authProvider: "None"` only when the trust model is acceptable.
2. Consider keeping Steam auth enabled anyway for accountability.
3. Use `operators` and `admins` as a coarse whitelist if needed.

### Docker or cloud deployments

1. Prefer `SteamGameServer`.
2. Ensure the container or host can reach Steam backend services.
3. Expose `steamGameServerQueryPort` correctly.
4. Keep tokens out of version control.

## Security Considerations

- Never commit Steam API keys or game server tokens.
- Store sensitive values in environment variables or secure local configuration.
- Keep `authAllowLoopbackBypass` enabled unless you fully understand the consequences.
- Maintain the `bannedPlayers` list in SteamID64 format.

## Related Documentation

- [Permissions System](permissions.md) - Operators, admins, and command permissions
- [Server Commands](../commands.md) - Admin and player commands
- [Networking](../troubleshooting/networking.md) - Connection troubleshooting
