---
title: Mod API Overview
---

The S1 Dedicated Server Mod API lets you write server-side and client-side mods with clear lifecycle hooks and safe access to core systems. It is designed around partial classes and conditional compilation so the same codebase can compile for server or client.

Use these guides for workflows and architecture, then use the generated API reference for exact signatures and XML documentation.

## Architecture

- `S1DS.Server` is available in `SERVER` builds
- `S1DS.Client` is available in `CLIENT` builds
- shared access lives under `S1DS.IsServer`, `S1DS.IsClient`, `S1DS.BuildConfig`, and `S1DS.Shared.Config`
- mods implement `IServerMod` and/or `IClientMod`, or inherit the provided base classes

## Quick Start

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
        // Client UI and messaging are ready here.
    }
}
```

If you prefer separate types, use `ServerMelonModBase` and `ClientMelonModBase`.

## Registration Approaches

### Auto-discovery

Any `MelonMod` that inherits from the provided `*MelonModBase` types or directly implements `IServerMod` / `IClientMod` is discovered automatically.

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
- player connect/disconnect
- save/load notifications
- client init/shutdown
- server connect/disconnect
- local player readiness

## Companion Mods And Join Verification

DedicatedServerMod can automatically verify paired server/client mods during join.

The normal workflow is:

- server assembly declares `S1DSClientCompanionAttribute`
- client assembly declares `S1DSClientModIdentityAttribute`
- the server checks `modId` plus minimum version compatibility

This avoids making normal servers maintain manual SHA-256 allowlists for every client build while still preserving a strict-mode path for hardened operators.

See [Companion Mods and Verification Metadata](companion-mods.md) for the full attribute workflow.
