# Docker Deployment Package

This folder is the tracked Docker release template for DedicatedServerMod.

It intentionally does **not** include:

- MelonLoader bootstrap files
- `DedicatedServerMod_Mono_Server.dll`
- a preinstalled game directory

## What You Need

1. `Docker.zip` from the release assets
2. `Server.zip` from the same release
3. Your own Steam credentials for the first game install

## Folder Layout

Extract `Docker.zip`, then copy `Mods/DedicatedServerMod_Mono_Server.dll` from `Server.zip` into the same folder as `Dockerfile`:

```text
Docker/
  .dockerignore
  Dockerfile
  run.sh
  README.md
  DedicatedServerMod_Mono_Server.dll
```

`docker build` will fetch MelonLoader automatically. The image expects the server DLL to already be present in the build context.

## Build

```bash
docker build -t s1ds-dedicated-server .
```

## Run

First boot needs Steam credentials because the container installs Schedule I through SteamCMD at startup:

```bash
docker run --name s1ds ^
  -p 38465:38465/udp ^
  -p 27016:27016/udp ^
  -e STEAM_USER=your_steam_login ^
  -e STEAM_PASS=your_steam_password ^
  -e STEAM_BRANCH=alternate ^
  -v s1ds-game:/home/steam/game ^
  s1ds-dedicated-server
```

On Linux/macOS shells, replace `^` line continuations with `\`.

## Notes

- `STEAM_GUARD` can be supplied when Steam prompts for a guard code.
- `FORCE_STEAMCMD_UPDATE=true` forces a fresh game update on the next run.
- Persist `/home/steam/game` so the installed game files and generated config survive container recreation.
- Edit the generated `server_config.toml` under the mounted game directory after first boot.
- For auth and networking guidance inside containers, see the docs site Docker page and the authentication/messaging backend guides.
