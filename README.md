# S1DS - S1 DedicatedServerMod

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)](https://github.com/ifBars/S1DedicatedServers)
[![Version](https://img.shields.io/badge/version-0.9.0--beta-blue)](https://github.com/ifBars/S1DedicatedServers/releases)
[![License](https://img.shields.io/github/license/ifBars/S1DedicatedServers)](https://github.com/ifBars/S1DedicatedServers/blob/master/LICENSE)

DedicatedServerMod adds authoritative, headless dedicated servers to Schedule I, along with admin tooling, remote console support, an optional localhost web panel, and a public mod API for server and client extensions.

`0.9.0-beta` introduces initial IL2CPP support for dedicated server and client workflows. Treat IL2CPP support as beta: if you hit IL2CPP-specific regressions or startup issues, report them on the [GitHub issue tracker](https://github.com/ifBars/S1DedicatedServers/issues) with logs and reproduction steps.

## What It Provides

- Headless dedicated hosting with Schedule I save/load, time, sleep, and multiplayer flow support
- Operators, admins, configurable command permissions, and remote console access
- Configurable authentication and messaging backends for private or public hosting
- Server and client mod APIs with lifecycle hooks, persistence hooks, and more for mod developers
## Quick Start

1. Download the latest release from [GitHub Releases](https://github.com/ifBars/S1DedicatedServers/releases).
2. Follow the [Quick Start guide](https://docs.s1servers.com/).
3. Review the [configuration overview](https://docs.s1servers.com/docs/configuration.html) before opening a server to other players.
4. Check the [troubleshooting guide](https://docs.s1servers.com/docs/troubleshooting.html) if startup, networking, or authentication goes wrong.

## Documentation

- [Docs site](https://docs.s1servers.com/)
- [Quick Start](https://docs.s1servers.com/)
- [Docker Deployment](https://docs.s1servers.com/docs/docker.html)
- [Configuration](https://docs.s1servers.com/docs/configuration.html)
- [Web Panel](https://docs.s1servers.com/docs/configuration/web-panel.html)
- [Authentication](https://docs.s1servers.com/docs/configuration/authentication.html)
- [Client mod verification](https://docs.s1servers.com/docs/configuration/client-mod-verification.html)
- [Commands and permissions](https://docs.s1servers.com/docs/commands.html)
- [Host console](https://docs.s1servers.com/docs/host-console.html)
- [Troubleshooting](https://docs.s1servers.com/docs/troubleshooting.html)
- [Mod API overview](https://docs.s1servers.com/docs/modding/overview.html)
- [Server modding](https://docs.s1servers.com/docs/modding/server.html)
- [Client modding](https://docs.s1servers.com/docs/modding/client.html)
- [Companion mod metadata](https://docs.s1servers.com/docs/modding/companion-mods.html)
- [API reference](https://docs.s1servers.com/reference/index.html)

## Development

Contributions are welcome. Read [CONTRIBUTING.md](CONTRIBUTING.md), [CODING_STANDARDS.md](CODING_STANDARDS.md), and [BUILD_SETUP.md](BUILD_SETUP.md) before opening a pull request.

## Disclaimer

This mod is not officially affiliated with or endorsed by the developers of Schedule I.
