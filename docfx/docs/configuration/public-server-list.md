---
title: Public Server List
description: Opt a DedicatedServerMod server into public discovery while keeping direct IP connections independent.
---

# Public Server List

Public discovery is optional and disabled by default. A server that does not opt in can still be reached by IP and port, saved as a favorite, and shown in client history.

![Favorites, History, and Public tabs centered above the server list](../assets/public-server-list-tabs.png)

## Enable Listing

Set `publicListingEnabled` under `[publicListing]`:

```toml
[publicListing]
publicListingEnabled = true
publicListingServiceUrl = 'https://list.s1servers.com'
publicListingId = ''
publicListingSecret = ''
```

Leave `publicListingId` and `publicListingSecret` empty when enabling discovery for the first time. The server registers after the game, save, gameplay transport, and TCP status-query endpoint are ready, then writes both values back to `server_config.toml`.

`publicListingSecret` is a credential. Do not publish it in support logs, screenshots, container templates, or public configuration examples.

## Published Data

An opted-in server publishes:

- The public IP address observed by Cloudflare and the configured `serverPort`
- Server name and description
- Current and maximum player counts
- Password-protected state
- Game, mod, and directory protocol versions

The directory does not publish the server password, operator identities, player identities, save data, or listing secret.

## Availability and Verification

The server sends a heartbeat every five minutes. Directory presence expires automatically after 15 minutes without a heartbeat, preserving the same three-missed-heartbeat tolerance while reducing Cloudflare requests. Graceful shutdown attempts immediate removal, but TTL expiry remains authoritative.

Clients treat directory results as discovery candidates. They query the existing TCP status endpoint before showing verified latency and live metadata. Public hosting therefore still requires `serverPort` to be reachable over both UDP for gameplay and TCP for status queries.

If the directory is unavailable:

- Server startup continues normally.
- Connected players are unaffected.
- Direct connect, favorites, and history continue to work.
- The Public tab reports that discovery is temporarily unavailable.

## Opt Out

Set `publicListingEnabled = false` and restart the server. The listing disappears after the active presence TTL even if immediate removal cannot reach the directory. Keeping the issued ID and secret allows the same listing identity to be reused if the server opts in again later.
