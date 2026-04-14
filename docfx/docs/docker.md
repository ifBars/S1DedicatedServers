## Docker Deployment

Use the Docker release package when you want to run the dedicated server in a container instead of unpacking the server directly onto a Windows host.

Docker deployment supports both Schedule I server runtimes:

- `mono` uses the Schedule I `alternate` branch
- `il2cpp` uses the default Schedule I branch

Set `S1DS_RUNTIME` to choose the runtime. If you do not set it, the container defaults to `mono`.

`STEAM_BRANCH` is optional. Leave it unset to use the runtime default branch automatically. Set it only when you intentionally want a different branch.

## Published Image

Stable releases publish `latest` and the exact version tag to `ghcr.io/ifbars/s1dedicatedservers`. Prereleases publish the exact version tag only.

Pull the stable image with:

```bash
docker pull ghcr.io/ifbars/s1dedicatedservers:latest
```

Or pin a specific release:

```bash
docker pull ghcr.io/ifbars/s1dedicatedservers:0.9.0-beta
```

Run the published image directly with Mono:

```bash
docker run --name s1ds \
  -p 38465:38465/udp \
  -p 27016:27016/udp \
  -e STEAM_USER=your_steam_login \
  -e STEAM_PASS=your_steam_password \
  -e S1DS_RUNTIME=mono \
  -v s1ds-game:/home/steam/game \
  ghcr.io/ifbars/s1dedicatedservers:latest
```

Run the published image directly with IL2CPP:

```bash
docker run --name s1ds \
  -p 38465:38465/udp \
  -p 27016:27016/udp \
  -e STEAM_USER=your_steam_login \
  -e STEAM_PASS=your_steam_password \
  -e S1DS_RUNTIME=il2cpp \
  -v s1ds-game:/home/steam/game \
  ghcr.io/ifbars/s1dedicatedservers:latest
```

The included Compose example also pulls from GHCR by default now.

## Build From Release Assets

Download `Docker.zip` from the same release and extract it into a working folder.

The release package includes:

- `Dockerfile`
- `run.sh`
- `.dockerignore`
- `docker-compose.example.yml`
- `DedicatedServerMod_Mono_Server.dll`
- `DedicatedServerMod_Il2cpp_Server.dll`
- package-local Docker instructions

The Docker image downloads MelonLoader during `docker build`, so users should not add MelonLoader files manually.

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
docker run --name s1ds \
  -p 38465:38465/udp \
  -p 27016:27016/udp \
  -e STEAM_USER=your_steam_login \
  -e STEAM_PASS=your_steam_password \
  -e S1DS_RUNTIME=mono \
  -v s1ds-game:/home/steam/game \
  s1ds-dedicated-server
```

IL2CPP example:

```bash
docker run --name s1ds \
  -p 38465:38465/udp \
  -p 27016:27016/udp \
  -e STEAM_USER=your_steam_login \
  -e STEAM_PASS=your_steam_password \
  -e S1DS_RUNTIME=il2cpp \
  -v s1ds-game:/home/steam/game \
  s1ds-dedicated-server
```

Optional environment variables:

- `STEAM_GUARD` for Steam Guard prompts
- `FORCE_STEAMCMD_UPDATE=true` to force the next startup to refresh the game install
- `STEAM_BRANCH` when you intentionally want to override the runtime default branch

### Docker Compose

The release package includes `docker-compose.example.yml` for Compose-based deployments. Copy it to `docker-compose.yml`, then add a `.env` file in the same folder:

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

Then pull and start the service:

```bash
docker compose up -d
```

To build locally from `Docker.zip` instead of pulling from GHCR, replace the `image:` line in the Compose file with the commented `build:` block before running `docker compose up -d --build`.

## After First Boot

- Edit the generated `server_config.toml` in the mounted game directory
- Keep your save path, auth settings, and query port aligned with your deployment
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
