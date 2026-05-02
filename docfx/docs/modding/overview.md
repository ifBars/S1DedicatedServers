---
title: Mod API Overview
---

The S1 Dedicated Server Mod API lets you write server-side and client-side mods with clear lifecycle hooks and safe access to core systems. It is designed around partial classes and conditional compilation so side-specific addons can compile cleanly for server or client.

Use these guides for workflows and architecture, then use the generated API reference for exact signatures and XML documentation.

## Architecture

- `S1DS.Server` is available in `SERVER` builds
- `S1DS.Client` is available in `CLIENT` builds
- shared access lives under `S1DS.IsServer`, `S1DS.IsClient`, `S1DS.BuildConfiguration`, and `S1DS.Shared.Config`
- server mods should prefer `ServerModBase` / `ServerMelonModBase`; direct `IServerMod` implementation is a compatibility path because it includes obsolete string-based callbacks
- client mods can implement `IClientMod` directly or inherit the provided base classes
- specialized helper types live under `DedicatedServerMod.API.Client`, `DedicatedServerMod.API.Server`, `DedicatedServerMod.API.Metadata`, `DedicatedServerMod.API.Configuration`, and `DedicatedServerMod.API.Toml`
- optional lifecycle and message hooks should prefer `ModManager` events over adding more mod-facing interfaces

`S1DS.Shared.Config` is side-aware:

- on `SERVER`, it is the authoritative persistent server configuration
- on `CLIENT`, it is a local in-memory config object used by shared runtime systems

Client mods should not treat `S1DS.Shared.Config` as a live snapshot of the remote server's full config file.

## Quick Start

```csharp
using DedicatedServerMod.API;
using DedicatedServerMod.Server.Player;

public sealed class MyServerMod : ServerMelonModBase
{
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
        MelonLogger.Msg($"Player joined: {player.DisplayName} ({player.TrustedUniqueId})");
    }
}
```

```csharp
using DedicatedServerMod.API;

public sealed class MyClientMod : ClientMelonModBase
{
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
        // Client UI and messaging are ready here.
    }
}
```

## Registration Approaches

### Auto-discovery

Any `MelonMod` that inherits from the provided `*MelonModBase` types is discovered automatically. Direct `IClientMod` implementations are also discovered automatically, and direct `IServerMod` implementations remain discovered for compatibility with legacy mods.

### Manual registration

If you want a thin bootstrap mod with plain handler objects, register them manually:

```csharp
internal sealed class MyServerHandler : ServerModBase
{
    public override void OnServerInitialize() { }
}

internal sealed class MyClientHandler : ClientModBase
{
    public override void OnClientInitialize() { }
}

public sealed class MyBootstrapMod : MelonMod
{
    public override void OnInitializeMelon()
    {
#if SERVER
        ModManager.RegisterServerMod(new MyServerHandler());
#endif
#if CLIENT
        ModManager.RegisterClientMod(new MyClientHandler());
#endif
    }
}
```

Use one registration style per runtime object. Do not manually register a `MelonMod` that is already auto-discoverable.

## Lifecycle Hooks

Lifecycle hooks give you clear timing for:

- server init/start/shutdown
- player connect/disconnect with `ConnectedPlayerInfo`
- save/load notifications
- client init/shutdown
- server connect/disconnect
- local player readiness

Legacy string-based `playerId` and `senderId` callbacks remain for compatibility, but they are obsolete compatibility hooks now. On the server side, those string identifiers resolve to trusted unique ID first, then tracked SteamID64, then FishNet client ID. New code should prefer `ModManager.ServerPlayerConnected`, `ModManager.ServerPlayerDisconnected`, and `ModManager.ServerCustomMessageReceived`, or the typed `ConnectedPlayerInfo` overrides on `ServerModBase` / `ServerMelonModBase`. On the client side, new optional hooks should prefer `ModManager.ClientInitializing`, `ModManager.ClientConnectedToServer`, `ModManager.ClientPlayerReady`, and `ModManager.ClientCustomMessageReceived`. The base-class overrides remain available when a single mod class owns the entire lifecycle.

## Configuration

Addon configuration now uses the same TOML platform as the core server.

- use the typed schema/store API for normal addon settings
- use the low-level document API for dynamic table-driven formats
- keep addon config files under the standard `UserData/DedicatedServerMod/Mods/<modId>/` path

See [Addon Configuration](configuration.md) for the full reusable TOML workflow.

## Community Addons

Third-party community addons can build directly on the S1DS API, and some existing Schedule I mods may also work in a dedicated server environment when their client/server assumptions line up. For discovery links and compatibility guidance, see [Community Addons](community-addons.md).

## Companion Mods And Join Verification

DedicatedServerMod can automatically verify paired server/client mods during join.

The normal workflow is:

- server assembly declares `S1DSClientCompanionAttribute`
- client assembly declares `S1DSClientModIdentityAttribute`
- the server checks `modId` plus minimum version compatibility

This avoids making normal servers maintain manual SHA-256 allowlists for every client build while still preserving a strict-mode path for hardened operators.

See [Companion Mods and Verification Metadata](companion-mods.md) for the full attribute workflow.
