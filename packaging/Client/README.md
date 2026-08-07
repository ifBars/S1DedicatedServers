# S1DS - Dedicated Servers

Version: {{VERSION}}<br>
Runtime: {{RUNTIME}}<br>
Side: Client

S1DedicatedServers adds a headless server flow for Schedule I, plus the client package players need to connect to dedicated servers. It is built for communities that want a world running 24/7 with admin tools, save/load support, permissions, hosted-console support, and a public API for server/client companion mods.

![S1DedicatedServers documentation](https://raw.githubusercontent.com/ifBars/S1DedicatedServers/master/marketing/src/assets/docs-site.png)

## Core features

- **Dedicated server launch flow:** supports Schedule I dedicated servers.
- **Client connection support:** lets players join S1DedicatedServers hosts.
- **Server/client companion mod support:** enables dedicated-server-aware companion mods.
- **Mono and IL2CPP support:** server and client packages are available for both runtimes.
- **Public API:** available for addon authors building server/client integrations.

## For players

This package is for {{RUNTIME}} game clients joining {{RUNTIME}} dedicated servers. Most players only need the matching Client package for the runtime used by the server they join.

Do not install a Server package if you only want to join someone else's dedicated server.

## Basic install

1. Install MelonLoader for the {{RUNTIME}} Schedule I runtime.
2. Extract this archive into the Schedule I install root so the included `Mods` folder merges into the game folder.
3. Confirm `Mods\{{DLL_NAME}}` exists after extraction.
4. Launch the game normally and connect to a dedicated server.

## File guide

- **Mono Server:** for a Mono dedicated server install.
- **Mono Client:** for Mono game clients joining Mono dedicated servers.
- **IL2CPP Server:** for an IL2CPP dedicated server install.
- **IL2CPP Client:** for IL2CPP game clients joining IL2CPP dedicated servers.

## Useful links

- [Documentation and quick start](https://docs.s1servers.com/)
- [Managed server hosting](https://docs.s1servers.com/docs/hosting-providers.html)
- [GitHub repository](https://github.com/ifBars/S1DedicatedServers)
- [GitHub releases](https://github.com/ifBars/S1DedicatedServers/releases)
- [Issues and support](https://github.com/ifBars/S1DedicatedServers/issues)

This mod is not officially affiliated with or endorsed by TVGS or the developers of Schedule I.
