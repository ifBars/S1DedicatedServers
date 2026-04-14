# Quick Start

Run authoritative, headless Schedule I servers and build server/client mods against the same in-repo documentation set.

## Download the Release

1. Download the latest release from [GitHub Releases](https://github.com/ifBars/S1DedicatedServers/releases).
2. Use `Mono-Server.zip` for Mono dedicated server installs.
3. Use `Mono-Client.zip` for Mono game installs that will connect to dedicated servers.
4. Use `Il2cpp_Server.zip` for IL2CPP dedicated server installs.
5. Use `Il2cpp_Client.zip` for IL2CPP game installs that will connect to dedicated servers.

## Compatibility

- Server and client should use matching Schedule I builds.
- Supported MelonLoader versions are `0.6.x`, `0.7.0`, and `0.7.2+`.
- Avoid MelonLoader `0.7.1`.
- Current beta builds continue initial IL2CPP support for both dedicated-server and client installs. Expect rough edges, and report IL2CPP issues on the [GitHub issue tracker](https://github.com/ifBars/S1DedicatedServers/issues) with logs and reproduction steps.
- Windows is the primary target for server hosting. Linux hosting typically requires Wine or Proton.

## Host Sizing Baseline

The smallest host size currently known to run a mostly idle server is a VPS with:

- `2` vCPUs at around `2.0 GHz`
- `4 GB` RAM

That baseline has only been observed to handle a server with `0-1` connected players, at roughly `72%` average CPU usage and about `3.0 GB` RAM in use. Treat it as a bare-minimum starting point, not as a comfortable production target.

If you expect multiple players, CPU spikes during save/load, extra mods, or virtualization overhead from a budget VPS, provision more headroom.

## Docker Deployment

For containerized hosting, use the Docker release package and follow [Docker Deployment](docs/docker.md).

`Docker.zip` intentionally contains only the Docker template files. Copy `Mods/DedicatedServerMod_Mono_Server.dll` from `Mono-Server.zip` into that Docker folder before running `docker build`.
Docker deployment currently targets the Mono server artifact; use `Il2cpp_Server.zip` for native IL2CPP installs outside Docker.

## Create a Server Install

1. Copy your Schedule I game folder to a new location. This becomes the dedicated server install.
2. Extract `Mono-Server.zip` into that server install for Mono, or `Il2cpp_Server.zip` for IL2CPP.
3. Confirm that `Mods/` merged into the install and `start_server.bat` was placed at the install root.
4. Run `start_server.bat` once so the mod generates `server_config.toml`, then close it.
5. Edit `server_config.toml` and set `saveGamePath`.
6. Start the server again with `start_server.bat`.

For save path details, see [Save Path](docs/configuration/save-path.md).

## Prepare a Client Install

1. Use your main game install, or another separate client copy.
2. Extract `Mono-Client.zip` for Mono or `Il2cpp_Client.zip` for IL2CPP so the included `Mods/` contents merge into that install.
3. Launch the game normally and connect to the server.

## After First Boot

- [Overview](docs/index.md)
- [Configuration](docs/configuration.md) to harden and tune the server
- [Authentication](docs/configuration/authentication.md)
- [Host Console](docs/host-console.md) for panel-hosted stdin/stdout administration
- [Web Panel](docs/configuration/web-panel.md) for local browser-based administration
- [Troubleshooting](docs/troubleshooting.md) if startup or connection fails
- [API Reference](reference/index.md)
