## Schedule I Dedicated Server

Run a headless, authoritative server for Schedule I using the Dedicated Server Mod. The server replicates the original host flow (network, loopback client, save/load, quests, time, sleep) and is designed for long-running worlds.

Key docs:

- [Quick Start](../index.md)
- [Docker Deployment](docker.md)
- [Hosting Providers](hosting-providers.md)
- [Configuration](configuration.md)
- [Web Panel](configuration/web-panel.md)
- [Permissions](configuration/permissions.md)
- [Host Console](host-console.md)
- [Client Mod Verification](configuration/client-mod-verification.md)
- [Commands](commands.md)
- [Mod API Overview](modding/overview.md)
- [Addon Configuration](modding/configuration.md)
- [Companion Mods and Verification Metadata](modding/companion-mods.md)
- [Troubleshooting](troubleshooting.md)

### Notes

- Please search existing issues before opening a new one; if you don't see your problem reported, opening an issue on GitHub is encouraged.
- Mono and IL2CPP are both supported. If you hit runtime-specific issues, please open a GitHub issue and include logs, runtime type, and reproduction steps.
- Docker deployment now supports both Mono and IL2CPP through the same image and release package by setting `S1DS_RUNTIME`.
- Cybrancee and Kinetic Hosting are supported providers, with Cybrancee as the recommended hosted option; see [Hosting Providers](hosting-providers.md) for provider status, verification notes, and third-party host guidance.
- Reproduction steps, logs, and any installed mods details are extremely helpful.
- Feedback, suggestions, and pull requests are welcome and appreciated.

Thanks for helping build open source Schedule I servers!

