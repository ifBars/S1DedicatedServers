---
title: Hosting Providers
description: Supported hosting providers and third-party hosting guidance for S1DedicatedServers.
---

# Hosting Providers

S1DedicatedServers is an open-source project. Players can self-host it directly, use the official Docker package, or rent a hosted server from a provider that supports the mod.

This page is the source of truth for supported hosted-provider guidance. Providers not listed here should be treated as independent third-party hosts.

Some provider links may be affiliate links. Using them can support continued S1DedicatedServers development, but commercial hosting is optional and never required to use the mod.

## Supported Providers

Want a hosted server instead of running the dedicated server yourself? The providers below have been tested by the maintainer with S1DedicatedServers. Cybrancee is the recommended option and the provider used for ongoing hosted-server validation.

<div class="hosting-provider-showcase">
  <section id="cybrancee" class="hosting-provider-primary" aria-label="Cybrancee supported provider">
    <div class="hosting-provider-primary__body">
      <div class="hosting-provider-brand">
        <p class="hosting-provider-eyebrow">Recommended provider</p>
        <a class="hosting-provider-logo-link" href="https://cybrancee.com/bars" aria-label="Open Cybrancee">
          <img class="hosting-provider-logo hosting-provider-logo--light-theme" src="assets/cybrancee-logo-light.png" alt="Cybrancee" />
          <img class="hosting-provider-logo hosting-provider-logo--dark-theme" src="assets/cybrancee-logo-dark.png" alt="Cybrancee" />
        </a>
      </div>
      <div class="hosting-provider-details">
        <p>
          Confirmed for supported hosting, with an ongoing maintainer
          test server available for future S1DedicatedServers updates.
        </p>
        <div class="hosting-provider-actions">
          <a class="hosting-provider-button" href="https://cybrancee.com/bars">Visit Cybrancee</a>
        </div>
      </div>
    </div>
  </section>

  <section id="kinetic-hosting" class="hosting-provider-secondary" aria-label="Kinetic Hosting supported provider">
    <div class="hosting-provider-secondary__body">
      <div class="hosting-provider-brand">
        <p class="hosting-provider-eyebrow">Supported provider</p>
        <a class="hosting-provider-logo-link hosting-provider-logo-link--kinetic" href="https://billing.kinetichosting.com/aff.php?aff=1417" aria-label="Open Kinetic Hosting">
          <img class="hosting-provider-logo hosting-provider-logo--light-theme" src="assets/kinetic-logo-light.svg" alt="Kinetic Hosting" />
          <img class="hosting-provider-logo hosting-provider-logo--dark-theme" src="assets/kinetic-logo-dark.svg" alt="Kinetic Hosting" />
        </a>
      </div>
      <div class="hosting-provider-details">
        <p>
          Confirmed working during maintainer setup testing and listed as a
          compatible hosted option for S1DedicatedServers.
        </p>
        <div class="hosting-provider-actions">
          <a class="hosting-provider-button" href="https://billing.kinetichosting.com/aff.php?aff=1417">Visit Kinetic Hosting</a>
        </div>
      </div>
    </div>
  </section>
</div>

## Self-Hosting

Self-hosting remains the baseline deployment path. Use the release packages and docs below when you want to control the install directly:

- [Quick Start](../index.md)
- [Docker Deployment](docker.md)
- [Configuration Overview](configuration.md)
- [Startup and Deployment](configuration/startup-and-deployment.md)

## Third-Party Hosting

Hosting providers other than Cybrancee and Kinetic may advertise Schedule I or S1DedicatedServers-compatible hosting. These providers operate independently and may use their own panels, packaging, update timing, support processes, and compatibility assumptions.

Unless this page lists a provider as recommended or verified, do not treat third-party listings, provider documentation, control-panel templates, bundled installs, or customer support claims as project verification or endorsement.

Before buying hosted service, compare the details that affect real server quality:

- **Runtime support:** confirm which S1DedicatedServers version is installed and whether the provider supports Mono, IL2CPP, or both. Public provider pages may not state this clearly, even when the provider supports Schedule I or general game hosting.
- **CPU performance:** prioritize modern high-clock CPUs. Schedule I dedicated servers are more sensitive to CPU speed than raw RAM once you have enough memory for your player count and mods.
- **RAM sizing:** choose more RAM for larger communities, heavier saves, and more mods. RAM matters, but extra RAM will not fix a weak CPU or overloaded node.
- **Storage and backups:** prefer NVMe storage, visible file access, and clear backup/restore controls for saves, configs, and logs.
- **Panel access:** confirm you can access logs, `server_config.toml`, `permissions.toml`, save files, and a supported operator surface such as the stdio host console or a host-provided console.
- **Updates:** ask how quickly new S1DedicatedServers releases are packaged and whether you can manually upload or pin a version.
- **Networking:** confirm the required gameplay, status query, and Steam query/listing ports can be exposed.
- **Locations and routing:** choose the closest region to your players, and check whether the provider publishes location or ping-test information.
- **Support and reputation:** review the provider's support channels, response expectations, refund policy, status page, and recent public reviews before committing.

Public provider notes:

- **Cybrancee:** publicly advertises game servers on Ryzen CPUs over 4GHz with NVMe SSDs, a customized Pterodactyl panel for game hosting, DDoS protection, backups, international locations, 24/7 support, and a 90-day money-back guarantee. Its Trustpilot profile is currently rated 4.8 Excellent.
- **Kinetic Hosting:** publicly lists Schedule 1 among supported games and advertises performance packages with NVMe SSDs, up to Ryzen 9 9950X at 5.7GHz, 6-8 CPU threads on common 8GB/16GB packages, default 60GB storage with free storage upgrades under fair use, backups, split servers, game swapping, a custom panel, 15 locations, 24/7 human support, public node stats, and a 7-day refund window. Its Trustpilot profile is currently rated 4.8 Excellent.

Because provider pages change, verify current package specs and S1DedicatedServers runtime support with the provider before purchase.

For hosted control panels, [Host Console](host-console.md) is usually the right operator path.

## For Commercial Hosting Providers

If you offer commercial S1DedicatedServers hosting, contact the project maintainer for setup guidance, version coordination, attribution guidelines, and partnership or affiliate options.

Commercial providers should:

- attribute the project as `S1DedicatedServers` / `DedicatedServerMod` by `ifBars`
- link users to the official releases and documentation
- keep bundled builds current with public releases
- make the installed runtime and version visible to customers
- avoid implying supported-provider, recommended, affiliate, sponsor, official, or exclusive status unless a written arrangement exists

Partnership, affiliate, sponsorship, supported-provider, and recommended-host placements should be disclosed clearly wherever they appear.

## Related Documentation

- [Host Console](host-console.md)
- [Docker Deployment](docker.md)
- [Startup and Deployment](configuration/startup-and-deployment.md)
- [Configuration Overview](configuration.md)
- [Troubleshooting](troubleshooting.md)
