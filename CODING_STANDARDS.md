# Coding Standards

DedicatedServerMod follows strict coding standards to ensure consistency, maintainability, and professional quality. These standards are inspired by S1API, MAPI, and SteamNetworkLib.

---

## General Best Practices

* **Review the codebase thoroughly** before submitting a PR
* **Follow existing patterns** - maintain consistency with established code
* **Write self-documenting code** - clear naming reduces need for comments
* **Test both configurations** - verify Mono and IL2CPP builds when applicable
* **Keep scope minimal** - one feature/fix per PR
* **Document breaking changes** - clearly mark API changes in commit messages

---

## File and Namespace Structure

### Namespace Organization

All classes must exist in a logical namespace matching the folder structure:

```csharp
// Correct
namespace DedicatedServerMod.Server.Commands
{
    public class CommandManager { }
}

// Incorrect - namespace doesn't match folder
namespace DedicatedServerMod
{
    public class CommandManager { } // in Server/Commands/ folder
}
```

### Folder Structure Conventions

```
DedicatedServerMod/
├── API/                    # Public API surface (IServerMod, IClientMod, etc.)
├── Server/                 # Server-side implementation
│   ├── Core/              # Bootstrap and orchestration
│   ├── Network/           # Network management
│   ├── Player/            # Player tracking and auth
│   ├── Commands/          # Command system
│   ├── Persistence/       # Save/load management
│   ├── Game/              # Game system patches
│   └── TcpConsole/        # Remote console
├── Client/                 # Client-side implementation
│   ├── Managers/          # Client-side managers
│   ├── Patches/           # Harmony patches
│   └── Data/              # Client data storage
├── Shared/                 # Code used by both server and client
│   ├── Configuration/     # Config management
│   ├── Networking/        # Custom messaging
│   ├── Permissions/       # Permission system
│   └── Patches/           # Shared patches
└── Utils/                  # Utility classes
```

### Conditional Compilation

Use `#if SERVER` and `#if CLIENT` for side-specific code:

```csharp
#if SERVER
using DedicatedServerMod.Server.Core;
#elif CLIENT
using DedicatedServerMod.Client;
#endif

namespace DedicatedServerMod.API
{
    public static partial class S1DS
    {
#if SERVER
        public static ServerBootstrap Server { get; }
#endif

#if CLIENT
        public static ClientBootstrap Client { get; }
#endif
    }
}
```

---

## Naming Conventions

### General Rules

* **PascalCase**: Classes, methods, properties, public/internal fields, enums
* **camelCase**: Local variables, parameters, private fields (prefixed with `_`)
* **SCREAMING_SNAKE_CASE**: Constants (use sparingly, prefer `const` in static class)

### Examples

```csharp
// Classes and interfaces
public class ServerBootstrap { }
public interface IServerMod { }

// Methods and properties
public void StartServer() { }
public string ServerName { get; }

// Private fields (underscore prefix)
private MelonLogger.Instance _logger;
private readonly ServerConfig _config;
internal NetworkConnection _activeConnection;

// Local variables and parameters
public void ProcessPlayer(Player player)
{
    string steamId = GetSteamId(player);
    bool isAdmin = CheckPermissions(steamId);
}

// Constants (prefer Constants class over scattered const)
public static class Constants
{
    public const string MOD_VERSION = "0.2.1-beta";
    public const int DEFAULT_PORT = 38465;
}

// Enums (no 'E' prefix)
public enum PermissionLevel
{
    Player,
    Admin,
    Operator
}
```

### Naming Patterns

* **Managers**: Use `-Manager` suffix for coordination classes
  ```csharp
  public class PlayerManager { }
  public class CommandManager { }
  ```

* **Patches**: Use `-Patches` or `-Patcher` suffix for Harmony patch classes
  ```csharp
  public static class SleepPatches { }
  public class ClientTransportPatcher { }
  ```

* **Data/Info Classes**: Use descriptive names with context
  ```csharp
  public sealed class ConnectedPlayerInfo { }
  public sealed class ServerHeartbeatData { }
  ```

* **Boolean Properties**: Use `Is`, `Has`, `Can`, `Should` prefixes
  ```csharp
  public bool IsOperator { get; }
  public bool HasPermission { get; }
  public bool CanUseCommand { get; }
  public bool ShouldAutoSave { get; }
  ```

---

## Access Modifiers

### Explicit Modifiers Required

Always use explicit access modifiers (never rely on defaults):

```csharp
// Correct
public class ServerBootstrap { }
internal sealed class InternalHelper { }
private void ProcessInternal() { }

// Incorrect
class ServerBootstrap { }  // implicit internal - be explicit!
void ProcessInternal() { }  // implicit private - be explicit!
```

### Access Level Guidelines

* **`public`**: API surface for modders, documented public functionality
* **`internal`**: Implementation details, cross-class coordination within assembly
* **`protected`**: Template pattern methods in base classes (e.g., `ServerModBase`)
* **`private`**: Class-specific implementation details

```csharp
// Public API
public abstract class ServerModBase
{
    // Public API for modders
    public MelonLogger.Instance LoggerInstance { get; }
    
    // Template methods for subclasses
    protected abstract void OnServerInitialize();
    protected abstract void OnPlayerConnected(string playerId);
    
    // Internal coordination
    internal void NotifyServerInitialize() { }
    
    // Private implementation
    private void ValidateState() { }
}
```

### Sealed Classes

Use `sealed` on classes not intended for inheritance:

```csharp
// Sealed (prevents inheritance)
public sealed class ServerConfig { }
public sealed class ConnectedPlayerInfo { }

// Not sealed (template/extensibility pattern)
public abstract class ServerModBase { }
public class NetworkBehaviourBase { }
```

---

## Documentation

### XML Documentation Requirements

**All public and protected API declarations must have XML documentation.**

```csharp
/// <summary>
/// Manages connected players and their authentication state.
/// </summary>
public class PlayerManager
{
    /// <summary>
    /// Gets a player by their Steam ID.
    /// </summary>
    /// <param name="steamId">The Steam ID to search for</param>
    /// <returns>The connected player info, or null if not found</returns>
    public ConnectedPlayerInfo GetPlayer(string steamId)
    {
        // Implementation
    }
    
    /// <summary>
    /// Kicks a player from the server.
    /// </summary>
    /// <param name="player">The player to kick</param>
    /// <param name="reason">The reason for kicking (shown to player)</param>
    /// <exception cref="ArgumentNullException">Thrown when player is null</exception>
    public void KickPlayer(Player player, string reason)
    {
        // Implementation
    }
}
```

### Documentation Tags

Use these XML tags appropriately:

* `<summary>`: Brief description (required)
* `<param>`: Parameter description (required for all parameters)
* `<returns>`: Return value description (required if non-void)
* `<exception>`: Document thrown exceptions
* `<remarks>`: Additional details, usage notes, warnings
* `<example>`: Code examples for complex APIs
* `<seealso>`: Related classes/methods

```csharp
/// <summary>
/// Sends a custom message from client to server.
/// </summary>
/// <param name="command">The message command identifier</param>
/// <param name="data">The message payload (JSON serialized)</param>
/// <remarks>
/// This method queues the message for network transmission. There is no guarantee
/// of immediate delivery. Use response handlers for acknowledgment.
/// </remarks>
/// <example>
/// <code>
/// S1DS.Client.Messaging.SendToServer("request_data", JsonConvert.SerializeObject(request));
/// </code>
/// </example>
/// <seealso cref="OnCustomMessage"/>
public static void SendToServer(string command, string data = "")
```

### When NOT to Document

* Private implementation methods (unless complex)
* Self-explanatory property getters/setters
* Override methods (inherit documentation from base)

```csharp
// No documentation needed - obvious
private string _cachedValue;

// Documentation inherited from interface/base
public override void OnServerInitialize()
{
    base.OnServerInitialize();
}
```

---

## Code Organization

### Member Ordering

Organize class members in this order:

1. Constants
2. Static fields
3. Fields (private, internal, protected, public)
4. Constructors
5. Properties
6. Public methods
7. Internal methods
8. Protected methods
9. Private methods
10. Event handlers
11. Nested types

```csharp
public class ExampleClass
{
    // 1. Constants
    private const int MAX_RETRIES = 3;
    
    // 2. Static fields
    private static readonly MelonLogger.Instance Logger;
    
    // 3. Fields
    private readonly ServerConfig _config;
    internal NetworkConnection _connection;
    
    // 4. Constructor
    public ExampleClass(ServerConfig config)
    {
        _config = config;
    }
    
    // 5. Properties
    public string ServerName => _config.ServerName;
    
    // 6. Public methods
    public void StartServer() { }
    
    // 7. Internal methods
    internal void NotifyStateChange() { }
    
    // 8. Protected methods
    protected virtual void OnStateChanged() { }
    
    // 9. Private methods
    private void ValidateConfig() { }
    
    // 10. Event handlers
    private void OnPlayerConnected(object sender, EventArgs e) { }
    
    // 11. Nested types
    private class StateManager { }
}
```

### Use Regions Sparingly

Only use regions for large classes with clear logical sections:

```csharp
public class ServerConfig
{
    #region Server Settings
    public string ServerName { get; set; }
    public int MaxPlayers { get; set; }
    #endregion
    
    #region Permission Management
    public bool IsAdmin(string steamId) { }
    public bool IsOperator(string steamId) { }
    #endregion
}
```

**Don't overuse regions** - prefer splitting large classes instead.

---

## Arrow Functions and Expression Bodies

Use arrow functions (`=>`) for simple one-line implementations:

```csharp
// Properties (arrow function on new line, indented)
public string ServerName =>
    _config.ServerName;

// Methods (arrow function on new line, indented)
public bool IsEmpty() =>
    PlayerCount == 0;

// Multi-line expression bodies (format carefully)
public Player GetPlayer(string steamId) =>
    _players.FirstOrDefault(p => p.SteamId == steamId);
```

Use traditional bodies for complex logic:

```csharp
// Complex logic - use traditional body
public void ProcessPlayer(Player player)
{
    if (player == null)
        throw new ArgumentNullException(nameof(player));
        
    string steamId = GetSteamId(player);
    ValidatePermissions(steamId);
    UpdatePlayerState(player);
}
```

---

## Error Handling

### Validation

Validate public method parameters:

```csharp
public void AddPlayer(Player player, string steamId)
{
    if (player == null)
        throw new ArgumentNullException(nameof(player));
    
    if (string.IsNullOrEmpty(steamId))
        throw new ArgumentException("Steam ID cannot be null or empty", nameof(steamId));
    
    // Implementation
}
```

### Try-Catch Patterns

Use try-catch for expected exceptions, let unexpected ones bubble:

```csharp
// Good - handle expected network errors
public void SendMessage(NetworkConnection connection, string message)
{
    try
    {
        connection.Send(message);
    }
    catch (NetworkException ex)
    {
        DebugLog.Warning($"Failed to send message: {ex.Message}");
        // Handle gracefully
    }
}

// Bad - catching everything hides bugs
try
{
    DoSomething();
}
catch (Exception ex)
{
    DebugLog.Error($"Error: {ex}");
    // Swallows all exceptions including bugs!
}
```

### Logging Exceptions

Use `DebugLog` utility for consistent logging:

```csharp
try
{
    RiskyOperation();
}
catch (Exception ex)
{
    DebugLog.Error("Failed to perform risky operation", ex);
    throw; // Re-throw if can't handle
}
```

---

## Immutability and Constants

### Use Readonly

Mark fields as `readonly` when they won't change after construction:

```csharp
private readonly ServerConfig _config;
private readonly MelonLogger.Instance _logger;
internal readonly Player S1Player; // Wrapper reference
```

### Use Const Appropriately

Only use `const` for true compile-time constants:

```csharp
// Good - compile-time constant
private const string DEFAULT_SERVER_NAME = "Schedule I Server";
private const int MAX_PLAYERS = 32;

// Bad - should be readonly (requires runtime evaluation)
private const string LogPath = Path.Combine("logs", "server.log");
```

---

## Nullable Reference Types

### Nullable Annotations

Use `?` to indicate nullable types:

```csharp
public Player? GetPlayer(string steamId) // May return null
{
    return _players.FirstOrDefault(p => p.SteamId == steamId);
}

public void ProcessPlayer(Player? player) // Accepts null
{
    if (player == null)
        return;
    
    // player is non-null here
}
```

### Non-Null Assertions

Use `!` only when you're certain a value is not null:

```csharp
// Safe - we checked above
if (player != null)
{
    string name = player.PlayerName!;
}

// Dangerous - could throw NullReferenceException
string name = GetPlayer(steamId)!.PlayerName;
```

---

## What **NOT** to Do

### ❌ Don't Mix Concerns

```csharp
// Bad - ServerConfig doing too much
public class ServerConfig
{
    public bool IsAdmin(string steamId) { } // Permission logic
    public void SendMessage(NetworkConnection conn) { } // Networking
    public void SaveToDatabase() { } // Persistence
}

// Good - separate concerns
public class ServerConfig { /* config only */ }
public class PermissionManager { /* permissions only */ }
public class NetworkManager { /* networking only */ }
```

### ❌ Don't Use Magic Strings/Numbers

```csharp
// Bad
if (messageType == "exec_console") { }
connection.Send(105, data);

// Good
if (messageType == Constants.Messages.EXEC_CONSOLE) { }
connection.Send(Constants.Network.CUSTOM_MESSAGE_ID, data);
```

### ❌ Don't Ignore Compiler Warnings

Treat warnings as errors. Fix them, don't suppress them.

```csharp
// Bad
#pragma warning disable CS1591 // Missing XML documentation
public void PublicMethod() { }

// Good - add documentation
/// <summary>
/// Public method that does something important.
/// </summary>
public void PublicMethod() { }
```

### ❌ Don't Leave Commented Code

```csharp
// Bad
public void DoSomething()
{
    // Old implementation
    // for (int i = 0; i < 10; i++)
    // {
    //     ProcessItem(i);
    // }
    
    // New implementation
    ProcessAllItems();
}

// Good
public void DoSomething()
{
    ProcessAllItems();
}
```

### ❌ Don't Use Var When Type Isn't Obvious

```csharp
// Bad - type not obvious
var result = GetSomething();
var data = ProcessData();

// Good - type obvious
var player = new Player();
var players = new List<Player>();

// Good - explicit type when unclear
PlayerData result = GetSomething();
NetworkMessage data = ProcessData();
```

---

## Git Commit Standards

Follow Conventional Commits format:

```
<type>(<scope>): <description>

[optional body]

[optional footer]
```

### Types

* `feat`: New feature
* `fix`: Bug fix
* `refactor`: Code restructuring without behavior change
* `docs`: Documentation changes
* `style`: Code style changes (formatting, etc.)
* `test`: Adding or updating tests
* `chore`: Build process, tooling changes
* `perf`: Performance improvements

### Examples

```
feat(Server): add TCP console remote management

Implements a TCP-based console server for remote server management.
Supports authentication and command execution.

Closes #42
```

```
fix(Client): resolve sleep cycle desync with ghost host

The client was not properly ignoring the ghost loopback player
during sleep readiness checks, causing sleep cycles to hang.
```

```
refactor(Shared): split ServerConfig into separate concerns

BREAKING CHANGE: ServerConfig permission methods moved to PermissionManager

- Created PermissionManager for admin/op management
- Created PlayerResolver for Steam ID utilities
- ServerConfig now only contains configuration
```

---

## Testing Guidelines

### Manual Testing Checklist

Before submitting a PR, verify:

- [ ] Code compiles for all configurations (Mono_Server, Mono_Client, Il2cpp_Server, Il2cpp_Client)
- [ ] No compiler warnings
- [ ] Server starts successfully
- [ ] Client connects successfully
- [ ] Modified features work as expected
- [ ] Existing features still work (regression testing)
- [ ] Config changes are documented

### Test Scenarios

Document test scenarios for new features:

```csharp
/// <summary>
/// Adds an operator to the permission system.
/// </summary>
/// <remarks>
/// Test scenarios:
/// 1. Add valid Steam ID - should succeed
/// 2. Add duplicate Steam ID - should return false
/// 3. Add null/empty Steam ID - should return false
/// 4. Config should be saved after successful add
/// </remarks>
public static bool AddOperator(string steamId)
```

---

## Questions or Concerns?

If you're unsure about any standard:

1. Check existing codebase for patterns
2. Ask in PR comments
3. Reference S1API/MAPI/SteamNetworkLib for inspiration
4. When in doubt, prioritize **readability** over cleverness

---

**Remember**: These standards exist to make the codebase maintainable and professional. They're not about personal preference—they're about consistency and quality for everyone who contributes.
