## Docker Deployment

Use the Docker release package when you want to run the dedicated server in a container instead of unpacking the server directly onto a Windows host.

The Docker release assets intentionally do **not** ship:

- MelonLoader files
- `DedicatedServerMod_Mono_Server.dll`
- a preinstalled copy of Schedule I

That keeps the Docker package small and avoids redistributing loader payloads inside the Docker template.

## What To Download

Download both of these assets from the same release:

1. `Docker.zip`
2. `Server.zip`

`Docker.zip` contains:

- `Dockerfile`
- `run.sh`
- `.dockerignore`
- package-local Docker instructions

`Server.zip` contains the server mod DLL that the image needs during `docker build`.

## Prepare The Build Context

Extract `Docker.zip` to a working folder, then copy `Mods/DedicatedServerMod_Mono_Server.dll` from `Server.zip` into that same folder so the layout looks like this:

```text
Docker/
  .dockerignore
  Dockerfile
  docker-compose.example.yml
  run.sh
  README.md
  DedicatedServerMod_Mono_Server.dll
```

The Docker image downloads MelonLoader during `docker build`, so users should not add MelonLoader files manually.

## Build The Image

```bash
docker build -t s1ds-dedicated-server .
```

If `DedicatedServerMod_Mono_Server.dll` is missing from the folder, the build will fail during the Docker `COPY` step.

## First Run

You can start the container with either a direct `docker run` command or Docker Compose.

### `docker run`

The first container start installs Schedule I through SteamCMD, so provide Steam credentials and persist the game directory:

```bash
docker run --name s1ds \
  -p 38465:38465/udp \
  -p 27016:27016/udp \
  -e STEAM_USER=your_steam_login \
  -e STEAM_PASS=your_steam_password \
  -e STEAM_BRANCH=alternate \
  -v s1ds-game:/home/steam/game \
  s1ds-dedicated-server
```

Optional environment variables:

- `STEAM_GUARD` for Steam Guard prompts
- `FORCE_STEAMCMD_UPDATE=true` to force the next startup to refresh the game install

### Docker Compose

The release package includes `docker-compose.example.yml` for Compose-based deployments. Copy it to `docker-compose.yml`, then add a `.env` file in the same folder:

```env
STEAM_USER=your_steam_login
STEAM_PASS=your_steam_password
STEAM_BRANCH=alternate
# Optional on first login when Steam prompts for a code.
STEAM_GUARD=
# Optional; set to true when you want to force a fresh update.
FORCE_STEAMCMD_UPDATE=false
```

Then build and start the service:

```bash
docker compose up -d --build
```

## After First Boot

- Edit the generated `server_config.toml` in the mounted game directory
- Keep your save path, auth settings, and query port aligned with your deployment
- Rebuild the image with a newer `DedicatedServerMod_Mono_Server.dll` when upgrading to a new release

## Container Notes

- `SteamGameServer` is the preferred authentication provider for containerized deployments
- `SteamNetworkingSockets` is the preferred messaging backend for containerized deployments
- `SteamP2P` should not be used in Docker
- The image reapplies MelonLoader and the server mod DLL on container start because SteamCMD validation may overwrite game files
- The Compose example also persists `/home/steam/steamcmd` and `/home/steam/Steam` so SteamCMD downloads and runtime data survive container recreation

## Related Documentation

- [Configuration](configuration.md)
- [Authentication](configuration/authentication.md)
- [Messaging Backends](configuration/messaging-backends.md)
- [Host Console](host-console.md)
