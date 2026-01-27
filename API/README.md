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
using MelonLoader;
using DedicatedServerMod.API;

public class MyMod : MelonMod, IServerMod, IClientMod
{
    public override void OnInitializeMelon()
    {
        S1DS.Log($"MyMod loaded! Build: {S1DS.BuildConfig}");
    }

    // Server-side events
    public void OnServerInitialize() { /* Server startup logic */ }
    public void OnServerShutdown() { /* Server cleanup logic */ }
    public void OnPlayerConnected(string playerId) { /* Player join logic */ }
    public void OnPlayerDisconnected(string playerId) { /* Player leave logic */ }

    // Client-side events  
    public void OnClientInitialize() { /* Client startup logic */ }
    public void OnClientShutdown() { /* Client cleanup logic */ }
    public void OnConnectedToServer() { /* Connected to server logic */ }
    public void OnClientPlayerReady() { /* Player spawned + messaging ready */ }
    public void OnDisconnectedFromServer() { /* Disconnected logic */ }
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
| `Players` | PlayerManager instance |
| `Network` | NetworkManager instance |
| `GameSystems` | GameSystemManager instance |
| `Persistence` | PersistenceManager instance |
| `Patches` | GamePatchManager instance |
| `IsRunning` | True if server is running |
| `PlayerCount` | Number of connected players |
| `IsRunning` | True if server is running |

### S1DS.Client (CLIENT builds only)

| Property/Method | Description |
|-----------------|-------------|
| `Core` | Client Core instance |
| `Connection` | ClientConnectionManager instance |
| `UI` | ClientUIManager instance |
| `PlayerSetup` | ClientPlayerSetup instance |
| `Time` | ClientTimeManager instance |
| `Console` | ClientConsoleManager instance |
| `Quests` | ClientQuestManager instance |
| `AdminStatus` | AdminStatusManager instance |
| `IsConnected` | True if connected to server |
| `IsInitialized` | True if client core initialized |
| `IsConnected` | True if connected |
| `IsInitialized` | True if initialized |

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

The `ModManager` automatically discovers mods that implement the interfaces and handles lifecycle events. You can also register mods manually:

```csharp
ModManager.RegisterServerMod(myServerMod);
ModManager.RegisterClientMod(myClientMod);
```

## Build Configuration Compatibility

The API respects your csproj's conditional compilation:

- **Mono_Server**: Only server classes available
- **Mono_Client**: Only client classes available  
- **Combined builds**: Both available (if you add such a config)

This ensures compile-time safety and prevents referencing unavailable namespaces.

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
- `SideAwareMelonModBase` if you want both server and client hooks in one class

These bases still satisfy `IServerMod`/`IClientMod`, so discovery and lifecycle management continue to work with no other changes.