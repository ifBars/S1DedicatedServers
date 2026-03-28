## Installation

Download the latest release from [GitHub Releases](https://github.com/ifBars/S1DedicatedServers/releases). Each release contains two archives:

- `server.zip` for the dedicated server install
- `client.zip` for normal game installs that will connect to dedicated servers

## Compatibility

- Server and client should use matching Schedule I builds.
- Supported MelonLoader versions are `0.6.x`, `0.7.0`, and `0.7.2+`.
- Avoid MelonLoader `0.7.1`.
- Windows is the primary target for server hosting. Linux hosting typically requires Wine or Proton.

## Prepare a server installation

1. Copy your Schedule I game folder to a new location. This becomes the dedicated server install.
2. Extract `server.zip` into that server install.
3. Confirm that `Mods/` merged into the install and `start_server.bat` was placed at the install root.
4. Run `start_server.bat` once to generate `server_config.json`, then close the server.
5. Edit `server_config.json` and set `saveGamePath`.
6. Restart with `start_server.bat`.

For save path details, see [Save Path](../configuration/save-path.md).

## Run fully headless

If you want the server to run without the MelonLoader console window:

1. Open `UserData/MelonLoader.cfg` in the server install.
2. In older MelonLoader versions, the file may be named `UserData/Loader.cfg`.
3. Under `[Console]`, set `HideConsole = true`.
4. In older config formats, use `hide_console = true` instead.
5. Restart the server.

Use the TCP console or log files for monitoring once the local console window is hidden.

## Prepare a client installation

1. Use your main game install, or another separate client copy.
2. Extract `client.zip` so the included `Mods/` contents merge into that install.
3. Launch the game normally and connect to the server.

## Next steps

- [Configuration overview](../configuration.md)
- [Authentication](../configuration/authentication.md)
- [Troubleshooting](../troubleshooting.md)
