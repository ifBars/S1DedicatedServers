---
title: Server API
---

Server mods implement `IServerMod` or inherit `ServerModBase` / `ServerMelonModBase`. Use `S1DS.Server` for access to server systems.

## Lifecycle Hooks

- `OnServerInitialize()`
- `OnServerStarted()`
- `OnServerShutdown()`
- `OnPlayerConnected(string playerId)`
- `OnPlayerDisconnected(string playerId)`
- `OnBeforeSave()`
- `OnAfterSave()`
- `OnBeforeLoad()`
- `OnAfterLoad()`
- `OnCustomMessage(string messageType, byte[] data, string senderId)`

Keep connect/disconnect hooks light and move heavy work elsewhere when possible.

## Server Systems

Available in `SERVER` builds via `S1DS.Server`:

- `Players`
- `Network`
- `GameSystems`
- `Persistence`
- `IsRunning`
- `PlayerCount`

Example:

```csharp
public override void OnServerStarted()
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
using DedicatedServerMod.API;

[assembly: S1DSClientCompanion(
    modId: "ghost.marketterminal",
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
