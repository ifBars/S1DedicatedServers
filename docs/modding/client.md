---
title: Client API
---

Client mods implement `IClientMod` (or inherit `ClientModBase` / `ClientMelonModBase`) and access client managers via `S1DS.Client`.

## Lifecycle hooks

- `OnClientInitialize()`
- `OnClientShutdown()`
- `OnConnectedToServer()`
- `OnDisconnectedFromServer()`
- `OnClientPlayerReady()`
- `bool OnCustomMessage(string messageType, byte[] data)`

Use `OnClientPlayerReady()` for logic that depends on UI and messaging being initialized.

## Client systems

Available in CLIENT builds via `S1DS.Client`:

- `ClientCore`
- `Connection` (connection status, connect/disconnect)
- `UI` (UI manager)
- `Console`
- `Quests`
- `Loopback`
- `Transport`
- `IsConnected`
- `IsInitialized`

Example:

```csharp
public override void OnClientPlayerReady()
{
    if (!S1DS.Client.IsConnected)
    {
        return;
    }

    // Messaging and UI are safe to use here.
}
```

## Registration

### Auto-discovery

Any `MelonMod` that implements `IClientMod` is automatically discovered and registered. This works with:

- `SideAwareMelonModBase` (both server and client hooks)
- `ClientMelonModBase` (client-only mod with auto-discovery)
- Any class that implements `IClientMod`

### Manual registration

For more control, create separate client handler classes and register them explicitly:

```csharp
// In your main MelonMod.OnInitializeMelon()
#if CLIENT
var clientMod = new MyClientHandler();
ModManager.RegisterClientMod(clientMod);
#endif
```

Registration ensures client message forwarding is wired so `OnCustomMessage` can be triggered.

For exact signatures and XML documentation, use the generated API Reference section alongside this guide.
