# S1DS - Dedicated Servers

Version: {{VERSION}}<br>
Runtime: {{RUNTIME}}<br>
Side: Server

S1DedicatedServers adds a headless server flow for Schedule I, plus the client package players need to connect to dedicated servers. It is built for communities that want a world running 24/7 with admin tools, save/load support, permissions, hosted-console support, and a public API for server/client companion mods.

![S1DedicatedServers web panel](https://raw.githubusercontent.com/ifBars/S1DedicatedServers/master/marketing/src/assets/web-panel.png)

## Core features

- **Dedicated server launch flow:** run Schedule I in a server-focused/headless setup.
- **Save/load support:** keep long-running worlds persistent across server restarts.
- **Admin tools:** manage players, permissions, moderation, and server commands.
- **Hosted console support:** works with local terminals and Pterodactyl-style hosting panels.
- **Optional local web panel:** monitor and operate a local server from a loopback-only panel.
- **Mono and IL2CPP support:** server and client packages are available for both runtimes.
- **Docker package:** available from GitHub releases for containerized hosting.

## For server owners

This package is for {{RUNTIME}} server installs. It is meant for communities, test servers, private persistent worlds, and hosted servers.

The server package includes `start_server.bat`. On first boot, the server generates its configuration files in `UserData` so you can tune authentication, ports, saves, permissions, console behavior, and other server details before opening the server to players.

## Basic install

1. Install MelonLoader for the {{RUNTIME}} Schedule I runtime.
2. Extract this archive into the Schedule I install root so the included `Mods` folder merges into the game folder.
3. Confirm `Mods\{{DLL_NAME}}` exists after extraction.
4. Run `start_server.bat` from the game root.
5. Let the server boot once so it can generate `server_config.toml` and related files.
6. Edit your save path, authentication settings, permissions, and ports before opening the server publicly.

If you host and play on the same PC, launch the normal Schedule I client before starting the Steam game server.

## Important: pick the right file

- **Server owners:** download a **Server** package for the runtime your server uses.
- **Players:** download the matching **Client** package for the runtime used by the server they join.
- **Do not install a Server package** if you only want to join someone else's dedicated server.

## Useful links

- [Documentation and quick start](https://docs.s1servers.com/)
- [Managed server hosting](https://docs.s1servers.com/docs/hosting-providers.html)
- [GitHub repository](https://github.com/ifBars/S1DedicatedServers)
- [GitHub releases](https://github.com/ifBars/S1DedicatedServers/releases)
- [Issues and support](https://github.com/ifBars/S1DedicatedServers/issues)

This mod is not officially affiliated with or endorsed by TVGS or the developers of Schedule I.
