---
title: Hosting Providers
description: Guidance for players and commercial hosts using S1DedicatedServers through third-party hosting providers.
---

# Hosting Providers

S1DedicatedServers is an open-source project. Players can self-host it directly, and commercial hosting providers may independently offer servers that include or support it.

Unless this page explicitly marks a provider as official, hosting providers are independent third parties. They are not affiliated with, endorsed by, sponsored by, or commercially partnered with this project.

## Current Partner Status

This project does not currently publish an official or recommended commercial hosting partner.

Avoid treating third-party listings, provider documentation, control-panel templates, or bundled installs as an endorsement unless they are linked from this page as an official partner or affiliate placement.

## Self-Hosting

Self-hosting remains the baseline deployment path. Use the release packages and docs below when you want to control the install directly:

- [Quick Start](../index.md)
- [Docker Deployment](docker.md)
- [Configuration Overview](configuration.md)
- [Startup and Deployment](configuration/startup-and-deployment.md)

## Third-Party Hosting

Some third-party hosting providers may advertise Schedule I or S1DedicatedServers-compatible hosting. These providers operate independently and may use their own panels, packaging, update timing, support processes, and compatibility assumptions.

Before buying hosted service, check:

- which S1DedicatedServers version and runtime the provider installs
- whether the provider supports Mono, IL2CPP, or both
- how updates are applied after new releases
- whether logs, `server_config.toml`, `permissions.toml`, and save files are accessible
- whether the panel exposes the stdio host console or another supported operator surface
- which network ports are exposed for gameplay, status query, and Steam query/listing

For hosted control panels, [Host Console](host-console.md) is usually the right operator path.

## For Commercial Hosting Providers

If you offer commercial S1DedicatedServers hosting, contact the project maintainer for setup guidance, version coordination, attribution guidelines, and partnership or affiliate options.

Commercial providers should:

- attribute the project as `S1DedicatedServers` / `DedicatedServerMod` by `ifBars`
- link users to the official releases and documentation
- keep bundled builds current with public releases
- make the installed runtime and version visible to customers
- avoid implying official partner, affiliate, sponsor, recommended host, or exclusive status unless a written arrangement exists

Partnership, affiliate, sponsorship, or recommended-host placements should be disclosed clearly wherever they appear.

## Related Documentation

- [Host Console](host-console.md)
- [Docker Deployment](docker.md)
- [Startup and Deployment](configuration/startup-and-deployment.md)
- [Configuration Overview](configuration.md)
- [Troubleshooting](troubleshooting.md)
