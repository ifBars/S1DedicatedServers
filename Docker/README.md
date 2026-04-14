# Docker Deployment Package

This folder is the tracked Docker release template for DedicatedServerMod.

Docker deployment supports both Schedule I server runtimes:

- `mono` uses the Schedule I `alternate` branch
- `il2cpp` uses the default Schedule I branch

The container selects the runtime at startup with `S1DS_RUNTIME`. If you do not set it, the container defaults to `mono`.

`STEAM_BRANCH` is optional. When you leave it unset, the container chooses the expected branch for the selected runtime. Set `STEAM_BRANCH` only when you intentionally want to override that default.

## What You Need

Choose one of these deployment paths:

1. Pull the published image from `ghcr.io/ifbars/s1dedicatedservers`
2. Build locally from `Docker.zip`

Both paths still require your own Steam credentials for the first game install.

## Pull The Published Image

Stable releases publish `latest` and the exact version tag. Prereleases publish the exact version tag only.

```bash
docker pull ghcr.io/ifbars/s1dedicatedservers:latest
```

You can also pin a specific release:

```bash
docker pull ghcr.io/ifbars/s1dedicatedservers:<release-tag>
```

Run the published image with Mono:

```bash
docker run --name s1ds ^
  -p 38465:38465/udp ^
  -p 38465:38465/tcp ^
  -p 27016:27016/udp ^
  -e STEAM_USER=your_steam_login ^
  -e STEAM_PASS=your_steam_password ^
  -e S1DS_RUNTIME=mono ^
  -v s1ds-game:/home/steam/game ^
  ghcr.io/ifbars/s1dedicatedservers:latest
```

Run the published image with IL2CPP:

```bash
docker run --name s1ds ^
  -p 38465:38465/udp ^
  -p 38465:38465/tcp ^
  -p 27016:27016/udp ^
  -e STEAM_USER=your_steam_login ^
  -e STEAM_PASS=your_steam_password ^
  -e S1DS_RUNTIME=il2cpp ^
  -v s1ds-game:/home/steam/game ^
  ghcr.io/ifbars/s1dedicatedservers:latest
```

On Linux/macOS shells, replace `^` line continuations with `\`.

For Docker Compose, the included example now pulls from GHCR by default.

## Build Locally From Release Assets

Download `Docker.zip` from the release assets and extract it to a working folder.

The release package now includes both server DLLs beside the Docker assets:

```text
Docker/
  .dockerignore
  Dockerfile
  docker-compose.example.yml
  run.sh
  README.md
  DedicatedServerMod_Mono_Server.dll
  DedicatedServerMod_Il2cpp_Server.dll
```

The Docker image downloads MelonLoader during `docker build`, so you do not need to add MelonLoader files manually.

## Build

```bash
docker build -t s1ds-dedicated-server .
```

The local image can run either runtime. Runtime selection happens when the container starts, not when the image is built.

## Run

You can start the container either with `docker run` or Docker Compose.

### `docker run`

First boot needs Steam credentials because the container installs Schedule I through SteamCMD at startup.

Mono example:

```bash
docker run --name s1ds ^
  -p 38465:38465/udp ^
  -p 38465:38465/tcp ^
  -p 27016:27016/udp ^
  -e STEAM_USER=your_steam_login ^
  -e STEAM_PASS=your_steam_password ^
  -e S1DS_RUNTIME=mono ^
  -v s1ds-game:/home/steam/game ^
  s1ds-dedicated-server
```

IL2CPP example:

```bash
docker run --name s1ds ^
  -p 38465:38465/udp ^
  -p 38465:38465/tcp ^
  -p 27016:27016/udp ^
  -e STEAM_USER=your_steam_login ^
  -e STEAM_PASS=your_steam_password ^
  -e S1DS_RUNTIME=il2cpp ^
  -v s1ds-game:/home/steam/game ^
  s1ds-dedicated-server
```

### Docker Compose

An example Compose file is included as `docker-compose.example.yml`. Copy it to `docker-compose.yml`, then create a `.env` file beside it with your Steam credentials and runtime selection:

```env
S1DS_IMAGE=ghcr.io/ifbars/s1dedicatedservers:latest
STEAM_USER=your_steam_login
STEAM_PASS=your_steam_password
S1DS_RUNTIME=mono
# Leave STEAM_BRANCH unset to use the runtime default.
STEAM_BRANCH=
# Optional on first login when Steam prompts for a code.
STEAM_GUARD=
# Optional; set to true when you want to force a fresh update.
FORCE_STEAMCMD_UPDATE=false
```

Set `S1DS_IMAGE` to an exact version tag when you want to pin a release or use a prerelease image.

Start it with:

```bash
docker compose up -d
```

To build locally from `Docker.zip` instead of pulling from GHCR, replace the `image:` line in the Compose file with the commented `build:` block before running `docker compose up -d --build`.

## Notes

- `STEAM_GUARD` can be supplied when Steam prompts for a guard code.
- `FORCE_STEAMCMD_UPDATE=true` forces a fresh game update on the next run.
- Persist `/home/steam/game` so the installed game files and generated config survive container recreation.
- The Compose example also persists `/home/steam/steamcmd` and `/home/steam/Steam` to avoid re-downloading Steam runtime data on every container recreation.
- Leave `STEAM_BRANCH` unset unless you intentionally need a non-default branch for the selected runtime.
- Switching `S1DS_RUNTIME` on an existing persistent game volume is supported; the container removes the other runtime's DedicatedServerMod DLL before starting.
- Edit the generated `server_config.toml` under the mounted game directory after first boot.
- Publish `serverPort` on both UDP and TCP. UDP is gameplay traffic; TCP is DedicatedServerMod's status query endpoint.
- If you use `SteamGameServer`, also publish `steamGameServerQueryPort` on UDP.
- Only publish `tcpConsolePort` on TCP when `[tcpConsole].tcpConsoleEnabled = true` and you intentionally bind the console beyond localhost.
- For auth and networking guidance inside containers, see the docs site Docker page and the authentication/messaging backend guides.
