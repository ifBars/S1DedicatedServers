---
title: Server API
---

Server mods implement `IServerMod` (or inherit `ServerModBase` / `ServerMelonModBase`) to receive lifecycle callbacks and access server systems via `S1DS.Server`.

## Lifecycle hooks

- `OnServerInitialize()`
- `OnServerStarted()`
- `OnServerShutdown()`
- `OnPlayerConnected(string playerId)`
- `OnPlayerDisconnected(string playerId)`
- `OnBeforeSave()` / `OnAfterSave()`
- `OnBeforeLoad()` / `OnAfterLoad()`
- `bool OnCustomMessage(string messageType, byte[] data, string senderId)`

Use guard clauses and keep heavy work out of connect/disconnect handlers.

## Server systems

Available in SERVER builds via `S1DS.Server`:

- `Players`: player/session info and events
- `Network`: transport-level helpers
- `GameSystems`: access to game subsystems
- `Persistence`: save/load manager
- `IsRunning`: server initialized state
- `PlayerCount`: number of connected players

Example:

```csharp
public override void OnServerStarted()
{
    var total = S1DS.Server.PlayerCount;
}
```

## Registration

### Auto-discovery
Any `MelonMod` that implements `IServerMod` is automatically discovered and registered. This works with:

- `SideAwareMelonModBase` (both server and client hooks)
- `ServerMelonModBase` (server-only mod with auto-discovery)
- Any class that implements `IServerMod` interface

### Manual registration
For more control, create separate server handler classes and register them explicitly:

```csharp
// In your main MelonMod.OnInitializeMelon()
#if SERVER
var serverMod = new MyServerHandler();
ModManager.RegisterServerMod(serverMod);
#endif
```

If registration happens after the server is live, `OnServerInitialize()` is invoked immediately.

## Messaging on server

Prefer the shared messaging API for client↔server communication. See the Messaging page for details.


