---
title: Companion Mods and Verification Metadata
---

DedicatedServerMod supports paired server/client mods through assembly metadata instead of making server owners maintain ordinary allowlists by hand.

This page covers:

- `S1DSClientCompanionAttribute` for server-side mod assemblies
- `S1DSClientModIdentityAttribute` for client-side mod assemblies

## Why This Exists

Without metadata, a server mostly sees anonymous client DLLs and hashes. With metadata:

- server mods can declare which client companion they expect
- client mods can declare a stable ID and version
- the server can verify compatibility automatically

The normal path is:

- match by `modId`
- enforce `minVersion`
- use hashes only for deny lists and optional strict mode

## Server-Side Attribute

Apply `S1DSClientCompanionAttribute` at the assembly level in the server mod project:

```csharp
using DedicatedServerMod.API;

[assembly: S1DSClientCompanion(
    modId: "ghost.mycoolmod",
    displayName: "My Cool Mod",
    Required = true,
    MinVersion = "1.2.0")]
```

Fields:

- `modId`: stable identifier shared with the client companion
- `displayName`: user-facing name shown in verification messages
- `Required`: whether the client companion must be installed to join
- `MinVersion`: minimum compatible client version
- `PinnedSha256`: optional strict-mode hash pins

### When To Use `PinnedSha256`

Usually, do not.

For normal compatibility-first servers, `modId` plus `MinVersion` is the intended workflow. `PinnedSha256` is only for servers that explicitly enable `strictClientModMode`.

## Client-Side Attribute

Apply `S1DSClientModIdentityAttribute` at the assembly level in the client mod project:

```csharp
using DedicatedServerMod.API;

[assembly: S1DSClientModIdentity("ghost.mycoolmod", "1.2.3")]
```

Fields:

- `modId`: stable identifier for the mod
- `version`: version string reported during join verification

The `modId` must match the server companion declaration.

## Full Paired Example

Server assembly:

```csharp
using DedicatedServerMod.API;

[assembly: S1DSClientCompanion(
    modId: "ghost.marketterminal",
    displayName: "Market Terminal",
    Required = true,
    MinVersion = "2.0.0")]
```

Client assembly:

```csharp
using DedicatedServerMod.API;

[assembly: S1DSClientModIdentity("ghost.marketterminal", "2.1.0")]
```

Result:

- the server requires the client companion to be present
- the client can join if its companion version is `2.0.0` or newer
- no per-build hash maintenance is required unless the server enables strict mode

## Optional Companion Example

If a server-side feature can work without the client mod but improves UX when present:

```csharp
[assembly: S1DSClientCompanion(
    modId: "ghost.mapmarkers",
    displayName: "Map Markers",
    Required = false,
    MinVersion = "1.0.0")]
```

This means:

- clients may join without the companion
- if they do load it, it still must be compatible

## Client-Only Mods

A mod with no server component can still declare `S1DSClientModIdentityAttribute`.

That is recommended because it gives server owners a stable mod ID they can:

- deny in `client_mod_policy.toml`
- approve in strict mode
- recognize in logs and disconnect reasons

Example:

```csharp
[assembly: S1DSClientModIdentity("ghost.visualtrees", "1.0.0")]
```

## Guidance For Mod Authors

- keep `modId` stable across releases
- use a clear, author-prefixed identifier
- bump `version` on every client release
- prefer `MinVersion` over hash pins for normal compatibility
- only ship `PinnedSha256` if you intentionally support strict-mode operators

## What Server Owners See

When your mod uses these attributes correctly:

- required companions are enforced automatically
- optional companions are recognized automatically
- strict-mode operators can still add hash pins if they want exact binary control

For the server-owner side, see [Client Mod Verification](../configuration/client-mod-verification.md).
