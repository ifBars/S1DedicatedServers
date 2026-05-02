---
title: Community Addons
---

Community addons are third-party creations made by the Schedule I dedicated server community. They are not part of DedicatedServerMod, are not maintained by this project, and may have their own install steps, support channels, licenses, and compatibility requirements.

Use this page as a discovery list and starting point. Always read the linked addon documentation before installing it on a server or asking players to install a companion client mod.

## Known Addons

| Addon | Author | Type | Notes |
| --- | --- | --- | --- |
| [S1DS-PlayerList](https://github.com/ZackaryH8/S1DS-PlayerList) | ZackaryH8 | Client and server companion addon | Adds a client-side player list overlay for players connected to a dedicated server. The client builds the visible roster locally and requests server-provided role and ping metadata while the panel is open. |

## Compatibility Expectations

An addon does not have to be written specifically for DedicatedServerMod to work in a dedicated server environment. Compatibility depends on what the mod does and which side it runs on.

Mods are more likely to work when they:

- are client-side only and do not require host-only game state
- use existing game networking paths, such as FishNet-synchronized objects or state that already replicates correctly
- avoid assuming that the host player is an interactive real player
- do not require local server UI, single-player menus, or scene state that is absent on a headless server
- store configuration and data in predictable per-mod locations

Mods are less likely to work when they:

- patch server-owned gameplay systems without considering dedicated server startup order
- assume `Player.Local` or the host player represents the authoritative server actor
- depend on client-only Unity objects from server code
- require every player to have the same client mod but provide no companion metadata or verification guidance
- bypass the S1DS messaging and lifecycle APIs when they need dedicated-server-specific behavior

For addons intentionally built for S1DS, prefer the APIs documented in this section:

- [Server API](server.md) for server-side lifecycle, player, permission, and status-query integration
- [Client API](client.md) for client-side lifecycle and readiness hooks
- [Companion Mods](companion-mods.md) for paired server/client mod metadata and join verification
- [Custom Messaging](messaging.md) for server-client addon traffic
- [Addon Configuration](configuration.md) for addon-owned configuration files

## Adding A Community Addon

To suggest an addon for this page, provide:

- repository or release link
- author or maintainer name
- whether it is server-side, client-side, or paired client/server
- supported S1DS version or release notes
- short description of what it does
- any required companion mod or client verification details

Listings should stay factual. A link here means the addon is relevant to dedicated server users; it does not mean the DedicatedServerMod project owns, audits, or endorses the addon.
