## Docker Deployment

Use the Docker release package when you want to run the dedicated server in a container instead of unpacking the server directly onto a Windows host.

Docker deployment supports both Schedule I server runtimes:

- `mono` uses the Schedule I `alternate` branch
- `il2cpp` uses the default Schedule I branch

Set `S1DS_RUNTIME` to choose the runtime. If you do not set it, the container defaults to `mono`.

`STEAM_BRANCH` is optional. Leave it unset to use the runtime default branch automatically. Set it only when you intentionally want a different branch.

## Published Image

Stable releases publish `latest` and the exact version tag to `ghcr.io/ifbars/s1dedicatedservers`. Prereleases publish the exact version tag only, without the leading `v` from the GitHub release tag.

Pull the current image with:

```bash
docker pull ghcr.io/ifbars/s1dedicatedservers:1.1.6
```

Use `latest` only after a stable release has been published:

```bash
docker pull ghcr.io/ifbars/s1dedicatedservers:latest
```

Run the published image directly with Mono:

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

Run the published image directly with IL2CPP:

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

The included Compose example also pulls from GHCR by default now.

The examples pin 1.1.6 rather than the older 1.0.0 image, which predates the save/shutdown fixes. For later upgrades, choose an exact published release tag and use client packages from that same release.

## Build From Release Assets

Download `Docker.zip` from the same release and extract it into a working folder.

The release package includes:

- `Dockerfile`
- `run.sh`
- `server-lifecycle.sh`
- `.dockerignore`
- `docker-compose.example.yml`
- `DedicatedServerMod_Mono_Server.dll`
- `DedicatedServerMod_Il2cpp_Server.dll`
- package-local Docker instructions

The Docker image downloads MelonLoader during `docker build`, so users should not add MelonLoader files manually. It runs on the stable Wine 11 base and installs the .NET 6 desktop runtime into an image-managed Wine prefix during build so IL2CPP MelonLoader startup does not need to run its first-launch runtime installer. The Wine prefix is kept outside `/home/steam/game`, so bind-mounting or persisting the game directory does not hide the preinstalled runtime.

## Build The Image

```bash
docker build -t s1ds-dedicated-server .
```

The local image can run either runtime. Runtime selection happens when the container starts, not when the image is built.

## First Run

You can start the container with either a direct `docker run` command or Docker Compose.

### `docker run`

The first container start installs Schedule I through SteamCMD, so provide Steam credentials and persist the game directory.

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

Optional environment variables:

- `STEAM_GUARD` for Steam Guard prompts
- `FORCE_STEAMCMD_UPDATE=true` to force the next startup to refresh the game install
- `STEAM_BRANCH` when you intentionally want to override the runtime default branch
- Startup fails if required Steam runtime DLLs such as `steam_api64.dll` are missing. The Docker entrypoint launches with Steam authentication and Steam Networking Sockets enabled.

### Docker Compose

The release package includes `docker-compose.example.yml` for Compose-based deployments. Copy it to `docker-compose.yml`, then add a `.env` file in the same folder:

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

Then pull and start the service:

```bash
docker compose up -d
```

To build locally from `Docker.zip` instead of pulling from GHCR, replace the `image:` line in the Compose file with the commented `build:` block before running `docker compose up -d --build`.

## UTF-8 Launch Environment

The examples explicitly pass `LANG=C.UTF-8` and `LC_ALL=C.UTF-8` to the container. The supplied Compose file reads them from `.env` through `env_file`; a `.env` file alone does not inject variables into an arbitrary third-party Compose service. For a custom image or hosted panel, confirm that this locale is installed and that both variables reach the Wine/game process, or choose another installed UTF-8 locale.

This prevents relying on inherited host/image locale defaults. [Issue #71](https://github.com/ifBars/S1DedicatedServers/issues/71) reported save failures at `NPCs/Javier Pérez` that were resolved by setting these variables. The same path was later reported in [#78](https://github.com/ifBars/S1DedicatedServers/issues/78), where the affected service's resolution is still unconfirmed. Locale changes apply on the next launch; follow [save-failure recovery](troubleshooting/save-load.md#saves-fail-or-shutdown-is-cancelled) before restarting a world that cannot save.

## Stopping and Restarting

Images built from the current source force the S1DS stdio console on and keep a private command pipe open even when Docker provides no stdin. On SIGTERM/SIGINT, the lifecycle supervisor writes `shutdown` once and waits for game process exit, keeping Xvfb alive throughout the final save. Repeated stop signals do not kill the game or queue duplicate shutdowns. Attached/panel stdin is forwarded when available; EOF does not close the private pipe. The wrapper returns the game's exit code.

The Compose example sets `stop_grace_period: 2m`; direct `docker run` examples use `--stop-timeout 120`. Docker can still send SIGKILL after this timeout, so allow more time for large worlds or slow storage. To request a stop without Docker's forced-kill deadline, use `docker stop --timeout -1 s1ds`. See Docker's [stop timeout reference](https://docs.docker.com/reference/cli/docker/container/stop/#timeout) and [Compose stop grace period](https://docs.docker.com/reference/compose-file/services/#stop_grace_period).

If the final save fails, S1DS cancels shutdown and the supervisor continues waiting. Investigate [the native persistence errors](troubleshooting/save-load.md#saves-fail-or-shutdown-is-cancelled), repair the cause, and send a new `shutdown` through the configured TCP console or attached stdin. Repeated Docker stop signals are not a save retry. With an unlimited timeout, stopping will remain pending until the game exits.

This handler is not present in the already-published 1.1.6 image used by the pinned examples. Build from source to use it until a release containing it is published. On older images, manually send `shutdown` through the configured console and wait for `Save completed: server_shutdown`, `=== Server Shutdown Complete ===`, and game exit before stopping/recreating the container. Increasing an old image's timeout alone does not add graceful shutdown. Use a disposable/copied world for the first stop/restart test.

## IL2CPP Interop Cache Cleanup

After SteamCMD and the pinned MelonLoader bootstrap are applied, and before Wine starts Schedule I, the entrypoint confirms the game is not running and removes only this disposable Cpp2IL output directory:

```text
/home/steam/game/MelonLoader/Dependencies/Il2CppAssemblyGenerator/Cpp2IL/cpp2il_out
```

This cleanup is safe and idempotent on every startup. It does not remove `MelonLoader/Il2CppAssemblies`, `Mods`, configuration, saves, or logs. It prevents Il2CppInterop from consuming assemblies that a game update removed but a persistent game volume retained in Cpp2IL output.

### Troubleshooting IL2CPP Interop Generation After A Game Update

If startup reports an Il2CppInterop generation error such as `Pass11ComputeTypeSpecifics`, stop the server, delete only the `cpp2il_out` directory shown above, then start the server so it regenerates the output. The container performs this recovery automatically on every startup.

Do not use a MelonLoader downgrade or reinstall as the primary fix: the inspected `0.7.0` through `0.7.3` releases share the stale-output behavior, and reinstalling often appears to help only because it removes this directory. The image remains pinned to the currently tested MelonLoader `0.7.2` until an upstream release containing [MelonLoader PR #1193](https://github.com/LavaGang/MelonLoader/pull/1193) is tested.

## After First Boot

Complete the [save/shutdown/reload verification checklist](configuration/startup-and-deployment.md#verify-a-new-install-or-upgrade) before inviting players or treating this deployment as a known-good backup source. A successful world load or updated file timestamp does not establish that every save file was written.

- Edit the generated `server_config.toml` in the mounted game directory
- Keep your save path, auth settings, and port publishing aligned with your deployment
- Publish `serverPort` on both UDP and TCP. UDP is gameplay traffic; TCP is DedicatedServerMod's status query endpoint.
- If you use `SteamGameServer`, also publish `steamGameServerQueryPort` on UDP.
- Only publish `tcpConsolePort` on TCP when you explicitly enable the TCP console and bind it beyond localhost.
- Do not publish `webPanelPort`. The built-in web panel is loopback-only for security and is not intended for access from another LAN machine or the public internet. Use the TCP console with authentication, stdio through a hosted panel such as Pterodactyl, or a custom authenticated web panel built on top of a supported control surface for remote administration.
- Pull a newer GHCR image tag or rebuild locally from a newer `Docker.zip` when upgrading to a new release
- Leave `STEAM_BRANCH` unset unless you intentionally need a non-default branch for the selected runtime

## Container Notes

- `SteamGameServer` is the preferred authentication provider for containerized deployments
- `FishNetRpc` is the simplest messaging backend for containerized deployments
- Switch to `SteamNetworkingSockets` only when you specifically want Steam relay or Steam-side routing behavior
- The image reapplies MelonLoader and the selected server mod DLL on container start because SteamCMD validation may overwrite game files
- Switching `S1DS_RUNTIME` on an existing persistent game volume is supported; the container removes the other runtime's DedicatedServerMod DLL before starting
- The Compose example also persists `/home/steam/steamcmd` and `/home/steam/Steam` so SteamCMD downloads and runtime data survive container recreation

## Related Documentation

- [Configuration](configuration.md)
- [Authentication](configuration/authentication.md)
- [Messaging Backends](configuration/messaging-backends.md)
- [Host Console](host-console.md)
