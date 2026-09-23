---
title: Public Server List API
description: Query active, opted-in Schedule I dedicated servers from a third-party service.
---

# Public Server List API

The public directory lets third-party services look up servers that have opted into S1DS public discovery. Requests do not need a listing credential. Query the API from your backend; the endpoint is intended for server-to-server use and does not send browser CORS headers.

Base URL: `https://list.s1servers.com`

## List or search servers

`GET /api/v2/servers`

| Parameter | Description |
| --- | --- |
| `name` | Case-insensitive substring of the advertised server name. Maximum 100 characters. |
| `host` | Exact advertised IP address. Use with `port`. |
| `port` | Exact gameplay port, from 1 through 65535. Use with `host`. |
| `limit` | Maximum number of servers in the response, from 1 through 100. Defaults to 50; values outside the range are clamped. |
| `cursor` | Opaque continuation value from the preceding response. Use it with the same filters and limit. |

Omit `name`, `host`, and `port` to browse the full directory. Filters can be combined. For example, a listing site can look up a server submitted by its owner:

```http
GET https://list.s1servers.com/api/v2/servers?host=203.0.113.10&port=7777
```

The address must match the `host` and `port` published by the directory. The host is the public source IP seen by the listing service, not a hostname supplied by the server. A server name can change or be shared by multiple servers, so use the address and port when checking a submitted server.

For name search:

```http
GET https://list.s1servers.com/api/v2/servers?name=community&limit=50
```

A successful response has this shape:

```json
{
  "success": true,
  "servers": [
    {
      "listingId": "00000000-0000-0000-0000-000000000001",
      "protocolVersion": 2,
      "serverName": "Community Server",
      "serverDescription": "A place to play together",
      "currentPlayers": 3,
      "maxPlayers": 16,
      "port": 7777,
      "host": "203.0.113.10",
      "passwordProtected": false,
      "gameVersion": "0.4.0",
      "modVersion": "1.0.0",
      "lastHeartbeat": 1790180000000
    }
  ],
  "nextCursor": "opaque-continuation-value"
}
```

`lastHeartbeat` is Unix time in milliseconds. The directory includes only active, compatible listings with a heartbeat in the last 15 minutes. This is a presence signal, not a live connection test. Player counts and other metadata reflect the last heartbeat. A client can query the server's TCP status endpoint at the advertised host and port for a live check.

## Pagination

When `nextCursor` is present, request another page with the same filters. Continue even if `servers` is empty: a page can contain listings that expired or did not match the search. Search scans at most 1,000 directory records or 20 KV pages per request, then returns `nextCursor` if more records remain. The cursor is tied to the underlying directory scan, so do not treat it as a server identifier or store it long-term.

For an exact address lookup, continue until you find a match or a response has no `nextCursor`. Save the returned `listingId` if you need to recognize the same listing later; the address or name may change.

## Errors and limits

Errors use `{ "success": false, "error": "CODE", "message": "..." }`. Invalid search values return HTTP 400 with `INVALID_SEARCH`; an oversized cursor returns HTTP 400 with `INVALID_CURSOR`. The public endpoint is rate limited by source IP and returns HTTP 429 with `RATE_LIMITED` when the limit is exceeded. Its current limit is 120 requests per minute per source IP. Successful responses may be cached for up to 10 seconds.

Only opted-in servers appear here. Server owners enable public discovery through the [Public Server List configuration](configuration/public-server-list.md).
