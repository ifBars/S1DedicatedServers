## Messaging Backends

DedicatedServerMod uses a pluggable messaging system for custom server-client communication. The messaging backend determines how custom messages (commands, events, data) are transmitted between the server and clients.

## Quick Recommendations

**Choose the right backend for your setup:**

| Your Setup | Recommended Backend | Why |
|------------|-------------------|-----|
| **Mono build dedicated server** | `FishNetRpc` | Simplest, works out-of-the-box, no extra config |
| **IL2CPP dedicated server** | `SteamNetworkingSockets` | Production-ready, supports dedicated server API |
| **Docker/Cloud deployment** | `SteamNetworkingSockets` | Full dedicated server support, NAT traversal |
| **Game launched through Steam** | `FishNetRpc` or `SteamNetworkingSockets` | Either works; FishNetRpc is simpler |

**⚠️ AVOID: SteamP2P** - Only works when server is launched through Steam client (not dedicated server builds). Use `SteamNetworkingSockets` or `FishNetRpc` instead.

---

## Why Messaging Backends Matter

The base game uses FishNet for player movement and game state synchronization. However, for custom mod features (admin commands, server announcements, player data sync), we need a separate messaging channel. Different backends have different trade-offs for compatibility, performance, and ease of deployment.

## Available Backends

### FishNetRpc (Default)

**When to use:** Most servers, especially Mono builds, IL2CPP builds when FishNet code generation works properly.

**How it works:** Uses FishNet's custom RPC system by registering callbacks on the `DailySummary` NetworkBehaviour. Messages are serialized to JSON and sent through FishNet's reliable channel.

**Pros:**
- ✅ **Default and most tested** backend
- ✅ Works on both Mono and IL2CPP builds
- ✅ No additional Steam API setup required
- ✅ Uses FishNet's existing connection infrastructure
- ✅ Automatic reliability and ordering guarantees
- ✅ No separate port configuration needed
- ✅ Simplest to configure (works out of the box)

**Cons:**
- ⚠️ Depends on FishNet code generation (usually not an issue)
- ⚠️ Tied to FishNet's transport layer
- ⚠️ Requires DailySummary NetworkBehaviour to be spawned

**Configuration:**
```json
{
  "messagingBackend": "FishNetRpc"
}
```

**Best for:**
- First-time server setup
- Standard deployments
- When you want zero extra configuration
- IL2CPP builds with working FishNet codegen

---

### SteamP2P (Legacy - Not Recommended)

**Status:** ⚠️ **Only works when server is launched through Steam** (game + mods + launch arguments), NOT with standalone dedicated server builds.

**When to use:** Rarely recommended. Only for specific scenarios where you're running the full game as a server through Steam client.

**How it works:** Uses Steam's legacy P2P messaging API (`SteamNetworkingMessages`). Messages are routed through Steam's relay network if direct connection isn't possible. Falls back to FishNetRpc during early handshake until peer identity is established.

**Pros:**
- ✅ Can traverse NAT via Steam relay (SDR)
- ✅ No port forwarding required if relay is enabled

**Cons:**
- 🚫 **CRITICAL: Only works when server is launched through Steam client** (not dedicated server builds)
- ⚠️ **Legacy Steam API** (newer code should use SteamNetworkingSockets)
- ⚠️ Not suitable for dedicated server deployments
- ⚠️ Not suitable for Docker/containerized deployments
- ⚠️ Requires Steam to be running on all clients and server
- ⚠️ Requires Steam ID mapping between FishNet connections and Steam peers
- ⚠️ Additional latency when using relay
- ⚠️ More complex setup than FishNetRpc
- ⚠️ Fallback to FishNetRpc during bootstrap adds complexity

> **Recommendation:** Use **SteamNetworkingSockets** for dedicated servers or **FishNetRpc** for Mono builds instead of SteamP2P.

**Configuration:**
```json
{
  "messagingBackend": "SteamP2P",
  "steamP2PAllowRelay": true,
  "steamP2PChannel": 0,
  "steamP2PMaxPayloadBytes": 1200,
  "steamP2PServerSteamId": ""
}
```

**Configuration Keys:**

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `steamP2PAllowRelay` | `bool` | `true` | Allow Steam relay (SDR) for P2P connectivity |
| `steamP2PChannel` | `int` | `0` | Steam P2P channel number for message isolation |
| `steamP2PMaxPayloadBytes` | `int` | `1200` | Maximum message payload size in bytes (256-4096) |
| `steamP2PServerSteamId` | `string` | `""` | Target server Steam ID for client routing (optional, auto-discovered) |

**Best for:**
- ⚠️ **Not recommended for most use cases**
- Only if you're running the game through Steam client as a server (not dedicated server)
- Legacy compatibility scenarios only

**Use instead:**
- ✅ **SteamNetworkingSockets** for dedicated servers
- ✅ **FishNetRpc** for simple Mono builds

---

### SteamNetworkingSockets (Recommended for Production)

**When to use:** Production dedicated servers, Docker deployments, when you need the most robust Steam-based networking.

**How it works:** Uses Steam's modern Networking Sockets API. Server creates a listen socket on a virtual port, clients connect via P2P sockets. Uses game server APIs on dedicated server builds and client APIs on player builds. Falls back to FishNetRpc during early handshake.

**Pros:**
- ✅ **Modern Steam networking API** (recommended by Valve)
- ✅ Supports dedicated server deployments with game server APIs
- ✅ NAT traversal via Steam relay (SDR)
- ✅ Optimized for low-latency, high-reliability messaging
- ✅ Works with Docker and cloud deployments
- ✅ Better performance than legacy P2P API
- ✅ Production-grade reliability

**Cons:**
- ⚠️ Most complex backend to configure
- ⚠️ Requires Steam initialization (client and game server)
- ⚠️ Requires proper Steam ID mapping
- ⚠️ More moving parts = more potential failure points
- ⚠️ Requires Steam to be running on all clients and server
- ⚠️ Fallback to FishNetRpc during bootstrap adds complexity

**Configuration:**
```json
{
  "messagingBackend": "SteamNetworkingSockets",
  "steamP2PChannel": 0,
  "steamP2PMaxPayloadBytes": 1200,
  "steamP2PServerSteamId": ""
}
```

**Configuration Keys:**

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `steamP2PChannel` | `int` | `0` | Virtual port number for Steam sockets (reused from P2P config) |
| `steamP2PMaxPayloadBytes` | `int` | `1200` | Maximum message payload size in bytes (256-4096) |
| `steamP2PServerSteamId` | `string` | `""` | Target server Steam ID for client routing (optional, auto-discovered) |

**Best for:**
- Production dedicated servers
- Docker/containerized deployments
- Servers requiring robust NAT traversal
- When using Steam Game Server authentication
- Professional hosting environments

---

## Comparison Table

| Feature | FishNetRpc | SteamP2P | SteamNetworkingSockets |
|---------|-----------|----------|------------------------|
| **Ease of Setup** | ⭐⭐⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐ |
| **Mono Support** | ✅ Yes | ✅ Yes (Steam launch only) | ✅ Yes |
| **IL2CPP Support** | ✅ Yes | ✅ Yes (Steam launch only) | ✅ Yes |
| **Dedicated Server Support** | ✅ Yes | 🚫 **No** | ✅ **Yes** |
| **NAT Traversal** | Via FishNet transport | ✅ Via Steam relay | ✅ Via Steam relay |
| **Port Forwarding** | Required (FishNet port) | Optional (with relay) | Optional (with relay) |
| **Steam Required** | ❌ No | ✅ Yes (client mode) | ✅ Yes (server API) |
| **Latency** | Low (direct) | Medium (relay) | Low (optimized) |
| **Reliability** | High | Medium | Very High |
| **Production Ready** | ✅ Yes | ❌ **No** | ✅ **Yes** |
| **Configuration Complexity** | Minimal | Moderate | Moderate |
| **Recommendation** | ✅ **Mono builds** | ⚠️ **Avoid** | ✅ **Dedicated servers** |

---

## How to Choose

### Recommended: FishNetRpc for Mono Builds
**Choose FishNetRpc if:**
- ✅ You're running a **Mono build** of the game
- ✅ You want the **simplest setup** (works out-of-the-box)
- ✅ You're setting up a server for the first time
- ✅ You don't need Steam relay features
- ✅ You're okay with port forwarding
- ✅ You want minimal configuration

**Best for:** Most standard dedicated server deployments on Mono builds.

---

### Recommended: SteamNetworkingSockets for IL2CPP/Production
**Choose SteamNetworkingSockets if:**
- ✅ You're running a **dedicated server** (standalone executable)
- ✅ You're using **IL2CPP builds**
- ✅ You're using Docker or cloud hosting
- ✅ You need robust NAT traversal via Steam relay
- ✅ You want production-grade reliability
- ✅ You're using Steam Game Server authentication

**Best for:** Production dedicated servers, especially IL2CPP builds and Docker deployments.

---

### ⚠️ Avoid: SteamP2P (Legacy)
**Do NOT choose SteamP2P unless:**
- You're running the **full game through Steam client** (not dedicated server)
- You have a legacy setup that specifically requires it

**Why avoid:**
- 🚫 Does NOT work with standalone dedicated server builds
- 🚫 Only works when launching game through Steam client with mods
- 🚫 Not suitable for Docker, cloud hosting, or headless deployments
- ⚠️ Legacy API with better alternatives available

**Use instead:**
- ✅ **SteamNetworkingSockets** for dedicated servers
- ✅ **FishNetRpc** for Mono builds

---

## Configuration Examples

### Basic Setup (FishNetRpc - Recommended for Most)

```json
{
  "messagingBackend": "FishNetRpc"
}
```

No additional configuration needed!

---

### Steam P2P with Relay (Not Recommended)

> ⚠️ **Warning:** Only works when server is launched through Steam client. Not compatible with dedicated server builds.

```json
{
  "messagingBackend": "SteamP2P",
  "steamP2PAllowRelay": true,
  "steamP2PChannel": 0,
  "steamP2PMaxPayloadBytes": 1200
}
```

**Use SteamNetworkingSockets or FishNetRpc instead.**

---

### Steam Networking Sockets for Production

```json
{
  "messagingBackend": "SteamNetworkingSockets",
  "steamP2PChannel": 0,
  "steamP2PMaxPayloadBytes": 1200,
  "authProvider": "SteamGameServer"
}
```

Pairs well with Steam Game Server authentication for full Steam integration.

---

## Command-Line Overrides

```bash
# Set messaging backend via CLI
--messaging-backend <fishnet|fishnetrpc|steamp2p|steam_p2p|steamsockets|steamnetworkingsockets>

# Examples
--messaging-backend fishnetrpc
--messaging-backend steam_networking_sockets
```

**Supported aliases:**
- `fishnet`, `fishnetrpc`, `fishnet_rpc` → FishNetRpc
- `steamp2p`, `steam_p2p` → SteamP2P
- `steamsockets`, `steam_sockets`, `steamnetworkingsockets`, `steam_networking_sockets` → SteamNetworkingSockets

---

## Bootstrap Fallback Behavior

### Why Fallback Exists

Steam-based backends (SteamP2P and SteamNetworkingSockets) require Steam ID mapping between FishNet connections and Steam peers. During the initial connection handshake, this mapping doesn't exist yet.

### How It Works

1. Client connects via FishNet
2. FishNet connection established (client gets FishNet ClientId)
3. **Messaging uses FishNetRpc fallback** until Steam ID mapping is available
4. Server sends auth challenge via fallback
5. Client responds with Steam ticket
6. Steam ID mapping established
7. **Messaging switches to configured Steam backend**

### What This Means

- Early messages (auth handshake, initial sync) use FishNetRpc
- Later messages use the configured Steam backend
- This is automatic and transparent
- You'll see "fallback active" log messages until mapping is complete

---

## Troubleshooting

### "Backend not initialized" Errors

**Symptoms:** Messages fail to send, "backend not initialized" in logs

**Solutions:**
1. Check that messaging backend is valid in `server_config.json`
2. For Steam backends, verify Steam is running and initialized
3. Check MelonLoader logs for initialization errors
4. Try switching to FishNetRpc temporarily to isolate the issue

### "DailySummary not spawned" Errors

**Symptoms:** FishNetRpc messages fail, "DailySummary not spawned" in logs

**Solutions:**
1. Wait for game to fully initialize before sending messages
2. Ensure FishNet networking is active
3. Messages sent too early may fail - wait for player ready state

### Steam ID Mapping Failures

**Symptoms:** Steam backends log "no NetworkConnection mapping for SteamID"

**Solutions:**
1. Ensure authentication is working properly
2. Check that Steam is running on both client and server
3. Review auth logs for ticket validation success
4. Verify FishNet connection is established before Steam messaging

### High Latency with Steam Backends

**Symptoms:** Messages arrive slowly

**Solutions:**
1. Check if Steam relay is being used (adds latency)
2. Try disabling relay: `"steamP2PAllowRelay": false`
3. Ensure direct connection is possible (port forwarding, firewall)
4. Consider using FishNetRpc if latency is critical

### Messages Not Arriving

**Symptoms:** Messages sent but never received

**Solutions:**
1. Check `steamP2PMaxPayloadBytes` - ensure messages aren't too large
2. Verify backend is initialized on both sides
3. Check firewall isn't blocking Steam networking
4. Review MelonLoader logs for send/receive errors
5. Try increasing payload limit if messages are large

---

## Best Practices

### For Development

1. Start with `FishNetRpc` - simplest and most reliable
2. Test messaging functionality before adding Steam complexity
3. Enable verbose logging to see message flow
4. **Never use SteamP2P** - it won't work with dedicated server builds

### For Production

1. **Mono builds:** Use `FishNetRpc` (recommended)
2. **IL2CPP/Dedicated servers:** Use `SteamNetworkingSockets` (recommended)
3. **Never use SteamP2P** - only works with Steam client launch
4. Pair Steam backends with Steam authentication
5. Monitor logs for fallback usage (should be minimal after init)

### For Docker/Cloud

1. **Always use SteamNetworkingSockets** (FishNetRpc also works if not using Steam features)
2. **Never use SteamP2P** - incompatible with containerized deployments
3. Ensure Steam can initialize in container
4. Configure relay for NAT traversal
5. Test connectivity before production deployment

### General Tips

1. Don't send large payloads (keep under 1KB when possible)
2. Use JSON for structured data
3. Validate permissions server-side before acting on messages
4. Keep message commands namespaced (e.g., `yourmod_feature_action`)
5. Handle message failures gracefully (retry or fallback)

---

## Performance Considerations

### Message Size

- Keep payloads small (< 1KB recommended)
- Default max is 1200 bytes
- Large messages may be fragmented or dropped
- Consider batching small updates instead of large payloads

### Message Frequency

- Avoid spamming messages (rate limiting recommended)
- Batch updates when possible
- Use events instead of polling
- Server should throttle broadcast frequency

### Backend Overhead

| Backend | CPU Overhead | Memory Overhead | Network Overhead |
|---------|--------------|-----------------|------------------|
| FishNetRpc | Minimal | Minimal | Same as FishNet |
| SteamP2P | Low-Moderate | Low | Steam relay varies |
| SteamNetworkingSockets | Moderate | Moderate | Optimized by Valve |

---

## Security Considerations

### Message Validation

- **Always validate messages server-side**
- Don't trust client-sent data
- Check permissions before executing commands
- Sanitize string inputs

### Command Permissions

Use the permission system to restrict message handling:

```csharp
// Server side
CustomMessaging.ServerMessageReceived += (conn, cmd, data) =>
{
    if (cmd == "admin_command")
    {
        if (!PermissionManager.IsAdmin(conn))
        {
            // Reject unauthorized command
            return;
        }
        // Process admin command
    }
};
```

### Sensitive Data

- **Never send passwords or API keys via messaging**
- Use Steam authentication for identity verification
- Encrypt sensitive data if necessary
- Don't log sensitive message contents

---

## Related Documentation

- [Custom Messaging API](../modding/messaging.md) - How to use messaging in mods
- [Authentication](authentication.md) - Steam authentication configuration
- [Modding Overview](../modding/overview.md) - Mod development guide
- [Networking Troubleshooting](../troubleshooting/networking.md) - Connection issues
