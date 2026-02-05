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
- 🔒 **Password Protection**: Secure your server with password authentication
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

## 🚀 Quick Start

### Server Setup (5 Minutes)

1. **Install Schedule I Dedicated Server**:
   ```bash
   # Copy Schedule I game files to server machine
   # OR use SteamCMD (if available)
   ```

2. **Install MelonLoader**:
   ```bash
   # Download MelonLoader 0.6.5+ or 0.7.0+
   # Run MelonLoader.Installer.exe on Schedule I executable
   ```

3. **Install DedicatedServerMod**:
   ```bash
   # Download DedicatedServerMod_X_Server.dll from releases
   # Place in: Schedule I/Mods/
   ```

4. **Configure Server**:
   ```bash
   # Start server once to generate config
   # Edit: Schedule I/UserData/server_config.json
   ```

5. **Start Server**:
   ```bash
   # Run: "Schedule I.exe"
   # Server will start in headless mode
   ```

### Client Setup (2 Minutes)

1. **Install DedicatedServerMod Client**:
   ```bash
   # Download DedicatedServerMod_X_Client.dll
   # Place in: Schedule I/Mods/
   ```

2. **Connect to Server**:
   - Launch Schedule I
   - Use multiplayer "Join" menu
   - Enter server IP:PORT
   - Enjoy!

---

## ⚙️ Configuration

The server configuration file is located at `UserData/server_config.json`. Here's a sample configuration:

```json
{
  "serverName": "My Schedule I Server",
  "serverDescription": "A friendly server for new players",
  "maxPlayers": 8,
  "serverPort": 38465,
  "serverPassword": "",
  "publicServer": true,
  
  "operators": ["76561198XXXXXXX"],
  "admins": ["76561198YYYYYYY"],
  
  "timeNeverStops": true,
  "allowSleeping": true,
  "autoSaveEnabled": true,
  "autoSaveIntervalMinutes": 10.0,
  
  "tcpConsoleEnabled": false,
  "tcpConsolePort": 4050,
  "tcpConsoleBindAddress": "127.0.0.1"
}
```

### Command Line Arguments

Override configuration at startup:

```bash
"Schedule I.exe" --server-name "Production Server" --max-players 16 --add-operator 76561198XXXXXXX
```

Available arguments:
- `--server-name <name>`: Set server name
- `--max-players <num>`: Set max player count
- `--server-password <pass>`: Set server password
- `--add-operator <steamid>`: Add operator
- `--add-admin <steamid>`: Add admin
- `--debug`: Enable debug mode
- `--verbose`: Enable verbose logging
- `--tcp-console`: Enable TCP console
- `--tcp-console-port <port>`: Set TCP console port

---

## 🔐 Admin & Permissions

### Permission Levels

1. **Operators** (Full Access):
   - All commands (including dangerous ones)
   - Server management (shutdown, reload config)
   - Player management (kick, ban, permissions)
   - Game state manipulation

2. **Admins** (Standard Admin):
   - Player assistance commands (teleport, give items)
   - Time/weather control
   - Spawn vehicles/NPCs
   - Limited to non-destructive commands

3. **Players** (Minimal Access):
   - FPS display commands
   - Personal settings commands
   - (Configurable in `playerAllowedCommands`)

### Adding Admins

**Via Config File**:
```json
{
  "operators": ["76561198XXXXXXX"],
  "admins": ["76561198YYYYYYY", "76561198ZZZZZZZ"]
}
```

**Via Command Line**:
```bash
"Schedule I.exe" --add-operator 76561198XXXXXXX
```

**In-Game** (requires existing operator):
```
/op <player_name>
/admin <player_name>
```

**Via TCP Console**:
```bash
telnet localhost 4050
> op 76561198XXXXXXX
> admin 76561198YYYYYYY
```

---

## 📡 TCP Console

The TCP console allows remote server management via telnet/netcat/SSH.

### Enable TCP Console

**Config File**:
```json
{
  "tcpConsoleEnabled": true,
  "tcpConsolePort": 4050,
  "tcpConsoleBindAddress": "127.0.0.1",
  "tcpConsoleRequirePassword": true,
  "tcpConsolePassword": "your_secure_password"
}
```

### Connect to TCP Console

```bash
# Local connection
telnet localhost 4050

# Remote connection (if bindAddress is 0.0.0.0)
telnet your-server-ip 4050

# With netcat
nc your-server-ip 4050

# With socat (for persistent connection)
socat READLINE TCP:your-server-ip:4050
```

### Available Commands

```
help                  - List available commands
save                  - Save server state
shutdown              - Gracefully shutdown server
reload                - Reload configuration
list                  - List connected players
op <player>           - Grant operator privileges
deop <player>         - Remove operator privileges
admin <player>        - Grant admin privileges
deadmin <player>      - Remove admin privileges
kick <player>         - Kick player
ban <player>          - Ban player
unban <player>        - Unban player
serverinfo            - Show server information
```

### Security Considerations

- **Bind to 127.0.0.1** for local-only access (recommended)
- **Use strong passwords** if exposing to network
- **Use firewall rules** to restrict access
- **Consider SSH tunneling** for remote access:
  ```bash
  ssh -L 4050:localhost:4050 user@your-server
  telnet localhost 4050
  ```

---

## 🔧 Modding API

DedicatedServerMod provides powerful APIs for server and client mods.

### Server Mod Example

```csharp
using DedicatedServerMod.API;
using MelonLoader;

[assembly: MelonInfo(typeof(MyServerMod), "MyServerMod", "1.0.0", "YourName")]
[assembly: MelonGame("TVGS", "Schedule I")]

public class MyServerMod : ServerModBase
{
    protected override void OnServerInitialize()
    {
        LoggerInstance.Msg("My server mod initialized!");
    }
    
    protected override void OnPlayerConnected(string playerId)
    {
        LoggerInstance.Msg($"Player connected: {playerId}");
        
        // Send welcome message
        S1DS.Server.Messaging.BroadcastToClients(
            "welcome_message", 
            $"Welcome {playerId}!"
        );
    }
    
    protected override bool OnCustomMessage(string messageType, byte[] data, string senderId)
    {
        if (messageType == "my_custom_command")
        {
            string payload = System.Text.Encoding.UTF8.GetString(data);
            LoggerInstance.Msg($"Received custom command from {senderId}: {payload}");
            return true; // Message handled
        }
        return false; // Not handled, pass to other mods
    }
}
```

### Client Mod Example

```csharp
using DedicatedServerMod.API;
using MelonLoader;

[assembly: MelonInfo(typeof(MyClientMod), "MyClientMod", "1.0.0", "YourName")]
[assembly: MelonGame("TVGS", "Schedule I")]

public class MyClientMod : ClientModBase
{
    protected override void OnClientInitialize()
    {
        LoggerInstance.Msg("My client mod initialized!");
    }
    
    protected override void OnConnectedToServer()
    {
        LoggerInstance.Msg("Connected to dedicated server!");
        
        // Request server info
        S1DS.Client.Messaging.SendToServer("request_server_info");
    }
    
    protected override bool OnCustomMessage(string messageType, byte[] data)
    {
        if (messageType == "welcome_message")
        {
            string message = System.Text.Encoding.UTF8.GetString(data);
            LoggerInstance.Msg($"Server says: {message}");
            return true;
        }
        return false;
    }
}
```

### Custom Messaging

**Server → Client**:
```csharp
// Send to specific client
S1DS.Server.Messaging.SendToClient(connection, "command", "data");

// Broadcast to all clients
S1DS.Server.Messaging.BroadcastToClients("command", "data");
```

**Client → Server**:
```csharp
S1DS.Client.Messaging.SendToServer("command", "data");
```

---

## 📚 Documentation

- **[Getting Started Guide](docs/getting-started.md)**: Detailed setup instructions
- **[Server Setup](docs/server-setup.md)**: Advanced server configuration
- **[Server Modding](docs/server-modding.md)**: Complete server mod API reference
- **[Client Modding](docs/client-modding.md)**: Complete client mod API reference
- **[Configuration Reference](docs/configuration.md)**: All config options explained
- **[Permissions System](docs/permissions.md)**: Understanding permissions and security
- **[TCP Console](docs/tcp-console.md)**: Remote management guide
- **[Custom Messaging](docs/custom-messaging.md)**: Building cross-mod communication

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

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 🙏 Credits

- **Original Development**: [Your Name]
- **Contributors**: See [CONTRIBUTORS.md](CONTRIBUTORS.md)
- **Special Thanks**:
  - Schedule I development team
  - MelonLoader contributors
  - FishNet networking framework
  - S1 Modding Community

---

## 🔗 Links

- **GitHub**: [Repository URL]
- **Discord**: [Discord Server]
- **Documentation**: [Docs Site URL]
- **Issue Tracker**: [Issues URL]

---

## ⚠️ Disclaimer

This is an alpha/beta release. Expect bugs and incomplete features. Use in production at your own risk. Always backup your save files before using with a dedicated server.

**This mod is not officially affiliated with or endorsed by the developers of Schedule I.**

---

## 📊 Project Status

**Version**: 0.2.1-beta  
**Status**: Alpha/Beta Testing  
**API Stability**: Unstable (expect breaking changes)  
**Production Ready**: No  

### Roadmap

- [x] Core dedicated server functionality
- [x] Basic admin system
- [x] TCP console
- [x] Custom messaging API
- [ ] Web-based admin panel
- [ ] Plugin hot-reload
- [ ] Advanced metrics/monitoring
- [ ] Docker containerization
- [ ] Auto-updater

---

**Made with ❤️ for the Schedule I community**

---

## 🖥️ Running Entirely Headless

You can disable the MelonLoader console window to run the server completely headless:

1. Navigate to `Schedule I/UserData/`
2. Open `MelonLoader.cfg` in a text editor (may be named `Loader.cfg` in older MelonLoader versions)
3. Find the `[Console]` section
4. Set `HideConsole = true` (or `hide_console = true` in older versions)
5. Restart the server

The server will now run without any visible windows. Use the TCP console or log files for monitoring.
