## Documentation Maintenance

Use this page when updating Markdown docs, XML comments, examples, or release-facing wording. The docs should describe the implementation that exists in the current branch, not the shape we expect the project to grow into.

## Source Of Truth Map

| Docs area | Check these files first | Notes |
| --- | --- | --- |
| Version, release channel, package tags | `API/Version.cs`, `.github/workflows/auto-release.yml`, `README.md`, `docfx/docs/docker.md`, `Docker/README.md` | `API/Version.cs` owns `ModVersion`. A `-beta`, `-alpha`, or other suffix controls prerelease behavior. |
| Server configuration keys | `Shared/Configuration/ServerConfig.cs`, `Shared/Configuration/ServerConfigSchema.cs`, `webpanel/src/features/config/configSchema.ts` | Markdown examples should use TOML section names from `ServerConfigSchema`. |
| Game behavior | `../GameTime/TimeManager.cs`, `../PlayerScripts/Player.cs`, `../UI/SleepCanvas.cs`, `../Persistence/LoadManager.cs`, `../Persistence/SaveManager.cs` | Verify native time, sleep, load, and save behavior against the adjacent Schedule I source before documenting operator behavior. |
| Dedicated save preparation | `Server/Core/ServerStartupOrchestrator.cs`, `Server/Persistence/SaveInitializer.cs`, `Shared/Configuration/ServerConfig.cs` | Empty `saveGamePath` resolves to `UserData/DedicatedServerSave`; the server attempts to seed missing core save files before loading. |
| Command-line overrides | `Server/Core/ServerBootstrap.cs`, `Shared/Configuration/ServerConfig.cs`, `Client/Managers/ClientConnectionManager.cs` | Server bootstrap handles `--dedicated-server` / `--server`; config parsing handles server settings; client connection parsing handles `--server-ip` and `--server-port`. |
| Built-in commands | `Server/Commands/CommandManager.cs`, `Server/Commands/BuiltIn/**` | Command pages should match registered command words, usage strings, and required permission nodes. |
| Permissions | `Server/Permissions/PermissionDefaults.cs`, `Shared/Permissions/PermissionNode.cs`, `Server/Permissions/PermissionTomlMapper.cs` | Prefer durable nodes such as `server.stop` and `permissions.reload` over generic command nodes when the command exposes a dedicated node. |
| Client mod verification | `API/Metadata/**`, `Shared/ModVerification/**`, `Server/Player/ClientModVerificationManager.cs` | State clearly that this is a join-policy gate, not mod distribution or a full anti-cheat. |
| Messaging backends | `Shared/CustomMessaging.cs`, `Shared/Networking/Messaging/**`, `Shared/Configuration/ServerConfig.cs` | Mod-facing APIs stay the same when backend selection changes. |
| Mod API guide and generated reference | `API/**`, `Examples/**`, `docfx/docfx.json`, `docfx/filterConfig.yml` | Public and protected APIs need XML comments before the reference is regenerated. |
| Docker deployment | `Docker/**`, `.github/workflows/auto-release.yml`, `docfx/docs/docker.md` | Keep `Docker/README.md` and the docs-site Docker page aligned. |

## Markdown Rules

- Start operator docs with the safe default, then explain alternatives.
- Use exact TOML keys, command names, and permission nodes from code.
- Keep examples runnable or clearly marked as partial snippets.
- Avoid documenting internal implementation types as extension points unless they are intentionally public.
- When a setting is legacy or migration-only, say what replaces it and whether it is still parsed.
- Keep Docker, README, and docs-site install instructions synchronized when release packaging changes.

## XML Comment Rules

- Public and protected APIs should have a concise summary and useful parameter/return text.
- Add remarks when the method is side-aware, lifecycle-sensitive, or has security implications.
- Prefer examples for public extension points such as mod lifecycle, TOML config, status-query handlers, and client join preparation.
- Do not expose raw Unity, FishNet, or Il2Cpp behavior as a stable contract unless the API is deliberately designed that way.
- If an API is obsolete or compatibility-only, say what new code should use instead.

## Validation Checklist

Before considering docs done:

- Build the relevant project configuration when XML comments or public API files changed.
- Run `docfx docfx/docfx.json` after Markdown, TOC, XML, or API-reference changes.
- Review DocFX warnings for broken links, unresolved xrefs, or missing generated metadata.
- If a page contains command output, file paths, ports, or version tags, confirm them against the current branch.
- If docs mention Docker packaging, confirm both `docfx/docs/docker.md` and `Docker/README.md`.
