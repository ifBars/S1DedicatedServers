---
title: Client API
---

Client mods implement `IClientMod` or inherit `ClientModBase` / `ClientMelonModBase`. Use `S1DS.Client` for access to dedicated-client systems.

## Lifecycle Hooks

- `OnClientInitialize()`
- `OnClientShutdown()`
- `OnConnectedToServer()`
- `OnDisconnectedFromServer()`
- `OnClientPlayerReady()`
- `ModManager.ClientInitializing`
- `ModManager.ClientConnectedToServer`
- `ModManager.ClientDisconnectedFromServer`
- `ModManager.ClientPlayerReady`
- `ModManager.ClientCustomMessageReceived`

Use `OnClientPlayerReady()` for logic that depends on UI and messaging already being initialized.

Prefer the `ModManager` events above for optional client hooks. Keep `IClientMod` and the base classes for coarse lifecycle participation, registration boundaries, and auto-discovery.

## Client Systems

Available in `CLIENT` builds via `S1DS.Client`:

- `ClientCore`
- `Connection`
- `UI`
- `Console`
- `Avatars`
- `Quests`
- `IsConnected`
- `IsInitialized`

Example:

```csharp
public override void OnClientInitialize()
{
    ModManager.ClientPlayerReady += HandleClientReady;
}

public override void OnClientShutdown()
{
    ModManager.ClientPlayerReady -= HandleClientReady;
}

private void HandleClientReady()
{
    // Messaging and UI are safe to use here.
}
```

## Registration

### Auto-discovery

Any `MelonMod` that implements `IClientMod` is discovered automatically. This includes:

- `ClientMelonModBase`
- direct `IClientMod` implementations

### Manual registration

```csharp
internal sealed class MyClientHandler : ClientModBase
{
    public override void OnClientInitialize()
    {
    }
}

public sealed class MyMod : MelonMod
{
    public override void OnInitializeMelon()
    {
#if CLIENT
        ModManager.RegisterClientMod(new MyClientHandler());
#endif
    }
}
```

Registration ensures client message forwarding is wired so `OnCustomMessage` and `ModManager.ClientCustomMessageReceived` can be delivered.

Do not manually register a `MelonMod` that already implements `IClientMod` or inherits `ClientMelonModBase`.

## Declaring Client Mod Identity

If your client mod may be checked by a dedicated server, declare its stable identity at the assembly level:

```csharp
using DedicatedServerMod.API.Metadata;

[assembly: S1DSClientModIdentity("bars.marketterminal", "2.1.0")]
```

This is strongly recommended for:

- client companions paired with a server mod
- client-only mods that server owners may want to identify by stable mod ID

When the mod is paired, the `modId` should match the server-side `S1DSClientCompanionAttribute`.

See [Companion Mods and Verification Metadata](companion-mods.md) for the full paired-mod workflow and strict-mode notes.

For exact signatures and XML documentation, use the generated API reference alongside this guide.
