# S1DS Mod API

DedicatedServerMod exposes a side-aware API for server mods and client mods. The root `DedicatedServerMod.API` namespace stays intentionally small around entry points and lifecycle, while specialized helper types now live in dedicated sub-namespaces.

For new server mods, prefer `ServerModBase` or `ServerMelonModBase`. Direct `IServerMod` implementations remain supported for compatibility, but they still include obsolete string-based player and message callbacks.

## Core Entry Points

`S1DS` is the primary facade:

- `S1DS.Server` in `SERVER` builds
- `S1DS.Client` in `CLIENT` builds
- `S1DS.Shared.Config` in both builds
- `S1DS.IsServer`, `S1DS.IsClient`, and `S1DS.BuildConfiguration` for side detection

## Namespace Layout

Use the root namespace for the main lifecycle and facade types:

- `DedicatedServerMod.API`
  - `S1DS`
  - `IServerMod`
  - `IClientMod`
  - `ServerModBase` / `ServerMelonModBase`
  - `ClientModBase` / `ClientMelonModBase`
  - `ModManager`
  - `S1DSBuildConfiguration`
  - `Version`

Use the focused sub-namespaces for specialized API surfaces:

- `DedicatedServerMod.API.Client`
  - `ClientJoinPreparationBuilder`
  - `ClientJoinPreparationContext`
  - `ClientJoinPreparationRegistration`
  - `ClientSteamAvatarService`
- `DedicatedServerMod.API.Server`
  - `ServerStatusQueryContext`
  - `ServerStatusQueryHandlerBuilder`
  - `ServerStatusQueryRegistration`
- `DedicatedServerMod.API.Metadata`
  - `ClientModMetadata`
  - `S1DSClientCompanionAttribute`
  - `S1DSClientModIdentityAttribute`
- `DedicatedServerMod.API.Configuration`
  - `ModConfigPaths`
  - `TomlConfigStore<TConfig>`
  - `TomlConfigSchema<TConfig>`
  - `TomlConfigLoadResult<TConfig>`
- `DedicatedServerMod.API.Toml`
  - `TomlDocument`
  - `TomlTable`
  - `TomlValue`
  - `TomlParser`

## Quick Start

### Server Mod

```csharp
using DedicatedServerMod.API;
using DedicatedServerMod.Server.Player;

public sealed class MyServerMod : ServerMelonModBase
{
    public override void OnServerInitialize()
    {
        LoggerInstance.Msg($"Server running: {S1DS.Server.IsRunning}");
        ModManager.ServerPlayerConnected += OnServerPlayerConnected;
    }

    public override void OnServerShutdown()
    {
        ModManager.ServerPlayerConnected -= OnServerPlayerConnected;
    }

    private void OnServerPlayerConnected(ConnectedPlayerInfo player)
    {
        LoggerInstance.Msg($"Player joined: {player.DisplayName} ({player.TrustedUniqueId})");
    }
}
```

### Client Mod

```csharp
using DedicatedServerMod.API;

public sealed class MyClientMod : ClientMelonModBase
{
    public override void OnClientInitialize()
    {
        ModManager.ClientPlayerReady += OnClientReady;
    }

    public override void OnClientShutdown()
    {
        ModManager.ClientPlayerReady -= OnClientReady;
    }

    private void OnClientReady()
    {
        if (!S1DS.Client.IsConnected)
        {
            return;
        }

        LoggerInstance.Msg("Dedicated client systems are ready.");
    }
}
```

## Player Lifecycle Migration

Legacy server lifecycle hooks still exist as obsolete compatibility members on the server mod surface:

- `IServerMod.OnPlayerConnected(string playerId)`
- `IServerMod.OnPlayerDisconnected(string playerId)`
- `IServerMod.OnCustomMessage(string messageType, byte[] data, string senderId)`

They now receive a compatibility identifier based on the tracked player:

1. authenticated Steam ID when available
2. tracked Steam ID
3. FishNet client ID

New code should prefer the typed event surface:

- subscribe to `ModManager.ServerPlayerConnected`, `ModManager.ServerPlayerDisconnected`, and `ModManager.ServerCustomMessageReceived`
- or override the `ConnectedPlayerInfo` overloads on `ServerModBase` / `ServerMelonModBase` when you are already deriving from those base classes

This avoids ambiguity around display names, raw client IDs, and partially trusted identity.

## Server Systems

Available through `S1DS.Server` in server builds:

- `Players`
- `Network`
- `GameSystems`
- `Persistence`
- `StatusQuery`
- `Permissions`
- `IsRunning`
- `PlayerCount`

`S1DS.Server.Players` is the main server-side player facade. It exposes tracked `ConnectedPlayerInfo` entries, player lookup helpers, moderation helpers, and join/leave events.

## Client Systems

Available through `S1DS.Client` in client builds:

- `ClientCore`
- `Connection`
- `UI`
- `Console`
- `Avatars`
- `Quests`
- `IsConnected`
- `IsInitialized`

## `S1DS.Shared.Config`

`S1DS.Shared.Config` exposes `ServerConfig.Instance`, but its behavior differs by side:

- on `SERVER`, it is the authoritative persistent config loaded from disk
- on `CLIENT`, it is an in-memory config object used by shared client/runtime systems

Client mods should not assume `S1DS.Shared.Config` is the live server configuration snapshot. Use the client runtime systems and data stores exposed through `S1DS.Client` for server-driven state.

## Client Join Preparation

Client mods can register a dedicated-server join-preparation step through `S1DS.Client.Connection`.

```csharp
using System.Collections;
using DedicatedServerMod.API;
using DedicatedServerMod.API.Client;

public sealed class MyClientMod : ClientMelonModBase
{
    private ClientJoinPreparationRegistration _registration;

    public override void OnClientInitialize()
    {
        _registration = S1DS.Client.Connection.RegisterJoinPreparation(
            "bars.example-prejoin",
            builder => builder
                .WithPriority(100)
                .WithPrepare(PrepareForJoin)
                .WithFinalize(FinalizePreparedJoin)
                .WithReset(ResetPreparedJoin));
    }

    public override void OnClientShutdown()
    {
        _registration?.Dispose();
    }

    private IEnumerator PrepareForJoin(ClientJoinPreparationContext context)
    {
        yield break;
    }

    private void FinalizePreparedJoin(ClientJoinPreparationContext context)
    {
    }

    private void ResetPreparedJoin(ClientJoinPreparationContext context)
    {
    }
}
```

## Status Query Extensions

Server mods can extend the lightweight TCP status-query endpoint through `S1DS.Server.StatusQuery`.

```csharp
using DedicatedServerMod.API;
using DedicatedServerMod.API.Server;

public sealed class MyServerMod : ServerMelonModBase
{
    private ServerStatusQueryRegistration _registration;

    public override void OnServerInitialize()
    {
        _registration = S1DS.Server.StatusQuery.RegisterHandler(
            "bars.example-status",
            builder => builder
                .WithPriority(100)
                .WithHandler(HandleStatusQuery));
    }

    public override void OnServerShutdown()
    {
        _registration?.Dispose();
    }

    private void HandleStatusQuery(ServerStatusQueryContext context)
    {
        if (context.RequestLine == "EXAMPLE_STATUS")
        {
            context.Respond("{\"ok\":true}");
        }
    }
}
```

## Steam Avatar Helper

Client mods can resolve Steam avatar textures through `S1DS.Client.Avatars`.

```csharp
using DedicatedServerMod.API;
using DedicatedServerMod.Shared.Permissions;
using ScheduleOne.PlayerScripts;

public sealed class AvatarExampleMod : ClientMelonModBase
{
    public override void OnClientPlayerReady()
    {
        foreach (Player player in Player.PlayerList)
        {
            string steamId = PlayerResolver.GetSteamId(player);
            if (string.IsNullOrWhiteSpace(steamId))
            {
                continue;
            }

            S1DS.Client.Avatars.RequestSteamAvatar(steamId, texture =>
            {
                if (texture != null)
                {
                    LoggerInstance.Msg($"Loaded avatar for {player.PlayerName}");
                }
            });
        }
    }
}
```

## Companion Mod Metadata

DedicatedServerMod supports paired server/client mods through assembly metadata.

```csharp
using DedicatedServerMod.API.Metadata;

[assembly: S1DSClientCompanion(
    modId: "bars.marketterminal",
    displayName: "Market Terminal",
    Required = true,
    MinVersion = "2.0.0")]

[assembly: S1DSClientModIdentity("bars.marketterminal", "2.1.0")]
```

Use metadata-based version matching as the normal path. `PinnedSha256` is intended for strict-mode operators, not everyday development.

## Loaded Client Mod Metadata

Use `ModManager.ClientModMetadata` when you need descriptive information about the local
client runtime's loaded mods without reaching into MelonLoader types directly.

```csharp
foreach (ClientModMetadata mod in ModManager.ClientModMetadata)
{
    LoggerInstance.Msg($"{mod.DisplayName} v{mod.Version} ({mod.ModId})");
}
```

`ModManager.ClientMods` remains the lifecycle surface for registered `IClientMod` instances.
`ModManager.ClientModMetadata` is the discovery and introspection surface for loaded client mod metadata.

## Registration Rules

`ModManager` auto-discovers:

- `MelonMod` implementations of `IClientMod`
- legacy `MelonMod` implementations of `IServerMod`
- `ServerMelonModBase`
- `ClientMelonModBase`

Manual registration is intended for plain handler objects:

```csharp
ModManager.RegisterServerMod(myServerHandler);
ModManager.RegisterClientMod(myClientHandler);
```

Do not manually register a `MelonMod` that is already auto-discoverable or it can receive duplicate callbacks.

## Build Compatibility

DedicatedServerMod ships side-specific assemblies:

- `Mono_Server`
- `Mono_Client`
- `Il2cpp_Server`
- `Il2cpp_Client`

Server-only helper types are compiled only into server-capable builds. Client-only helper types are compiled only into client-capable builds. Keep your addon DLLs side-aware in the same way.
