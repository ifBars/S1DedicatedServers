# DedicatedServerMod Web Panel

This workspace contains the embedded localhost web panel for DedicatedServerMod.

It is a Bun-managed React + TypeScript + Vite application that builds static assets into `../Server/WebPanel/Static`. The C# server then serves those files from its in-process HTTP host.

## Purpose

The panel is an operator workspace, not a public website. It provides:

- overview and runtime status
- live console output and command execution
- player/session visibility
- configuration editing
- recent activity/log inspection

The panel is localhost-only in v1 and is disabled by default. Server owners must opt in with `webPanelEnabled = true` in `server_config.toml`.

## Package Manager

Use Bun only.

```bash
bun install
```

## Common Commands

Run these from `webpanel`:

```bash
bun run dev
bun run typecheck
bun run build
```

What they do:

- `bun run dev`: starts the Vite dev server for frontend iteration
- `bun run typecheck`: runs TypeScript without emitting files
- `bun run build`: builds the production bundle into `../Server/WebPanel/Static`

## Embedded Build Flow

`bun run build` is the production path. It writes:

- `index.html`
- `assets/app.css`
- `assets/app.js`

into `Server/WebPanel/Static`.

The dedicated server serves those files directly. `dotnet build` does not require a live frontend dev server.

## Runtime Contract

The browser app talks only to same-origin JSON endpoints exposed by the embedded server:

- `GET /api/bootstrap`
- `GET /api/overview`
- `GET /api/players`
- `GET /api/config`
- `POST /api/config`
- `POST /api/actions/save`
- `POST /api/actions/reload-config`
- `POST /api/actions/shutdown`
- `POST /api/console/execute`
- `GET /api/events`

Authentication is based on a one-time launch token exchanged for a localhost session cookie.

## Notes For Contributors

- Keep `src/main.tsx` as the entrypoint only.
- Keep app composition in `src/app/`.
- Keep page UIs under `src/features/`.
- Treat the panel like a real server control surface: dense, flat, operational UI with restrained chrome.
- Do not reintroduce a monolithic `App.tsx` or dashboard-card-heavy layouts.
