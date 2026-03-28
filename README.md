# DedicatedServerMod

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)](https://github.com/ifBars/S1DedicatedServers)
[![Version](https://img.shields.io/badge/version-0.2.1--beta-blue)](https://github.com/ifBars/S1DedicatedServers/releases)
[![License](https://img.shields.io/github/license/ifBars/S1DedicatedServers)](https://github.com/ifBars/S1DedicatedServers/blob/master/LICENSE)

DedicatedServerMod adds authoritative, headless dedicated servers to Schedule I, along with admin tooling, remote console support, and a public mod API for server and client extensions.

Current release status: `0.2.1-beta`. The project is usable, but the runtime and public API are still evolving. Minimal support will be provided until a full non-beta release is made.

## What It Provides

- Headless dedicated hosting with Schedule I save/load, time, sleep, and multiplayer flow support
- Operators, admins, configurable command permissions, and remote TCP console access
- Configurable authentication and messaging backends for private or public hosting
- Server and client mod APIs with lifecycle hooks, persistence hooks, and custom messaging

## Quick Start

1. Download the latest release from [GitHub Releases](https://github.com/ifBars/S1DedicatedServers/releases).
2. Follow the [installation guide](https://ifbars.github.io/S1DedicatedServers/docs/home/installation.html).
3. Review the [configuration overview](https://ifbars.github.io/S1DedicatedServers/docs/configuration.html) before opening a server to other players.
4. Check the [troubleshooting guide](https://ifbars.github.io/S1DedicatedServers/docs/troubleshooting.html) if startup, networking, or authentication goes wrong.

## Documentation

- [Docs site](https://ifbars.github.io/S1DedicatedServers/)
- [Installation](https://ifbars.github.io/S1DedicatedServers/docs/home/installation.html)
- [Configuration](https://ifbars.github.io/S1DedicatedServers/docs/configuration.html)
- [Authentication](https://ifbars.github.io/S1DedicatedServers/docs/configuration/authentication.html)
- [Commands and permissions](https://ifbars.github.io/S1DedicatedServers/docs/commands.html)
- [Troubleshooting](https://ifbars.github.io/S1DedicatedServers/docs/troubleshooting.html)
- [Mod API overview](https://ifbars.github.io/S1DedicatedServers/docs/modding/overview.html)
- [Server modding](https://ifbars.github.io/S1DedicatedServers/docs/modding/server.html)
- [Client modding](https://ifbars.github.io/S1DedicatedServers/docs/modding/client.html)
- [API reference](https://ifbars.github.io/S1DedicatedServers/reference/index.html)

## Development

Contributions are welcome. Read [CONTRIBUTING.md](CONTRIBUTING.md), [CODING_STANDARDS.md](CODING_STANDARDS.md), and [BUILD_SETUP.md](BUILD_SETUP.md) before opening a pull request.

Example build:

```bash
dotnet build -c Mono_Server
```

## Disclaimer

This mod is not officially affiliated with or endorsed by the developers of Schedule I.
