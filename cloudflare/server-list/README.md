# Public Server List Worker Prototype

This Worker provides opt-in discovery for DedicatedServerMod. It does not proxy game traffic and it is not required for direct IP connections.

## Storage model

- D1 stores durable listing identities, hashed bearer secrets, and moderation state.
- KV stores short-lived `v2:active:` presence records with a 180-second TTL.
- KV metadata contains the compact public response, avoiding a `KV.get()` request for every listed server.
- Cloudflare rate-limit bindings protect registration, heartbeat, and public-list routes.

The existing v1 tables and KV prefixes are intentionally untouched.

## Local development

```powershell
bun install
bun run types
bun run check
bun test
bunx wrangler d1 migrations apply s1-servers --local
bun run dev
```

Do not apply the migration remotely or deploy the Worker until the prototype contract and client UI have been accepted.

## API

- `POST /api/v2/listings` creates a listing identity and returns its secret once.
- `PUT /api/v2/listings/{id}/heartbeat` publishes presence using `Authorization: Bearer <secret>`.
- `DELETE /api/v2/listings/{id}/presence` removes presence during graceful shutdown.
- `GET /api/v2/servers?limit=50&cursor=...` returns active, protocol-compatible candidates.

Cloudflare supplies the advertised host through `CF-Connecting-IP`; heartbeat payloads cannot choose arbitrary connection targets.
