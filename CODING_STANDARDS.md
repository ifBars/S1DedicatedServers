# Coding Standards

DedicatedServerMod follows strict coding standards to ensure consistency, maintainability, and professional quality. These standards are inspired by S1API, MAPI, and SteamNetworkLib, and align with SOLID principles.

---

## SOLID Principles (Design Foundation)

Apply these principles when designing APIs and modules:

| Principle | Definition | Applied In DedicatedServerMod |
|-----------|------------|-------------------------------|
| **S**ingle Responsibility | A class has one reason to change | `PlayerManager` (players only), `NetworkManager` (network only), `ServerConfig` (config only) |
| **O**pen/Closed | Open for extension, closed for modification | `IServerMod`/`IClientMod` interfaces and other extension seams let consumers extend behavior without modifying core |
| **L**iskov Substitution | Subtypes are substitutable for base types | `ServerModBase` / `ClientModBase`; any implementation works where base is expected |
| **I**nterface Segregation | Clients depend only on what they use | `IServerMod` vs `IClientMod`; small, focused interfaces |
| **D**ependency Inversion | Depend on abstractions, not concretions | External consumers depend on stable surfaces such as `S1DS.Server` and `S1DS.Client`; internals can change without breaking integrations |

Concrete guidelines:
- Prefer interfaces over concrete types for cross-module boundaries
- Keep classes focused; split when a class gains multiple unrelated responsibilities
- Use partial classes only for build-variant surfaces (e.g. `S1DS.Server` / `S1DS.Client`), not as a substitute for SRP
- Do not add pass-through properties or methods that merely mirror another subsystem's state (for example forwarding `ServerConfig.Instance` values through `ServerBootstrap`); depend on the owning type directly or extract a dedicated coordinator/service
- Bootstrap/orchestration classes may wire systems together, but config application, status projection, command execution, and persistence policy should live in dedicated collaborators rather than accumulate in the bootstrap itself

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

### Mono/IL2CPP Runtime Compatibility

This codebase supports both Mono and IL2CPP runtimes. Follow these patterns to maintain compatibility:

#### 1. Using Aliases for Game Types

When referencing Schedule One game types, use runtime-conditional using aliases:

```csharp
#if IL2CPP
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.UI;
#else
using ScheduleOne.PlayerScripts;
using ScheduleOne.DevUtilities;
using ScheduleOne.UI;
#endif
```

#### 2. Using Aliases for Steamworks Types

Steamworks types have different namespaces in Mono vs IL2CPP:

```csharp
#if IL2CPP
using Il2CppSteamworks;
#else
using Steamworks;
#endif
```

#### 3. Using Aliases for FishNet Types

FishNet types are prefixed with `Il2Cpp` in IL2CPP builds:

```csharp
#if IL2CPP
using Il2CppFishNet;
using Il2CppFishNet.Connection;
using Il2CppFishNet.Object;
using Il2CppFishNet.Serializing;
using Il2CppFishNet.Transporting;
#else
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
#endif
```

#### 4. Preferred Patterns

**Pattern A: File-level using aliases (Recommended for files with many game type references)**

Place at the top of the file, after system usings but before namespace:

```csharp
using System;
using MelonLoader;
#if IL2CPP
using Il2CppScheduleOne.PlayerScripts;
using Il2CppFishNet.Connection;
#else
using ScheduleOne.PlayerScripts;
using FishNet.Connection;
#endif

namespace DedicatedServerMod.Server.Player
{
    public class PlayerManager { }
}
```

**Pattern B: Conditional blocks within method bodies (for isolated runtime differences)**

```csharp
public void ProcessSteamId(ulong steamId)
{
#if IL2CPP
    var cSteamId = new Il2CppSteamworks.CSteamID(steamId);
#else
    var cSteamId = new Steamworks.CSteamID(steamId);
#endif
    // Common logic here
}
```

#### 5. Shared/Internal Compatibility Layer

For complex cross-cutting concerns, create internal compatibility helpers in `Utils/Compat/`:

```csharp
// Utils/Compat/SteamworksCompat.cs
internal static class SteamworksCompat
{
#if IL2CPP
    public static Il2CppSteamworks.CSteamID CreateCSteamID(ulong id) => new(id);
#else
    public static Steamworks.CSteamID CreateCSteamID(ulong id) => new(id);
#endif
}
```

Prefer existing shared aliases/helpers when they already exist (for example `Utils/GlobalTypeAliases.cs`) instead of inventing file-local naming schemes for the same runtime split.

#### 6. What NOT to Do

**❌ Don't scatter inline conditionals for type references:**

```csharp
// Bad - hard to read and maintain
public void DoSomething(Player player)  // Which Player? Mono or Il2Cpp?
{
    // Runtime error waiting to happen
}
```

**❌ Don't duplicate entire classes for each runtime:**

```csharp
// Bad - maintenance nightmare
#if MONO
public class PlayerManager { /* 200 lines */ }
#elif IL2CPP
public class PlayerManager { /* same 200 lines with different usings */ }
#endif
```

**✅ Do use consistent naming and aliases:**

```csharp
// Good - one implementation, runtime-agnostic
#if IL2CPP
using Il2CppScheduleOne.PlayerScripts;
#else
using ScheduleOne.PlayerScripts;
#endif

public class PlayerManager
{
    public void ProcessPlayer(Player player) { }
}
```

#### 7. Build Configuration Reference

| Configuration | Runtime | Side | Define Constants |
|---------------|---------|------|------------------|
| Mono_Server | Mono | Server | `MONO;SERVER` |
| Mono_Client | Mono | Client | `MONO;CLIENT` |
| Il2cpp_Server | IL2CPP | Server | `IL2CPP;SERVER` |
| Il2cpp_Client | IL2CPP | Client | `IL2CPP;CLIENT` |

---

## Naming Conventions

### General Rules

* **PascalCase**: Classes, methods, properties, public/internal fields, enums
* **camelCase**: Local variables, parameters, private fields (prefixed with `_`)
* **SCREAMING_SNAKE_CASE**: Constants (use sparingly, prefer `const` in static class)
* Prefix private and internal instance fields with `_`
* Static readonly fields use PascalCase
* Prefer existing naming patterns in the codebase over introducing new generic suffixes

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
private static readonly MelonLogger.Instance Logger;

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

### Acronym and Initialism Casing

**Multi-letter acronyms** in type names use PascalCase with the acronym fully uppercased when it stands alone or ends an identifier:

```csharp
// Correct - DS = Dedicated Server, full acronym uppercased
public static class S1DS { }

// Incorrect - inconsistent casing causes CS0117 (type not found)
public static class S1Ds { }  // Don't mix; use S1DS
```

**Critical for partial classes**: All partial class declarations must use the **exact same identifier** (C# is case-sensitive). A mismatch (e.g. `S1Ds` in one file, `S1DS` in another) causes `CS0117: 'X' does not contain a definition for 'Y'` because the compiler treats them as different types.

```csharp
// Correct - identical across all partial files
// S1DS.cs:
public static partial class S1DS { }

// S1DS.Server.cs:
public static partial class S1DS { }

// S1DS.Client.cs:
public static partial class S1DS { }
```

### Naming Patterns

* **Managers**: Use `-Manager` suffix for coordination classes
  ```csharp
  public class PlayerManager { }
  public class CommandManager { }
  ```

* **Avoid suffix churn**: Do not introduce interchangeable names like `FooManager`, `FooHandler`, `FooService`, and `FooSystem` for the same kind of responsibility. Follow the terminology already established in the surrounding subsystem.

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

* **`public`**: Reserved for intentional supported surface for external consumers, integrations, and extensibility points. Every `public` member expands the compatibility contract and documentation surface, so do not use it for convenience.
* **`internal`**: Default for non-API types and members that must be shared within DedicatedServerMod but are not part of the external contract.
* **`protected`**: Only for subclass extension points in deliberate inheritance scenarios (for example `ServerModBase`).
* **`private`**: Default for implementation details used only inside the declaring type.
* **`private protected` / `protected internal`**: Avoid unless the exact accessibility semantics are required and justified in review. These are almost never appropriate in this codebase.

### API Boundary Rules

Treat accessibility as contract design, not just visibility:

* Do not mark a type or member `public` unless external assemblies are expected to call it directly as part of the supported surface.
* Internal systems may expose helper methods needed by other code in this assembly, but those helpers should remain `internal`, not `public`.
* Types that are not part of the supported extension surface should not have `public` constructors. If a service is only created by the assembly, use `internal` or `private` constructors as appropriate.
* Do not expose lifecycle, coordination, cache, patch, bootstrap, or manager internals publicly just to make call sites easier. Add a deliberate API facade instead if external access is truly needed.
* If a member is public only for tests, refactor the design instead of widening production visibility.
* Before adding a `public` symbol, ask: "Do we want to document, support, and preserve this member across versions?" If the answer is no, it should not be public.
* Do not leak raw `Il2Cpp*`, Unity game, or FishNet implementation types through the supported external surface unless that is an explicit and documented design decision. Prefer DedicatedServerMod-owned abstractions or DTOs at public boundaries.

This matters for both safety and maintainability:

* Public internals let outside assemblies call into states and sequences we do not control, increasing the risk of breakage.
* Accidentally public members bloat generated documentation and make the supported modding surface harder to understand.

```csharp
// Public API
public abstract class ServerModBase
{
    // Supported external API
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

```csharp
// Internal system with assembly-only helpers
internal sealed class AdminStatusManager
{
    private readonly ClientContext _context;

    internal AdminStatusManager(ClientContext context)
    {
        _context = context;
    }

    internal bool IsLocalPlayerAdmin()
    {
        return _context.LocalPlayer != null && _context.LocalPlayer.IsAdmin;
    }

}
```

```csharp
// Bad - accidental external contract on an internal service
internal sealed class AdminStatusManager
{
    public AdminStatusManager(ClientContext context) { }
    public bool IsLocalPlayerAdmin() => true;
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
* Internal implementation details, unless additional comments are needed for maintainers
* Self-explanatory property getters/setters
* Override methods (inherit documentation from base)
* Never widen a member to `public` just to make it appear in generated documentation
* Raw runtime-specific details that are intentionally hidden behind the public API

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

## Extension Surface Design

DedicatedServerMod is an open-source dedicated server framework, not just a modding API. Design public contracts accordingly:

* Prefer small, stable extension seams over exposing entire internal subsystems.
* Public APIs should represent deliberate framework capabilities, not incidental access to current implementation details.
* When an external scenario only needs read-only state or a narrow capability, expose that narrow abstraction instead of the owning manager/service.
* Favor project-owned models, interfaces, and events at boundaries so internal networking, patching, and game interop can evolve without unnecessary breakage.
* If a new public member is primarily useful to code inside this repository, it probably should be `internal`.

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

### ❌ Don't Add Redundant Forwarders

```csharp
// Bad - bootstrap becomes a second config surface
public static bool AutoSaveEnabled
{
    get => ServerConfig.Instance.AutoSaveEnabled;
    set => ServerConfig.Instance.AutoSaveEnabled = value;
}

// Good - use the owning type directly
bool autoSaveEnabled = ServerConfig.Instance.AutoSaveEnabled;

// Good - or move behavior into a dedicated service
ServerRuntimeConfigurationApplier applier = new ServerRuntimeConfigurationApplier(ServerConfig.Instance, logger);
applier.Apply();
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

### Documentation Validation

For changes that affect the supported external surface or published guides:

- Build the relevant configuration(s) and keep XML documentation warnings clean
- Regenerate DocFX output when API docs or documentation structure changes
- Update `local.build.props.example` if new local path properties are introduced

---

## Questions or Concerns?

If you're unsure about any standard:

1. Check existing codebase for patterns
2. Ask in PR comments
3. Reference S1API/MAPI/SteamNetworkLib for inspiration
4. When in doubt, prioritize **readability** over cleverness

---

**Remember**: These standards exist to make the codebase maintainable and professional. They're not about personal preference—they're about consistency and quality for everyone who contributes.
