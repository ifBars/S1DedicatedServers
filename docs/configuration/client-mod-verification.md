---
title: Client Mod Verification
---

Client mod verification is the join-time policy gate that runs after authentication and before a player is fully admitted to the server.

It is intended to:

- require paired client companions for server mods that need them
- block known risky client-side tools by default
- let server owners deny specific client mod IDs, names, or hashes
- provide an optional strict mode for hardened servers

This feature does not download mods from the server and does not make the server a distribution point for executable client code.

## Default Behavior

Default settings in `server_config.toml`:

```toml
[authentication]
modVerificationEnabled = true
modVerificationTimeoutSeconds = 20
blockKnownRiskyClientMods = true
allowUnpairedClientMods = true
strictClientModMode = false
```

With those defaults:

- paired client companions are discovered automatically from installed server mods
- required companions must be present and meet the declared minimum version
- optional companions may be absent, but if present they still must be compatible
- unpaired client-only mods are allowed
- known risky client mods are blocked
- explicit deny rules still apply

## How Paired Mods Work

Server owners do not normally maintain allowlists for ordinary paired mods.

Instead:

- server-side mods declare their expected client companion through `DedicatedServerMod.API.Metadata.S1DSClientCompanionAttribute`
- client-side mods declare their identity through `DedicatedServerMod.API.Metadata.S1DSClientModIdentityAttribute`
- DedicatedServerMod reads that metadata automatically during startup and join verification

See [Companion Mods and Verification Metadata](../modding/companion-mods.md) for the mod-author side.

## Policy File

The policy file is always stored at `UserData/client_mod_policy.toml`, next to `server_config.toml`.

Example:

```toml
[policy]
deniedClientModIds = ['example.badmod']
deniedClientModNames = ['Suspicious Visual Pack']
deniedClientModHashes = ['0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef']
```

Use this file for:

- denying a known mod by stable ID
- denying a mod by display or assembly name when it does not declare an ID
- denying a specific binary by SHA-256
- adding strict-mode overrides

## Configuration Reference

### `modVerificationEnabled`

- Default: `true`
- Enables the post-authentication mod verification handshake

### `modVerificationTimeoutSeconds`

- Default: `20`
- Maximum time the server waits for the client verification report after auth succeeds

### `blockKnownRiskyClientMods`

- Default: `true`
- Blocks entries from DedicatedServerMod’s built-in risky client mod catalog

### `allowUnpairedClientMods`

- Default: `true`
- Allows client-only mods that are not paired with an installed server mod

### `strictClientModMode`

- Default: `false`
- Enables exact hash pinning for paired and approved unpaired mods

## Strict Mode

Strict mode is for hardened or private servers, not the normal default.

When enabled:

- required companion mods must have pinned hashes available
- optional companions must also match pinned hashes when present
- unpaired client-only mods are blocked unless explicitly approved

Pinned hashes can come from:

- a mod author’s `DedicatedServerMod.API.Metadata.S1DSClientCompanionAttribute.PinnedSha256`
- `strictPinnedCompanionHashes` in `client_mod_policy.toml`

If a required companion mod has no strict-mode hash source, server startup fails fast instead of silently weakening policy.

Example strict policy:

```toml
[policy]
deniedClientModIds = []
deniedClientModNames = []
deniedClientModHashes = []

[approvedUnpairedClientMods.bars.visualtrees]
modId = 'bars.visualtrees'
displayName = 'Visual Trees'
pinnedSha256 = ['aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa']

[strictPinnedCompanionHashes.bars.mycoolmod]
modId = 'bars.mycoolmod'
pinnedSha256 = ['bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb']
```

## Command-Line Overrides

The following startup arguments override `server_config.toml`:

- `--mod-verification`
- `--no-mod-verification`
- `--mod-verification-timeout <seconds>`
- `--strict-client-mod-mode`
- `--allow-unpaired-client-mods <true|false>`
- `--block-known-risky-client-mods <true|false>`

## Recommended Setups

### Public Server

- `modVerificationEnabled: true`
- `blockKnownRiskyClientMods: true`
- `allowUnpairedClientMods: true`
- `strictClientModMode: false`

### Private Or Competitive Server

- `modVerificationEnabled: true`
- `blockKnownRiskyClientMods: true`
- `allowUnpairedClientMods: false`
- `strictClientModMode: true`

Only use strict mode when you are willing to maintain pinned hashes.

## Operational Notes

- Verification runs after auth and before normal gameplay/admin custom messages are allowed.
- Failed verification disconnects the player with a human-readable reason.
- Clients never download mods from the server through this system.
- This is a join-policy gate, not a complete anti-cheat. Server-authoritative validation of gameplay actions is still required separately.
