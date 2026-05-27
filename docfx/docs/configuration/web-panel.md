## Web Panel

DedicatedServerMod includes an embedded loopback-only web panel for server owners who want a browser-based operator workspace on the same machine as the server.

This feature is disabled by default.

## When to use it

Use the web panel when:

- you are running the server on your own machine or home-hosted hardware
- you want a browser UI for console, players, config, and activity
- you are comfortable keeping the surface bound to loopback only and opening it from the server host

Leave it disabled when:

- you are deploying into a hosted panel environment that already owns process lifecycle and console I/O
- you need LAN or internet browser access from another machine
- you do not want an extra local HTTP listener
- the host only exposes stdio and log capture, which is already supported through the host console transport

## Default behavior

`webPanelEnabled` defaults to `false`.

That is intentional. The panel should be opt-in so server providers and service-style deployments do not unexpectedly expose or depend on a browser control surface.

## Configuration

Example:

```toml
[webPanel]
webPanelEnabled = true
webPanelBindAddress = '127.0.0.1'
webPanelPort = 4051
webPanelOpenBrowserOnStart = true
webPanelSessionMinutes = 120
webPanelExposeLogs = true
```

Field summary:

- `webPanelEnabled`: enables the embedded panel host
- `webPanelBindAddress`: loopback bind address for the HTTP listener; use `127.0.0.1` or another loopback address. `0.0.0.0` and LAN/public bind addresses are not supported by the built-in panel.
- `webPanelPort`: HTTP port used by the panel
- `webPanelOpenBrowserOnStart`: best-effort browser launch on startup
- `webPanelSessionMinutes`: lifetime of the localhost session
- `webPanelExposeLogs`: includes recent runtime logs in the panel bootstrap and live activity stream

## Startup behavior

When enabled:

1. the server starts an in-process localhost HTTP listener
2. it generates a one-time launch token
3. it attempts to open the default browser to the local launch URL
4. if auto-open fails, it logs the URL instead

The panel does not replace the TCP console or stdio host console. It sits alongside them.

The built-in panel is not a remote admin website. Do not publish or port-forward `webPanelPort`; the listener intentionally rejects non-loopback bind addresses.

## Security model

The current panel is intentionally narrow:

- localhost-only in v1
- one-time launch token
- short-lived session cookie
- same-origin JSON API
- no public LAN or internet exposure path

This is a security boundary, not just a default. The built-in web panel is intended for local operators and does not provide the authentication, rate limiting, TLS termination, audit logging, or network hardening expected for a remotely exposed admin site.

For remote administration, prefer one of these paths:

- use the TCP console with a non-loopback bind address, firewall rules, and a required password
- use the stdio host console through a hosted control panel such as Pterodactyl
- build your own web panel or provider integration on top of the TCP console or another authenticated control surface

## Hosted environments

For hosted control panels and game hosting providers, prefer the stdio host console path described in [Host Console](../host-console.md).

That model integrates better with existing panels such as Pterodactyl-style process supervision and log capture. The embedded web panel is primarily for local operators.
