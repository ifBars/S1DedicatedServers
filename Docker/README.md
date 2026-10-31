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

Stable releases publish `latest` and the exact version tag. Prereleases publish the exact version tag only, without the leading `v` from the GitHub release tag.

```bash
docker pull ghcr.io/ifbars/s1dedicatedservers:1.1.6
```

Use `latest` only after a stable release has been published:

```bash
docker pull ghcr.io/ifbars/s1dedicatedservers:latest
```

Run the published image with Mono:

```bash
docker run --name s1ds --stop-timeout 120 \
  -p 38465:38465/udp \
  -p 38465:38465/tcp \
  -p 27016:27016/udp \
  -e STEAM_USER=your_steam_login \
  -e STEAM_PASS=your_steam_password \
  -e S1DS_RUNTIME=mono \
  -e LANG=C.UTF-8 \
  -e LC_ALL=C.UTF-8 \
  -v s1ds-game:/home/steam/game \
  ghcr.io/ifbars/s1dedicatedservers:1.1.6
```

Run the published image with IL2CPP:

```bash
docker run --name s1ds --stop-timeout 120 \
  -p 38465:38465/udp \
  -p 38465:38465/tcp \
  -p 27016:27016/udp \
  -e STEAM_USER=your_steam_login \
  -e STEAM_PASS=your_steam_password \
  -e S1DS_RUNTIME=il2cpp \
  -e LANG=C.UTF-8 \
  -e LC_ALL=C.UTF-8 \
  -v s1ds-game:/home/steam/game \
  ghcr.io/ifbars/s1dedicatedservers:1.1.6
```

The examples use Bash line continuations (`\`).

For Docker Compose, the included example now pulls from GHCR by default.

The examples pin 1.1.6 rather than the older 1.0.0 image, which predates the save/shutdown fixes. For later upgrades, choose an exact published release tag and use client packages from that same release.

## Build Locally From Release Assets

Download `Docker.zip` from the release assets and extract it to a working folder.

The release package now includes both server DLLs beside the Docker assets:

```text
Docker/
  .dockerignore
  Dockerfile
  docker-compose.example.yml
  run.sh
  server-lifecycle.sh
  README.md
  DedicatedServerMod_Mono_Server.dll
  DedicatedServerMod_Il2cpp_Server.dll
```

The Docker image downloads MelonLoader during `docker build`, so you do not need to add MelonLoader files manually.
The image runs on the stable Wine 11 base and installs the .NET 6 desktop runtime into an image-managed Wine prefix during build so IL2CPP MelonLoader startup does not need to run its first-launch runtime installer. The Wine prefix is kept outside `/home/steam/game`, so bind-mounting or persisting the game directory does not hide the preinstalled runtime.

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
docker run --name s1ds --stop-timeout 120 \
  -p 38465:38465/udp \
  -p 38465:38465/tcp \
  -p 27016:27016/udp \
  -e STEAM_USER=your_steam_login \
  -e STEAM_PASS=your_steam_password \
  -e S1DS_RUNTIME=mono \
  -e LANG=C.UTF-8 \
  -e LC_ALL=C.UTF-8 \
  -v s1ds-game:/home/steam/game \
  s1ds-dedicated-server
```

IL2CPP example:

```bash
docker run --name s1ds --stop-timeout 120 \
  -p 38465:38465/udp \
  -p 38465:38465/tcp \
  -p 27016:27016/udp \
  -e STEAM_USER=your_steam_login \
  -e STEAM_PASS=your_steam_password \
  -e S1DS_RUNTIME=il2cpp \
  -e LANG=C.UTF-8 \
  -e LC_ALL=C.UTF-8 \
  -v s1ds-game:/home/steam/game \
  s1ds-dedicated-server
```

### Docker Compose

An example Compose file is included as `docker-compose.example.yml`. Copy it to `docker-compose.yml`, then create a `.env` file beside it with your Steam credentials and runtime selection:

```env
S1DS_IMAGE=ghcr.io/ifbars/s1dedicatedservers:1.1.6
STEAM_USER=your_steam_login
STEAM_PASS=your_steam_password
S1DS_RUNTIME=mono
LANG=C.UTF-8
LC_ALL=C.UTF-8
# Leave STEAM_BRANCH unset to use the runtime default.
STEAM_BRANCH=
# Optional on first login when Steam prompts for a code.
STEAM_GUARD=
# Optional; set to true when you want to force a fresh update.
FORCE_STEAMCMD_UPDATE=false
```

Use `ghcr.io/ifbars/s1dedicatedservers:latest` for the latest stable release, or pin `S1DS_IMAGE` to an exact version tag such as `ghcr.io/ifbars/s1dedicatedservers:1.1.6` when you want controlled upgrades. Prereleases should use the exact version tag shown on the GitHub release without the leading `v`.

Start it with:

```bash
docker compose up -d
```

To build locally from `Docker.zip` instead of pulling from GHCR, replace the `image:` line in the Compose file with the commented `build:` block before running `docker compose up -d --build`.

## IL2CPP Interop Cache Cleanup

After SteamCMD and the pinned MelonLoader bootstrap are applied, and before Wine starts Schedule I, the entrypoint confirms the game is not running and removes only this disposable Cpp2IL output directory:

```text
/home/steam/game/MelonLoader/Dependencies/Il2CppAssemblyGenerator/Cpp2IL/cpp2il_out
```

This cleanup is safe and idempotent on every startup. It does not remove `MelonLoader/Il2CppAssemblies`, `Mods`, configuration, saves, or logs. It prevents Il2CppInterop from consuming assemblies that a game update removed but a persistent game volume retained in Cpp2IL output.

### Troubleshooting IL2CPP Interop Generation After A Game Update

If startup reports an Il2CppInterop generation error such as `Pass11ComputeTypeSpecifics`, stop the server, delete only the `cpp2il_out` directory shown above, then start the server so it regenerates the output. The container performs this recovery automatically on every startup.

Do not use a MelonLoader downgrade or reinstall as the primary fix: the inspected `0.7.0` through `0.7.3` releases share the stale-output behavior, and reinstalling often appears to help only because it removes this directory. The image remains pinned to the currently tested MelonLoader `0.7.2` until an upstream release containing [MelonLoader PR #1193](https://github.com/LavaGang/MelonLoader/pull/1193) is tested.

## UTF-8 Launch Environment

Pass `LANG=C.UTF-8` and `LC_ALL=C.UTF-8` to the container, as shown in the examples. The supplied Compose file reads them from `.env` through `env_file`; a `.env` file alone does not inject variables into an arbitrary third-party Compose service. For a custom image or hosted panel, confirm that the locale is installed and that both variables reach the Wine/game process, or choose another installed UTF-8 locale.

[Issue #71](https://github.com/ifBars/S1DedicatedServers/issues/71) reported save failures at `NPCs/Javier Pérez` that were resolved by setting these variables. The same path was later reported in [#78](https://github.com/ifBars/S1DedicatedServers/issues/78), where the affected service's resolution is still unconfirmed. Locale changes apply on the next launch; preserve recovery copies and review [save-failure recovery](https://docs.s1servers.com/docs/troubleshooting/save-load.html#saves-fail-or-shutdown-is-cancelled) before restarting a world that cannot save.

## Stopping and Restarting

Images built from the current source force the S1DS stdio console on and keep a private command pipe open even when Docker provides no stdin. SIGTERM/SIGINT writes `shutdown` once and waits for game process exit, keeping Xvfb alive until the final save finishes. Repeated stop signals do not kill the game or queue duplicate shutdowns. Attached/panel stdin is forwarded when available; EOF does not close the private pipe. The wrapper returns the game's exit code.

The Compose example sets `stop_grace_period: 2m`; direct `docker run` examples use `--stop-timeout 120`. Docker can still send SIGKILL after this deadline. Allow more time for large worlds/slow storage, or request an unlimited wait with `docker stop --timeout -1 s1ds`. See Docker's [stop timeout reference](https://docs.docker.com/reference/cli/docker/container/stop/#timeout).

If the final save fails, S1DS cancels shutdown and the supervisor continues waiting. Resolve the native persistence error, then send a new `shutdown` through the configured TCP console or attached stdin. Repeated Docker stop signals are not a save retry. With an unlimited timeout, stopping remains pending until the game exits.

The already-published 1.1.6 image used by the pinned examples still has the old immediate-termination handler. Build from source to use the new handler until a release containing it is published. On older images, manually send `shutdown` through the configured console and wait for `Save completed: server_shutdown`, `=== Server Shutdown Complete ===`, and game exit before stopping/recreating the container. Increasing an old image's timeout alone does not add graceful shutdown.

Before inviting players, verify a disposable/copied world: connect, run `save` and confirm S1DS reports completion, run `shutdown` and confirm its separate final save, then restart/reconnect and check player progression, owned businesses/properties, and placed objects. Keep known-good backups outside the active save folder. See the [full verification checklist](https://docs.s1servers.com/docs/configuration/startup-and-deployment.html#verify-a-new-install-or-upgrade).

## Notes

- `STEAM_GUARD` can be supplied when Steam prompts for a guard code.
- `FORCE_STEAMCMD_UPDATE=true` forces a fresh game update on the next run.
- The Docker entrypoint launches with Steam authentication and Steam Networking Sockets enabled. Startup fails if required Steam runtime DLLs such as `steam_api64.dll` are missing.
- Persist `/home/steam/game` so the installed game files and generated config survive container recreation. Runtime bootstrap state such as the Wine prefix is managed outside this directory by the image.
- The Compose example also persists `/home/steam/steamcmd` and `/home/steam/Steam` to avoid re-downloading Steam runtime data on every container recreation.
- Leave `STEAM_BRANCH` unset unless you intentionally need a non-default branch for the selected runtime.
- Switching `S1DS_RUNTIME` on an existing persistent game volume is supported; the container removes the other runtime's DedicatedServerMod DLL before starting.
- Edit the generated `server_config.toml` under the mounted game directory after first boot.
- Publish `serverPort` on both UDP and TCP. UDP is gameplay traffic; TCP is DedicatedServerMod's status query endpoint.
- If you use `SteamGameServer`, also publish `steamGameServerQueryPort` on UDP.
- Only publish `tcpConsolePort` on TCP when `[tcpConsole].tcpConsoleEnabled = true` and you intentionally bind the console beyond localhost.
- Do not publish `webPanelPort`. The built-in web panel is loopback-only for security and is not intended for access from another LAN machine or the public internet. Use the TCP console with authentication, stdio through a hosted panel such as Pterodactyl, or a custom authenticated web panel built on top of a supported control surface for remote administration.
- For auth and networking guidance inside containers, see the docs site Docker page and the authentication/messaging backend guides.
