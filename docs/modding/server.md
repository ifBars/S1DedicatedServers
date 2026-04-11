---
title: Server API
---

Server mods implement `IServerMod` or inherit `ServerModBase` / `ServerMelonModBase`. Use `S1DS.Server` for access to server systems.

## Lifecycle Hooks

- `OnServerInitialize()`
- `OnServerStarted()`
- `OnServerShutdown()`
- `OnBeforeSave()`
- `OnAfterSave()`
- `OnBeforeLoad()`
- `OnAfterLoad()`
- `ModManager.ServerPlayerConnected`
- `ModManager.ServerPlayerDisconnected`
- `ModManager.ServerCustomMessageReceived`

Keep connect/disconnect hooks light and move heavy work elsewhere when possible.

Legacy string-based `playerId` and `senderId` callbacks still exist for compatibility, but they are now obsolete compatibility hooks and should not be used for new code. Their identifier resolution order is:

1. trusted unique ID, which normally means the authenticated SteamID64
2. tracked SteamID64
3. FishNet client ID

New code should prefer the typed `ModManager` events above. The `ConnectedPlayerInfo` overloads on `ServerModBase` and `ServerMelonModBase` remain available when an override is cleaner than an event subscription.

## Server Systems

Available in `SERVER` builds via `S1DS.Server`:

- `Players`
- `Network`
- `GameSystems`
- `Persistence`
- `StatusQuery`
- `Permissions`
- `IsRunning`
- `PlayerCount`

`Players` is the main server-side API most addons should build on today. `StatusQuery` and `Permissions` are the intended extension points for status-query registration and authorization lookups. Some other properties currently expose lower-level managers and may narrow into cleaner facades over time.

Example:

```csharp
using DedicatedServerMod.API;
using DedicatedServerMod.Server.Player;

public override void OnServerInitialize()
{
    ModManager.ServerPlayerConnected += HandlePlayerConnected;
}

public override void OnServerShutdown()
{
    ModManager.ServerPlayerConnected -= HandlePlayerConnected;
}

private void HandlePlayerConnected(ConnectedPlayerInfo player)
{
    int total = S1DS.Server.PlayerCount;
}
```

## Registration

### Auto-discovery

Any `MelonMod` that implements `IServerMod` is discovered automatically. This includes:

- `ServerMelonModBase`
- direct `IServerMod` implementations

### Manual registration

```csharp
internal sealed class MyServerHandler : ServerModBase
{
    public override void OnServerInitialize()
    {
    }
}

public sealed class MyMod : MelonMod
{
    public override void OnInitializeMelon()
    {
#if SERVER
        ModManager.RegisterServerMod(new MyServerHandler());
#endif
    }
}
```

If registration happens after the server is already live, `OnServerInitialize()` is invoked immediately.

Do not manually register a `MelonMod` that already implements `IServerMod` or inherits `ServerMelonModBase`.

## Declaring A Client Companion

If your server mod expects or supports a client companion, declare it at the assembly level:

```csharp
using DedicatedServerMod.API.Metadata;

[assembly: S1DSClientCompanion(
    modId: "bars.marketterminal",
    displayName: "Market Terminal",
    Required = true,
    MinVersion = "2.0.0")]
```

Use this when:

- the server mod requires client UI or client-side behavior
- the client companion is optional but should still be validated when present

Guidance:

- keep `modId` stable across releases
- prefer `MinVersion` for normal compatibility
- only populate `PinnedSha256` when you intentionally support strict-mode operators

See [Companion Mods and Verification Metadata](companion-mods.md) for the complete workflow.

## Messaging On Server

Prefer the shared messaging API for client-to-server communication. See [Custom Messaging](messaging.md).
