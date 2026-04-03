# Quick Start

Run authoritative, headless Schedule I servers and build server/client mods against the same in-repo documentation set.

## Download the Release

1. Download the latest release from [GitHub Releases](https://github.com/ifBars/S1DedicatedServers/releases).
2. Use `server.zip` for the dedicated server install.
3. Use `client.zip` for normal game installs that will connect to dedicated servers.

## Compatibility

- Server and client should use matching Schedule I builds.
- Supported MelonLoader versions are `0.6.x`, `0.7.0`, and `0.7.2+`.
- Avoid MelonLoader `0.7.1`.
- Windows is the primary target for server hosting. Linux hosting typically requires Wine or Proton.

## Create a Server Install

1. Copy your Schedule I game folder to a new location. This becomes the dedicated server install.
2. Extract `server.zip` into that server install.
3. Confirm that `Mods/` merged into the install and `start_server.bat` was placed at the install root.
4. Run `start_server.bat` once so the mod generates `server_config.toml`, then close it.
5. Edit `server_config.toml` and set `saveGamePath`.
6. Start the server again with `start_server.bat`.

For save path details, see [Save Path](docs/configuration/save-path.md).

The integrated localhost web panel is generated with the server build but remains disabled by default. If you want the browser UI on a local or home-hosted machine, enable it in `server_config.toml`:

```toml
[webPanel]
webPanelEnabled = true
```

## Run Fully Headless

If you want the server to run without the MelonLoader console window:

1. Open `UserData/MelonLoader.cfg` in the server install.
2. In older MelonLoader versions, the file may be named `UserData/Loader.cfg`.
3. Under `[Console]`, set `HideConsole = true`.
4. In older config formats, use `hide_console = true` instead.
5. Restart the server.

Use the TCP console or log files for monitoring once the local console window is hidden.

For panel-hosted environments that control the process through stdin/stdout, prefer the stdio host console and launch with `-logFile -` so logs are emitted to stdout. See [Host Console](docs/host-console.md).

For home-hosted environments where you want a browser-based operator UI on the same machine, see [Web Panel](docs/configuration/web-panel.md).

## Prepare a Client Install

1. Use your main game install, or another separate client copy.
2. Extract `client.zip` so the included `Mods/` contents merge into that install.
3. Launch the game normally and connect to the server.

## After First Boot

- [Overview](docs/index.md)
- [Configuration](docs/configuration.md) to harden and tune the server
- [Authentication](docs/configuration/authentication.md)
- [Troubleshooting](docs/troubleshooting.md) if startup or connection fails
- [API Reference](reference/index.md)
