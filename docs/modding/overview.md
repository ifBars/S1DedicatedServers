---
title: Mod API Overview
---

The S1 Dedicated Server Mod API lets you write server-side and client-side mods with clear lifecycle hooks and safe access to core systems. It's designed around partial classes and conditional compilation so the same codebase can compile for server or client.

Use these guides for concepts and workflows, then switch to the generated API Reference section for exact signatures and XML documentation.

## Architecture

- S1DS core is split by build symbols:
  - SERVER build: `S1DS.Server` is available
  - CLIENT build: `S1DS.Client` is available
  - Shared: `S1DS.IsServer`, `S1DS.IsClient`, `S1DS.BuildConfig`, and `S1DS.Shared.Config`
- Mods implement `IServerMod` and/or `IClientMod` to receive lifecycle callbacks, or inherit the provided base classes.

## Quick start

Implement callbacks in a Melon mod using the side-aware base:

```csharp
using MelonLoader;
using DedicatedServerMod.API;

public sealed class MyMod : SideAwareMelonModBase
{
    public override void OnServerInitialize()
    {
        MelonLogger.Msg($"Server running? {S1DS.Server.IsRunning}, players: {S1DS.Server.PlayerCount}");
    }

    public override void OnClientPlayerReady()
    {
        // Client UI/messaging is ready here
    }
}
```

Alternatively, keep server and client in separate types using `ServerMelonModBase` / `ClientMelonModBase`.

## Registration approaches

### 1. Single mod with auto-discovery

Inherit from any of the `*MelonModBase` classes to implement server and/or client hooks in one class:

```csharp
// Server-only mod
public sealed class ServerOnlyMod : ServerMelonModBase
{
    public override void OnServerInitialize() { }
    public override void OnPlayerConnected(string playerId) { }
}

// Client-only mod  
public sealed class ClientOnlyMod : ClientMelonModBase
{
    public override void OnClientPlayerReady() { }
}

// Both server and client in one class
public sealed class BothSidesMod : SideAwareMelonModBase
{
    // Server hooks
    public override void OnServerInitialize() { }
    public override void OnPlayerConnected(string playerId) { }
    
    // Client hooks  
    public override void OnClientPlayerReady() { }
}
```

**Auto-discovery**: Any `MelonMod` that inherits from `*MelonModBase` or implements `IServerMod`/`IClientMod` is automatically discovered and registered.

### 2. Separate handlers with manual registration

Create separate server/client classes and register them in your main `MelonMod`:

```csharp
public sealed class MyMod : MelonMod
{
    public override void OnInitializeMelon()
    {
#if SERVER
        var serverMod = new MyServerHandler();
        ModManager.RegisterServerMod(serverMod);
#endif
#if CLIENT
        var clientMod = new MyClientHandler();
        ModManager.RegisterClientMod(clientMod);
#endif
    }
}
```

This approach gives you more control over initialization timing and separation of concerns.

## Lifecycle hooks

Lifecycle hooks provide clear timing for init/shutdown and player connect/disconnect events on server, and connection/player-ready events on client.


