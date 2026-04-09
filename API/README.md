# S1DS Mod API

A simple, streamlined API for developing mods for the Schedule One Dedicated Server system. This API provides conditional access to server and client functionality while respecting the build configuration constraints.

## Architecture

The API uses **partial classes** with **conditional compilation** to provide typed access to the appropriate namespaces based on build configuration:

- **S1DS.cs** - Shared base class with side detection and logging
- **S1DS.Server.cs** - Server-only functionality (compiled only when `SERVER` is defined)
- **S1DS.Client.cs** - Client-only functionality (compiled only when `CLIENT` is defined)

## Quick Start

### 1. Create a Mod

```csharp
using DedicatedServerMod.API;

public sealed class MyServerMod : ServerMelonModBase
{
    public override void OnServerInitialize()
    {
        S1DS.Log($"Server mod loaded. Players: {S1DS.Server.PlayerCount}");
    }
}
```

```csharp
using DedicatedServerMod.API;

public sealed class MyClientMod : ClientMelonModBase
{
    public override void OnClientPlayerReady()
    {
        S1DS.Log($"Client mod loaded. Connected: {S1DS.Client.IsConnected}");
    }
}
```

### 2. Access System Managers

```csharp
// Check which side you're on
if (S1DS.IsServer)
{
    // Server managers are valid after OnServerInitialize
    var playerCount = S1DS.Server.PlayerCount;
    var persistence = S1DS.Server.Persistence;
    var network = S1DS.Server.Network;
}

if (S1DS.IsClient)
{
    // Client managers are valid after OnClientInitialize
    var ui = S1DS.Client.UI;
    var connection = S1DS.Client.Connection;
    var avatarService = S1DS.Client.Avatars;
}

// Access shared functionality (available once the mod initializes)
var messaging = S1DS.Shared.Messaging;
var config = S1DS.Shared.Config;
```

## API Reference

### S1DS Core

| Property/Method | Description |
|-----------------|-------------|
| `S1DS.IsServer` | True if running server build |
| `S1DS.IsClient` | True if running client build |
| `S1DS.BuildConfig` | Current build configuration string |
| `S1DS.Log(message, color)` | Log with MelonLoader |
| `S1DS.LogError(message)` | Log error |
| `S1DS.LogWarning(message)` | Log warning |

### S1DS.Server (SERVER builds only)

| Property/Method | Description |
|-----------------|-------------|
| `Bootstrap` | ServerBootstrap instance |
| `Players` | Player session queries and moderation facade. Use `S1DS.Server.Permissions` for permission evaluation. |
| `Network` | NetworkManager instance |
| `GameSystems` | GameSystemManager instance |
| `Persistence` | PersistenceManager instance |
| `StatusQuery` | `ServerStatusQueryService` registration surface for custom TCP status-query handlers |
| `Patches` | GamePatchManager instance |
| `PlayerCount` | Number of connected players |
| `IsRunning` | True if server is running |

### S1DS.Client (CLIENT builds only)

| Property/Method | Description |
|-----------------|-------------|
| `Core` | Client Core instance |
| `Connection` | ClientConnectionManager instance |
| `UI` | ClientUIManager instance |
| `Avatars` | `ClientSteamAvatarService` for Steam avatar lookup/caching |
| `PlayerSetup` | ClientPlayerSetup instance |
| `Time` | ClientTimeManager instance |
| `Console` | ClientConsoleManager instance |
| `Quests` | ClientQuestManager instance |
| `AdminStatus` | AdminStatusManager instance |
| `IsConnected` | True if connected |
| `IsInitialized` | True if initialized |

### Join Preparation Registration

Client mods that need to stage content before the dedicated-server join begins should register a join-preparation pipeline step through `S1DS.Client.Connection`.

```csharp
using System.Collections;
using DedicatedServerMod.API;

public sealed class MyClientMod : ClientMelonModBase
{
    private ClientJoinPreparationRegistration _registration;

    public override void OnClientInitialize()
    {
        _registration = S1DS.Client.Connection.RegisterJoinPreparation(
            "ghost.example-prejoin",
            builder => builder
                .WithPriority(100)
                .WithPrepare(PrepareForJoin)
                .WithFinalize(FinalizePreparedJoin)
                .WithReset(ResetPreparedJoin));
    }

    public override void OnClientShutdown()
    {
        _registration?.Dispose();
    }

    private IEnumerator PrepareForJoin(ClientJoinPreparationContext context)
    {
        yield break;
    }

    private void FinalizePreparedJoin(ClientJoinPreparationContext context)
    {
    }

    private void ResetPreparedJoin(ClientJoinPreparationContext context)
    {
    }
}
```

Use `context.Fail("reason")` from the prepare or finalize callback to abort the join with a user-facing error.

### Status Query Registration

Server mods can extend the lightweight TCP status-query endpoint through `S1DS.Server.StatusQuery`.

```csharp
using DedicatedServerMod.API;

public sealed class MyServerMod : ServerMelonModBase
{
    private ServerStatusQueryRegistration _registration;

    public override void OnServerInitialize()
    {
        _registration = S1DS.Server.StatusQuery.RegisterHandler(
            "ghost.example-status",
            builder => builder
                .WithPriority(100)
                .WithHandler(HandleStatusQuery));
    }

    public override void OnServerShutdown()
    {
        _registration?.Dispose();
    }

    private void HandleStatusQuery(ServerStatusQueryContext context)
    {
        if (context.RequestLine != "EXAMPLE_STATUS")
        {
            return;
        }

        context.Respond("{\"ok\":true}");
    }
}
```

### Steam Avatar Helper

Client mods can resolve Steam avatar textures for connected players through `S1DS.Client.Avatars`.

```csharp
using DedicatedServerMod.API;
using DedicatedServerMod.Shared.Permissions;
using ScheduleOne.PlayerScripts;

public sealed class AvatarExampleMod : ClientMelonModBase
{
    public override void OnClientPlayerReady()
    {
        foreach (var player in Player.PlayerList)
        {
            string steamId = PlayerResolver.GetSteamId(player);
            if (string.IsNullOrWhiteSpace(steamId))
            {
                continue;
            }

            S1DS.Client.Avatars.RequestSteamAvatar(steamId, texture =>
            {
                if (texture != null)
                {
                    LoggerInstance.Msg($"Loaded avatar for {player.PlayerName}");
                }
            });
        }
    }
}
```

`GetSteamAvatar(steamId)` returns a cached texture immediately when available and starts a Steam lookup when it is not. `RequestSteamAvatar(steamId, callback)` is the preferred helper when your mod wants a callback once the image is ready.

### S1DS.Shared (Both builds)

| Property/Method | Description |
|-----------------|-------------|
| `Messaging` | CustomMessaging instance |
| `Config` | ServerConfig instance |
| `IsMessagingAvailable` | True if messaging system available |
| `IsConfigLoaded` | True if config loaded |

## Mod Interfaces

### IServerMod
Server mod interface with essential lifecycle events.

### IClientMod  
Client mod interface with essential lifecycle events.

## ModManager

The `ModManager` automatically discovers `MelonMod` classes that implement the interfaces and handles lifecycle events. You can also register plain handler objects manually:

```csharp
ModManager.RegisterServerMod(myServerMod);
ModManager.RegisterClientMod(myClientMod);
```

Use one pattern per runtime object:

- Auto-discovery for `MelonMod`, `ServerMelonModBase`, `ClientMelonModBase`, or direct `IServerMod` / `IClientMod` implementations
- Manual registration for `ServerModBase`, `ClientModBase`, or other non-Melon handler instances

Do not manually register a `MelonMod` that is already auto-discoverable, or the mod can receive duplicate lifecycle callbacks.

## Companion Mod Metadata

DedicatedServerMod also supports join-time verification for paired server/client mods.

Normal usage:

- server assembly declares `S1DSClientCompanionAttribute`
- client assembly declares `S1DSClientModIdentityAttribute`
- the server checks `modId` plus minimum version compatibility during join

Example:

```csharp
[assembly: S1DSClientCompanion(
    modId: "ghost.marketterminal",
    displayName: "Market Terminal",
    Required = true,
    MinVersion = "2.0.0")]

[assembly: S1DSClientModIdentity("ghost.marketterminal", "2.1.0")]
```

`PinnedSha256` exists for strict-mode servers, but the normal workflow is `modId` plus version compatibility, not per-build hash maintenance.

## Build Configuration Compatibility

The API respects the side-specific build/distribution model used by DedicatedServerMod:

- **Mono_Server**: Only server classes available
- **Mono_Client**: Only client classes available
- **Il2cpp_Server**: Only server classes available
- **Il2cpp_Client**: Only client classes available

This keeps addon assemblies side-aware at compile time and avoids referencing unavailable namespaces from the wrong runtime.

## Example: Per-Player Organizations Mod

```csharp
public class PlayerOrganizationsMod : MelonMod, IServerMod
{
    private Dictionary<string, PlayerOrganization> _playerOrgs = new();

    public void OnServerInitialize()
    {
        S1DS.Log("Player Organizations mod initializing...");
        
        LoadPlayerOrganizations();
    }

    public void OnPlayerConnected(string playerId)
    {
        if (!_playerOrgs.ContainsKey(playerId))
        {
            // Send UI prompt for organization creation
            S1DS.Shared.Messaging.SendToClient(playerId, "CreateOrgPrompt", null);
        }
    }

    public bool OnCustomMessage(string messageType, byte[] data, string senderId)
    {
        if (messageType == "CreateOrganization")
        {
            var orgName = System.Text.Encoding.UTF8.GetString(data);
            CreatePlayerOrganization(senderId, orgName);
            return true;
        }
        return false;
    }

    private void CreatePlayerOrganization(string playerId, string orgName)
    {
        _playerOrgs[playerId] = new PlayerOrganization 
        { 
            PlayerId = playerId, 
            Name = orgName,
            Balance = 0,
            Properties = new List<string>()
        };
        
        S1DS.Log($"Created organization '{orgName}' for player {playerId}");
    }
}
```

This API provides a clean, type-safe way to access dedicated server functionality while working within the constraints of your conditional compilation setup.

## Simplified Mod Base Classes

To avoid implementing every interface method, you can inherit from base classes that provide no-op implementations and override only what you need:

- `ServerModBase` or `ServerMelonModBase` for server-side mods
- `ClientModBase` or `ClientMelonModBase` for client-side mods

DedicatedServerMod is distributed as side-specific server/client assemblies, so addons should ship side-specific mod DLLs as well.
