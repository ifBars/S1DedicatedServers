# DedicatedServerMod

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)](https://github.com/ifBars/S1DedicatedServers)
[![Version](https://img.shields.io/badge/version-0.2.1--beta-blue)](https://github.com/ifBars/S1DedicatedServers/releases)
[![License](https://img.shields.io/github/license/ifBars/S1DedicatedServers?color=green)](LICENSE)

**Professional dedicated server framework for Schedule I** - Run authoritative, headless servers with full multiplayer support, admin management, and extensive modding capabilities.

---

## 🎯 Overview

DedicatedServerMod transforms Schedule I into a multiplayer-ready game with dedicated server support. It provides a complete framework for:

- **Headless Server Operation**: Run servers without graphics/UI overhead
- **Client-Server Architecture**: Authoritative server with synchronized clients
- **Admin & Permission System**: Operators, admins, and granular command permissions
- **TCP Remote Console**: Manage servers remotely via telnet/SSH
- **Custom Messaging API**: Build server and client mods with bidirectional communication
- **Save/Load Management**: Automated saves, backup systems, and persistence
- **Time & Sleep Systems**: Server-controlled time progression and sleep cycling
- **Extensive Configuration**: JSON-based config with command-line overrides

---

## ✨ Key Features

### Server Features
- 🖥️ **True Dedicated Server**: Headless operation with minimal resource usage
- 👥 **Multi-Player Support**: Up to 16 players (configurable)
- 🔐 **Advanced Permissions**: Three-tier system (operators, admins, players)
- 📡 **TCP Console**: Remote server management and monitoring
- 💾 **Auto-Save System**: Configurable intervals with player event triggers
- ⏰ **Time Management**: Never-stop time option with multiplier control
- 🌙 **Sleep Cycling**: Server-controlled sleep with ghost host support
- 🔌 **Master Server Integration**: Optional server list registration
- 📊 **Performance Monitoring**: Built-in metrics and logging

### Client Features
- 🎮 **Seamless Connection**: Connect to dedicated servers like normal multiplayer
- 🖼️ **Enhanced UI**: Dedicated server indicators and admin console
- 📨 **Custom Messaging**: Bidirectional communication with server mods
- 🛠️ **Admin Tools**: In-game admin console for permitted players
- 🔄 **Server Data Sync**: Automatic sync of server configuration and state

### Modding API
- 📦 **Server Mod API**: `IServerMod` interface with lifecycle callbacks
- 🎨 **Client Mod API**: `IClientMod` interface for client-side extensions
- 🔌 **Custom Messaging**: Type-safe message passing between server/client mods
- 💾 **Save/Load Hooks**: Integrate custom data persistence
- 🎯 **Event System**: Subscribe to player connect/disconnect, server events

---

## 📋 Requirements

### Server Requirements
- **OS**: Windows Server 2019+, Windows 10/11, or Linux (Wine/Proton)
- **CPU**: 2+ cores recommended
- **.NET**: .NET Standard 2.1 (included with MelonLoader)
- **RAM**: 2GB minimum, 4GB recommended
- **Storage**: 500MB for game + saves

### Client Requirements
- **Schedule I**: Latest version (matches server version)
- **MelonLoader**: 0.6.x or 0.7.0/0.7.2+ (avoid 0.7.1)

---

## 🖥️ Running Entirely Headless

You can disable the MelonLoader console window to run the server completely headless:

1. Navigate to `Schedule I/UserData/`
2. Open `MelonLoader.cfg` in a text editor (may be named `Loader.cfg` in older MelonLoader versions)
3. Find the `[Console]` section
4. Set `HideConsole = true` (or `hide_console = true` in older versions)
5. Restart the server

The server will now run without any visible windows. Use the TCP console or log files for monitoring.

---

## 📚 Documentation

- **[Getting Started Guide](https://ifbars.github.io/S1DedicatedServersWiki/home/installation/)**: Detailed setup instructions
- **[Server Modding](https://ifbars.github.io/S1DedicatedServersWiki/modding/server/)**: Complete server mod API reference
- **[Client Modding](https://ifbars.github.io/S1DedicatedServersWiki/modding/client/)**: Complete client mod API reference
- **[Configuration Reference](https://ifbars.github.io/S1DedicatedServersWiki/configuration/)**: All config options explained
- **[Commands System](https://ifbars.github.io/S1DedicatedServersWiki/commands/)**: Understanding permissions and commands
- **[Custom Messaging](https://ifbars.github.io/S1DedicatedServersWiki/modding/messaging/)**: Building server-client communication

---

## 🔐 Authentication Quick Start

If you host a public server, enable ticket authentication so clients must prove Steam identity before they can run server commands.

```json
{
  "requireAuthentication": true,
  "authProvider": "SteamGameServer",
  "authTimeoutSeconds": 15,
  "authAllowLoopbackBypass": true,
  "steamGameServerLogOnAnonymous": true,
  "steamGameServerQueryPort": 27016,
  "steamGameServerMode": "Authentication"
}
```

- `authProvider` supports `None`, `SteamGameServer`, and `SteamWebApi`
- `SteamGameServer` is the recommended provider for dedicated hosting and Docker deployments
- Keep `authAllowLoopbackBypass` enabled so the dedicated server host loopback connection is not blocked
- Set `steamGameServerLogOnAnonymous` to `false` and provide `steamGameServerToken` when using a persistent GSLT
- `SteamWebApi` configuration fields exist, but Web API ticket validation is not yet fully implemented

---

## 🤝 Contributing

We welcome contributions! Please read our [Contributing Guide](CONTRIBUTING.md) and [Coding Standards](CODING_STANDARDS.md) before submitting PRs.

### Development Setup

1. Clone the repository
2. Copy `local.build.props.example` to `local.build.props`
3. Configure your game paths in `local.build.props`
4. Build with: `dotnet build -c Mono_Server`

See [BUILD_SETUP.md](BUILD_SETUP.md) for detailed build instructions.

---

## 🐛 Troubleshooting

### Server won't start
- Check `MelonLoader/Latest.log` for errors
- Verify `server_config.json` is valid JSON
- Ensure no port conflicts (default 38465)

### Client can't connect
- Check server firewall allows port 38465
- Verify server is running (`netstat -an | findstr 38465`)
- Ensure client and server versions match
- If auth is enabled, verify `authProvider` and Steam server login settings in `server_config.json`

### Permission denied errors
- Check your Steam ID is in operators/admins list
- Verify `server_config.json` saved correctly
- Reload config: `/reload` (if already operator)

### More help
- Check logs in `UserData/admin_actions.log`
- Enable debug mode: `"debugMode": true` in config
- Join our Discord: [Link Here]

---

## 📄 License

This project is licensed under the GPL-3.0 License - see the [LICENSE](LICENSE) file for details.

---

## ⚠️ Disclaimer

This is an alpha/beta release. Expect bugs and incomplete features. Use in production at your own risk. Always backup your save files before using with a dedicated server.

**This mod is not officially affiliated with or endorsed by the developers of Schedule I.**

---

**Made with ❤️ for the Schedule I community**
