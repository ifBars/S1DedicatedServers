# AI Agent Guidelines for DedicatedServerMod

This document provides guidance for AI coding assistants working on the DedicatedServerMod project.

---

## Project Overview

**DedicatedServerMod** is a professional dedicated server framework for Schedule I, built on Unity + FishNet networking with MelonLoader modding support. It enables authoritative headless servers with client synchronization, admin systems, and extensive modding APIs.

### Key Characteristics

- **Language**: C# (netstandard2.1)
- **Modding Framework**: MelonLoader 0.6.x / 0.7.x
- **Networking**: FishNet (observer-based prediction system)
- **Build Targets**: Mono and IL2CPP, Server and Client configurations
- **Architecture**: Side-aware (SERVER/CLIENT preprocessor directives)
- **Harmony Patching**: Extensive use of runtime method interception

---

## Architecture Overview

### Project Structure

```
DedicatedServerMod/
├── API/                    # Public-facing mod API
│   ├── S1DS.cs            # Main API entry point (partial class)
│   ├── S1DS.Server.cs     # Server-side API surface
│   ├── S1DS.Client.cs     # Client-side API surface
│   ├── IServerMod.cs      # Server mod interface
│   ├── IClientMod.cs      # Client mod interface
│   ├── ModManager.cs      # Mod registration and lifecycle
│   └── *ModBase.cs        # Base classes for mods
├── Server/                 # Server-only code (SERVER define)
│   ├── Core/              # Bootstrap and startup orchestration
│   ├── Network/           # Server networking and master server
│   ├── Player/            # Player tracking, auth, permissions
│   ├── Commands/          # Command system (TCP console, in-game)
│   ├── Persistence/       # Save/load management
│   ├── Game/              # Game system Harmony patches
│   └── TcpConsole/        # Remote console server
├── Client/                 # Client-only code (CLIENT define)
│   ├── Managers/          # Client-side subsystem managers
│   ├── Patches/           # Client-side Harmony patches
│   └── Data/              # Client state and server data storage
├── Shared/                 # Code used by both server and client
│   ├── Configuration/     # ServerConfig and related
│   ├── Networking/        # Custom messaging (server<->client RPC)
│   ├── Permissions/       # Permission manager, player resolver
│   └── Patches/           # Shared Harmony patches
├── Utils/                  # Cross-cutting utilities
│   ├── Constants.cs       # Centralized constants
│   ├── DebugLog.cs        # Logging abstraction
│   └── Version.cs         # Version tracking
├── Assets/                 # Embedded resources (asset bundles)
└── Examples/               # Example mods demonstrating API usage
```

### Key Subsystems

1. **Bootstrap & Initialization**
   - **Server**: `Server.Core.ServerBootstrap` (MelonMod entry point)
   - **Client**: `Client.ClientBootstrap` (MelonMod entry point)
   - Orchestrates startup sequence, patch application, manager initialization

2. **Configuration System**
   - `Shared.Configuration.ServerConfig`: JSON-based config with runtime reload
   - `Shared.Permissions.PermissionManager`: Admin/operator permission system
   - Command-line argument overrides

3. **Networking**
   - FishNet's `NetworkManager`, `NetworkObject`, `NetworkBehaviour`
   - Custom RPC via `Shared.Networking.CustomMessaging`
   - Registered on `DailySummary` (singleton NetworkBehaviour)

4. **Player Management**
   - `Server.Player.PlayerManager`: Track connected players
   - `Server.Player.PlayerAuthentication`: Steam ID verification
   - `Shared.Permissions.PlayerResolver`: Steam ID lookup utilities

5. **Command System**
   - `Server.Commands.CommandManager`: Command registration and dispatch
   - `Server.Commands.IServerCommand`: Command interface
   - `Server.TcpConsole.TcpConsoleServer`: Remote TCP console

6. **Harmony Patching**
   - Patches game methods for dedicated server compatibility
   - Examples: Sleep system, quest handling
   - Organized by concern (e.g., `Client.Patches.SleepPatches`)

7. **Mod API**
   - `API.IServerMod`, `API.IClientMod`: Modder interfaces
   - `API.ModManager`: Lifecycle notifications (init, player events, save/load)
   - Custom messaging for inter-mod communication

---

## Build System

### Configurations

Four build configurations (cross-product of runtime x side):

| Configuration | Runtime | Side   | Output |
|---------------|---------|--------|--------|
| Mono_Server   | Mono    | Server | `DedicatedServerMod_Mono_Server.dll` |
| Mono_Client   | Mono    | Client | `DedicatedServerMod_Mono_Client.dll` |
| Il2cpp_Server | IL2CPP  | Server | `DedicatedServerMod_Il2cpp_Server.dll` |
| Il2cpp_Client | IL2CPP  | Client | `DedicatedServerMod_Il2cpp_Client.dll` |

### Preprocessor Directives

- `#if SERVER`: Server-only code
- `#if CLIENT`: Client-only code
- `#if MONO`: Mono-specific (netstandard2.1)
- `#if IL2CPP`: IL2CPP-specific (net6.0, Il2Cpp interop)

### Build Properties

- `local.build.props`: User-specific game paths (git-ignored)
- `ci.build.props`: CI-specific assembly paths (GitHub Actions)
- `DedicatedServerMod.csproj`: Main project file with conditional references

### Assembly References

Uses **Krafs.Publicizer** to auto-publicize `Assembly-CSharp.dll` at build time.

---

## Coding Conventions

See [CODING_STANDARDS.md](CODING_STANDARDS.md) for complete details. Key points:

### Naming

- **PascalCase**: Classes, methods, properties, public fields
- **camelCase with `_` prefix**: Private/internal fields (`_logger`, `_config`)
- **SCREAMING_SNAKE_CASE**: Constants (prefer `Constants` class)
- **No `E` prefix** on enums

### Documentation

- **XML documentation required** for all public/protected APIs
- Include `<summary>`, `<param>`, `<returns>`, `<exception>`, `<remarks>`, `<example>`
- Document Harmony patches with "what" and "why"

### Access Modifiers

- **Explicit always**: Never rely on defaults
- **`public`**: API surface for modders
- **`internal`**: Cross-class coordination within assembly
- **`protected`**: Template methods in base classes
- **`private`**: Class-specific implementation
- **`sealed`**: Classes not intended for inheritance

### Error Handling

- Validate public method parameters (throw `ArgumentException`, `ArgumentNullException`)
- Use try-catch for expected exceptions, let unexpected ones bubble
- Log with `DebugLog` utility

### Code Organization

Member order: Constants → Static fields → Fields → Constructors → Properties → Public methods → Internal → Protected → Private → Event handlers → Nested types

---

## Common Tasks

### Adding a Public API Method

1. Add to appropriate partial class (`API/S1DS.Server.cs` or `API/S1DS.Client.cs`)
2. Write comprehensive XML documentation
3. Implement in relevant manager/subsystem
4. Update `docs/` if significant feature
5. Consider example mod usage

### Adding Configuration Option

1. Add property to `Shared/Configuration/ServerConfig.cs`
2. Add `[JsonProperty("jsonKey")]` attribute
3. Write XML documentation with default value and valid range
4. Handle in relevant system (e.g., `Server.Game.TimeSystemManager`)
5. Update `docs/configuration.md`

### Creating a Harmony Patch

1. Create patch class in appropriate folder:
   - Server patches: `Server/Game/`
   - Client patches: `Client/Patches/`
   - Shared patches: `Shared/Patches/`

2. Use attributes or manual patching:
   ```csharp
   [HarmonyPatch(typeof(TargetClass), nameof(TargetClass.Method))]
   public static class MyPatch
   {
       [HarmonyPrefix]
       public static bool Prefix(/* params */)
       {
           // Patch logic
           return true; // Continue to original
       }
   }
   ```

3. Document what you're patching and why:
   ```csharp
   /// <summary>
   /// Patches Player.AreAllPlayersReadyToSleep to ignore ghost host player.
   /// This enables sleep cycling to work on dedicated servers where the
   /// host player is a non-interactive loopback connection.
   /// </summary>
   ```

4. Apply patch in bootstrap:
   - Server: `ServerBootstrap.OnApplicationStart()`
   - Client: `ClientBootstrap.ApplyClientPatches()`

### Adding a Server Command

1. Create class implementing `IServerCommand` in `Server/Commands/Admin/` or `Server/Commands/Server/`
2. Implement interface methods:
   ```csharp
   public class MyCommand : IServerCommand
   {
       public string Name => "mycommand";
       public string Description => "Does something";
       public string Usage => "mycommand <arg>";
       
       public void Execute(string[] args, NetworkConnection sender = null)
       {
           // Implementation
       }
   }
   ```

3. Register in `CommandManager.RegisterCommands()`:
   ```csharp
   RegisterCommand(new MyCommand());
   ```

4. Add permission check if needed (CommandManager handles this based on config)

### Adding Custom Messaging

**Server → Client**:
```csharp
// In server code
CustomMessaging.SendToClient(connection, "my_command", JsonConvert.SerializeObject(data));
// Or broadcast
CustomMessaging.BroadcastToClients("my_command", JsonConvert.SerializeObject(data));
```

**Client → Server**:
```csharp
// In client code
CustomMessaging.SendToServer("my_request", JsonConvert.SerializeObject(request));
```

**Handling messages**:
- Server: Add case in `MessageRouter.RouteServerMessage()`
- Client: Add case in `MessageRouter.RouteClientMessage()`

---

## FishNet Integration

### Key Concepts

- **NetworkManager**: Singleton managing networking subsystem
- **NetworkObject**: Component for networked GameObject identity
- **NetworkBehaviour**: Base class for networked scripts (like MonoBehaviour)
- **Server RPC**: Client → Server method call
- **Target RPC**: Server → Specific Client method call
- **Observer RPC**: Server → All Observing Clients method call

### Dedicated Server Specifics

**Ghost Host Player**:
- Server runs a local "loopback" client for game state
- This player shouldn't interact with sleep, quests, etc.
- Identified by: `gameObject.name == "[DedicatedServerHostLoopback]"` or `Owner.ClientId == 0 && !IsOwner`

**Custom Messaging**:
- We don't use FishNet's code-generated RPCs (requires decorating game code)
- Instead: Manually register RPC handlers on `DailySummary` singleton
- Message ID: `105u` (arbitrary, avoid conflicts)

---

## Testing Strategy

### Build Testing

```bash
# Verify all configurations build
dotnet build -c Mono_Server
dotnet build -c Mono_Client
dotnet build -c Il2cpp_Server   # if IL2CPP assemblies available
dotnet build -c Il2cpp_Client   # if IL2CPP assemblies available

# Check for warnings
dotnet build -c Mono_Server -v normal | grep -i warning
```

### Runtime Testing

**Server**:
1. Start with config options being tested
2. Verify logs: `UserData/MelonLoader/Latest.log`
3. Check server state: TCP console commands
4. Monitor resource usage (CPU, RAM)

**Client**:
1. Connect to test server
2. Test modified functionality
3. Check client logs: `MelonLoader/Latest.log`
4. Verify UI behaves correctly

**Regression Testing**:
- Ensure existing features still work
- Test player connect/disconnect
- Test save/load cycle
- Test admin commands

### Manual Test Checklist

For significant changes:
- [ ] Server starts successfully
- [ ] Config loads correctly
- [ ] Client connects
- [ ] Player data syncs
- [ ] Commands work (if applicable)
- [ ] Save/load preserves state
- [ ] No exceptions in logs
- [ ] Performance acceptable

---

## Common Pitfalls

### 1. Side-Awareness Violations

**❌ Bad**:
```csharp
public class SomeClass
{
    public void DoSomething()
    {
        // Server-specific code without SERVER guard!
        ServerBootstrap.Instance.DoServerThing();
    }
}
```

**✅ Good**:
```csharp
public class SomeClass
{
    public void DoSomething()
    {
#if SERVER
        ServerBootstrap.Instance.DoServerThing();
#elif CLIENT
        ClientBootstrap.Instance.DoClientThing();
#endif
    }
}
```

### 2. Null Reference Exceptions with Unity Objects

Unity's `null` check is overloaded. Use pattern:
```csharp
// Unity object check
if (player != null && player.gameObject != null)
{
    // Safe to use
}

// Or for networked objects
if (player != null && player.Owner != null)
{
    // Safe to use
}
```

### 3. Harmony Patch Parameter Mismatches

Harmony injects parameters by type/name. Ensure patch signature matches:
```csharp
// Original method
public void DoSomething(int value, string name) { }

// Correct patch
[HarmonyPrefix]
public static bool Prefix(int value, string name) { }  // Match types/order

// Incorrect
public static bool Prefix(string name, int value) { }  // Wrong order
```

### 4. Config Changes Not Persisting

Always call `ServerConfig.SaveConfig()` after modifications:
```csharp
ServerConfig.Instance.SomeSetting = newValue;
ServerConfig.SaveConfig();  // Don't forget!
```

### 5. Custom Message Deserialization

Always validate deserialized data:
```csharp
// Bad - trusts data blindly
var data = JsonConvert.DeserializeObject<MyData>(message);
ProcessData(data.Value);  // NullReferenceException if deserialization failed

// Good - validates
try
{
    var data = JsonConvert.DeserializeObject<MyData>(message);
    if (data != null && !string.IsNullOrEmpty(data.Value))
    {
        ProcessData(data.Value);
    }
}
catch (JsonException ex)
{
    DebugLog.Warning($"Failed to deserialize message: {ex.Message}");
}
```

---

## Debugging Tips

### Enable Verbose Logging

```json
{
  "debugMode": true,
  "verboseLogging": true,
  "logPlayerActions": true,
  "logAdminCommands": true
}
```

### Use DebugLog Utility

```csharp
DebugLog.Info("Informational message");
DebugLog.Warning("Warning message");
DebugLog.Error("Error occurred", exception);
DebugLog.Debug("Debug message (only if debugMode true)");
DebugLog.Verbose("Verbose message (only if verboseLogging true)");
```

### Inspect Network State

```csharp
// Check if server/client
bool isServer = InstanceFinder.IsServer;
bool isClient = InstanceFinder.IsClient;
bool isHost = InstanceFinder.IsHost;

// Get server manager
var serverManager = InstanceFinder.ServerManager;
var connectedClients = serverManager?.Clients;

// Get client manager
var clientManager = InstanceFinder.ClientManager;
var connection = clientManager?.Connection;
```

### Remote Debugging via TCP Console

```bash
# Connect to server
telnet localhost 4050

# Check server state
> serverinfo
> list

# Test commands
> settime 12:00
> save
```

---

## Documentation Requirements

When adding/modifying public APIs:

1. **XML Documentation** (mandatory):
   ```csharp
   /// <summary>
   /// Brief description of what this does.
   /// </summary>
   /// <param name="paramName">What this parameter does</param>
   /// <returns>What this returns</returns>
   /// <remarks>
   /// Additional context, usage notes, warnings.
   /// </remarks>
   /// <example>
   /// <code>
   /// Example usage code here
   /// </code>
   /// </example>
   ```

2. **Markdown Documentation** (for significant features):
   - Update `docs/` with new feature documentation
   - Include examples, screenshots, troubleshooting

3. **Example Mods** (for new API surface):
   - Create example in `Examples/` demonstrating usage
   - Keep examples focused and well-commented

---

## Performance Considerations

### Hot Path Optimization

Avoid allocations in frequently-called methods:
```csharp
// Bad - allocates every call
public void Update()
{
    var list = new List<Player>();  // Allocation!
    foreach (var player in Player.PlayerList)
    {
        list.Add(player);
    }
}

// Good - cache or pool
private readonly List<Player> _cachedList = new();
public void Update()
{
    _cachedList.Clear();
    foreach (var player in Player.PlayerList)
    {
        _cachedList.Add(player);
    }
}
```

### Network Message Optimization

- Keep message payloads small (JSON compresses well)
- Batch updates when possible
- Use delta encoding for frequent updates

### Harmony Patch Performance

- Prefix patches execute before original method - minimize work
- Use `[HarmonyPrepare]` to conditionally apply patches
- Avoid patches in hot loops (Update, FixedUpdate)

---

## CI/CD Integration

### GitHub Actions

Workflows use private repository for game assemblies:
- `GAME_ASSEMBLIES_REPO` secret: Mono assemblies repo
- `IL2CPP_ASSEMBLIES_REPO` secret: IL2CPP assemblies repo
- `GAME_ASSEMBLIES_TOKEN` / `IL2CPP_ASSEMBLIES_TOKEN`: Access tokens

### Build Properties

CI uses `ci.build.props` generated dynamically:
```xml
<Project>
    <PropertyGroup>
        <AutomateLocalDeployment>false</AutomateLocalDeployment>
        <MelonLoaderAssembliesPath>$(MSBuildThisFileDirectory)ScheduleOneAssemblies/MelonLoader</MelonLoaderAssembliesPath>
        <MonoAssembliesPath>$(MSBuildThisFileDirectory)ScheduleOneAssemblies/Managed</MonoAssembliesPath>
        <Il2CppAssembliesPath>$(MSBuildThisFileDirectory)ScheduleOneAssemblies/Il2CppAssemblies</Il2CppAssembliesPath>
    </PropertyGroup>
</Project>
```

---

## Key Files Reference

### Entry Points

- **`Server/Core/ServerBootstrap.cs`**: Server MelonMod entry point
- **`Client/ClientBootstrap.cs`**: Client MelonMod entry point

### Core API

- **`API/S1DS.cs`**: Main API class (partial)
- **`API/IServerMod.cs`**: Server mod interface
- **`API/IClientMod.cs`**: Client mod interface
- **`API/ModManager.cs`**: Mod lifecycle management

### Configuration

- **`Shared/Configuration/ServerConfig.cs`**: Server configuration
- **`Shared/Permissions/PermissionManager.cs`**: Permission system
- **`Utils/Constants.cs`**: Centralized constants

### Networking

- **`Shared/Networking/CustomMessaging.cs`**: RPC system
- **`Server/Network/NetworkManager.cs`**: Server network orchestration

### Documentation

- **`README.md`**: User-facing documentation
- **`CODING_STANDARDS.md`**: Code style guide
- **`CONTRIBUTING.md`**: Contribution guide
- **`BUILD_SETUP.md`**: Build system documentation
- **`docs/`**: Detailed feature documentation

---

## Version & Release

Current version: **0.2.1-beta**

- **API Version**: 0.2.0
- **Status**: Alpha/Beta (unstable API, expect breaking changes)

Version bumping:
1. Update `Utils/Version.cs`
2. Update `README.md`
3. Create `CHANGELOG.md` entry
4. Tag release: `git tag v0.2.1-beta`

---

## Questions or Clarifications

If uncertain:
1. Check existing similar code for patterns
2. Reference S1API/MAPI/SteamNetworkLib for inspiration
3. Ask in PR comments
4. Prioritize readability and maintainability

---

**Remember**: AI assistants should follow these guidelines to maintain code quality and consistency. When in doubt, err on the side of more documentation, clearer naming, and safer error handling.
