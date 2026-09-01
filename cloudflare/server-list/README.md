# Public Server List Worker

This Worker provides opt-in discovery for DedicatedServerMod. It does not proxy game traffic and it is not required for direct IP connections.

## Storage model

- D1 stores account-linked operators, Discord/Steam identities, hashed sessions, durable listing identities, hashed listing secrets, and moderation state.
- KV stores short-lived `v2:active:` presence records with a 15-minute TTL.
- KV metadata contains the compact public response, avoiding a `KV.get()` request for every listed server.
- One indexed D1 query per page applies current ownership and moderation state before candidates are returned.
- Cloudflare rate-limit bindings protect portal actions, heartbeat, and public-list routes.

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

Production traffic is served from `https://list.s1servers.com`. Wrangler manages the custom-domain DNS record and certificate from `wrangler.jsonc`; the `workers.dev` route is disabled.

## API

- `GET /api/v2/portal/auth/{discord|steam}/start` begins operator sign-in.
- `GET /api/v2/portal/me` returns the current operator identity and CSRF token.
- `GET|POST /api/v2/portal/listings` lists or issues account-linked credentials.
- `POST /api/v2/portal/listings/{id}/rotate` rotates a secret and evicts current presence.
- `DELETE /api/v2/portal/listings/{id}` revokes a listing and evicts current presence.
- `PUT /api/v2/listings/{id}/heartbeat` publishes presence using `Authorization: Bearer <secret>`.
- `DELETE /api/v2/listings/{id}/presence` removes presence during graceful shutdown.
- `GET /api/v2/servers?limit=50&cursor=...` returns active, protocol-compatible candidates.

Cloudflare supplies the advertised host through `CF-Connecting-IP`; heartbeat payloads cannot choose arbitrary connection targets.
Anonymous `POST /api/v2/listings` registration is intentionally rejected. Discord access tokens and Steam assertions are used only to establish an identity and are not retained.

## Discord configuration

Create a Discord application with this redirect URI:

`https://list.s1servers.com/api/v2/portal/auth/discord/callback`

Store both application values without committing them:

```powershell
bunx wrangler secret put DISCORD_CLIENT_ID
bunx wrangler secret put DISCORD_CLIENT_SECRET
```
