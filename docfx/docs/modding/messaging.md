---
title: Custom Messaging API
---

## Overview

Lightweight client/server messaging is provided by `DedicatedServerMod.Shared.Networking.CustomMessaging`. Mods can send arbitrary string payloads and subscribe to receive events without patching networking internals.

> **Note:** This guide covers the `CustomMessaging` API for mod developers. For server configuration and backend selection (`FishNetRpc`, `SteamP2P`, `SteamNetworkingSockets`), see [Messaging Backends Configuration](../configuration/messaging-backends.md).

There are two layers to this system:

1. `CustomMessaging` is the low-level transport API. It sends and receives string payloads.
2. `ModManager` sits on top of that transport and re-emits those same incoming messages through mod-facing events and base-class callbacks.

When the docs say a message is "forwarded", that does **not** mean it is being relayed to another client or sent across an extra network hop. It only means:

- `CustomMessaging` received the message from the transport backend.
- `ModManager` listened to that `CustomMessaging` event.
- `ModManager` invoked `ModManager.*CustomMessageReceived` and the corresponding mod callbacks with the same message data.

In other words, "forwarding" here means "bridging the low-level transport event into the higher-level mod lifecycle API."

## Send helpers

- Server -> client: `CustomMessaging.SendToClient(NetworkConnection, string command, string data = "")`
- Server -> clients: `CustomMessaging.BroadcastToClients(string command, string data = "")`
- Client -> server: `CustomMessaging.SendToServer(string command, string data = "")`
- Client -> server with delivery result: `CustomMessaging.TrySendToServer(string command, string data = "")`
- Server -> client with endpoint queueing: `CustomMessaging.SendToClientOrDeferUntilReady(...)`

Payloads are strings; use JSON if you need structure.

## Receive hooks

- Client side: `CustomMessaging.ClientMessageReceived += (command, data) => { ... }`
- Server side: `CustomMessaging.ServerMessageReceived += (conn, command, data) => { ... }`
- Client mod lifecycle: `ModManager.ClientCustomMessageReceived += (command, payload) => { ... }`
- Server mod lifecycle: `ModManager.ServerCustomMessageReceived += (command, payload, player) => { ... }`
- Endpoint readiness: `CustomMessaging.EndpointReady += () => { ... }`
- Readiness flag: `CustomMessaging.IsEndpointReady`

`CustomMessaging` is the low-level transport surface. `ModManager.ClientCustomMessageReceived` and `ModManager.ServerCustomMessageReceived` are the event-first mod lifecycle hooks layered on top of that transport.

## Which API should a mod use?

In most cases:

- Use `CustomMessaging.SendToServer`, `SendToClient`, or `BroadcastToClients` to send messages.
- Use `ModManager.ClientCustomMessageReceived`, `ModManager.ServerCustomMessageReceived`, or your mod base-class `OnCustomMessage(...)` callback to receive them.

Use `CustomMessaging.ClientMessageReceived` or `CustomMessaging.ServerMessageReceived` directly only when you intentionally want the raw transport events rather than the mod lifecycle layer.

## What `EnsureServerMessageForwarding()` actually does

`EnsureServerMessageForwarding()` does **not** enable the transport itself and does **not** change how messages move over the network.

It only subscribes `ModManager` to `CustomMessaging.ServerMessageReceived` exactly once so that:

- `ModManager.ServerCustomMessageReceived` fires, and
- registered server mods receive `OnCustomMessage(...)`.

Without that wiring, `CustomMessaging` still works, but only code listening directly to `CustomMessaging.ServerMessageReceived` will see incoming client messages.

Under the normal dedicated server startup path, this is already called for you by the server bootstrap. Most server mods do **not** need to call it manually.

You only need to call it yourself if you are hosting or initializing the server messaging stack outside the normal dedicated-server bootstrap and still want `ModManager` server message hooks to run.

Example:

```csharp
// Only needed in custom hosting scenarios outside the normal bootstrap.
ModManager.EnsureServerMessageForwarding();
```

There is no equivalent step most client mods need to perform. Client-side message forwarding into `ModManager` is wired during normal mod registration.

## Example: round-trip

Client sends a request and server replies with JSON:

```csharp
// Client
CustomMessaging.SendToServer("request_org_info", "");
CustomMessaging.ClientMessageReceived += (cmd, data) =>
{
    if (cmd == "org_info")
    {
        var info = JsonConvert.DeserializeObject<OrgInfo>(data);
        // Update UI.
    }
};

// Server
CustomMessaging.ServerMessageReceived += (conn, cmd, data) =>
{
    if (cmd == "request_org_info")
    {
        var payload = JsonConvert.SerializeObject(BuildOrgInfo(conn));
        CustomMessaging.SendToClient(conn, "org_info", payload);
    }
};
```

## Best practices

- Keep command names namespaced, for example `yourmod_feature_action`.
- Validate server-side permissions before acting.
- Avoid large payloads; prefer small, frequent updates.
- Use `OnClientPlayerReady()` before sending from client to ensure messaging is ready.
- Prefer `ModManager` receive hooks for normal mod code, and reserve direct `CustomMessaging.*MessageReceived` subscriptions for lower-level scenarios.
- Use the generated API reference for the full event and property surface on `CustomMessaging`.
