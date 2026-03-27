## Installation

Download the latest release from [GitHub Releases](https://github.com/ifBars/S1DedicatedServers/releases). Each release contains both archives: `server.zip` and `client.zip`.

Two zips are provided in each release:

- server.zip
  - Contains `Mods/` with the dedicated server mod DLL
  - Contains `start_server.bat`

- client.zip
  - Contains `Mods/` with the client mod DLL (for the main game install)

### Prepare a server installation
1) Copy your Schedule I game folder to a new location (this becomes your server install).

2) Extract `server.zip` into the server install so that:
   
   - `Mods/` (from the zip) merges into the install’s Mods folder
   - `start_server.bat` is placed at the root of the server install

3) Run the server once with `start_server.bat` to generate `server_config.json`, then close it.

4) Edit `server_config.json` and set `saveGamePath` (see Configuration → Save path). Restart with `start_server.bat`.

### Prepare a client installation
1) Use your main game install (or another copy for clients).

2) Extract `client.zip` into the main install so that the `Mods/` folder contains the client mod DLL.

3) Launch the game normally and connect to the server.


