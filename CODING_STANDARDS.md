# Coding Standards

While we do not need to be overly strict about personal style, there are specific standards to apply.
These standards are meant to keep DedicatedServerMod consistent, predictable, and maintainable.

DedicatedServerMod follows the same general spirit as S1API: keep code readable, keep naming predictable, and keep the supported API surface intentional.

## General Best Practice

* Review the surrounding code before making changes.
* Follow existing subsystem patterns instead of introducing a new style for one file.
* Keep classes focused on a single responsibility.
* Keep scope minimal. One feature or fix should not quietly include unrelated cleanup.
* Validate the relevant build configurations before finishing.

See [BUILD_SETUP.md](BUILD_SETUP.md) for local build requirements and [AGENTS.md](AGENTS.md) for repo-specific workflow guidance.

## File and Namespace Structure

* All classes must exist in a logical namespace matching the folder structure.
* Public API code belongs under `DedicatedServerMod.API`.
* Keep the root API namespace intentionally small around entry points and broad lifecycle contracts.
* Place specialized public API helpers in focused sub-namespaces such as `DedicatedServerMod.API.Client`, `DedicatedServerMod.API.Server`, and `DedicatedServerMod.API.Metadata`.
* Side-specific implementation belongs under `DedicatedServerMod.Server` or `DedicatedServerMod.Client`.
* Shared code belongs under `DedicatedServerMod.Shared`.

```csharp
namespace DedicatedServerMod.Server.Commands
{
    internal sealed class CommandManager
    {
    }
}
```

* Prefer one top-level type per file.
* The filename should match the primary top-level type in the file.
* Nested helper types are acceptable when they are true implementation details of the owning type.

### Side-Aware Compilation

* Use `#if SERVER` and `#if CLIENT` for side-specific logic.
* Keep side-specific `using` directives wrapped in the same way.
* Do not let server-only code leak into client builds, or client-only code leak into server builds.

```csharp
#if SERVER
using DedicatedServerMod.Server.Core;
#elif CLIENT
using DedicatedServerMod.Client;
#endif
```

### Runtime Compatibility

DedicatedServerMod supports both Mono and IL2CPP builds.

* Prefer runtime-conditional `using` aliases over duplicating entire classes.
* Reuse existing shared aliases and compatibility helpers before inventing new file-local patterns.
* Keep one implementation when the difference is only the underlying imported type.

```csharp
#if IL2CPP
using Il2CppFishNet.Connection;
#else
using FishNet.Connection;
#endif
```

## Naming Conventions

These rules are intentionally close to S1API's style.

* In general, follow the project's existing naming patterns.
* `PascalCase` is used for class names, methods, properties, enums, and non-private fields.
* `camelCase` is used for local variables and parameters.
* Prefix private and internal instance fields with `_`.
* Static readonly fields use PascalCase.
* Enums should not use an `E` prefix.
* Use enums over strings where a closed set of values exists.
* Do not introduce interchangeable suffixes such as `Manager`, `Handler`, `Service`, and `System` for the same kind of responsibility inside one subsystem. Match the terminology already in use.

```csharp
private readonly ServerConfig _config;
internal NetworkConnection _connection;
private static readonly MelonLogger.Instance Logger;
```

### Naming Patterns

* Use `Manager` for coordination types where that pattern already exists, such as `PlayerManager` and `CommandManager`.
* Use descriptive names for data and transfer models, such as `ConnectedPlayerInfo`.
* Boolean members should read clearly and usually start with `Is`, `Has`, `Can`, or `Should`.
* Partial classes must use the exact same casing in every file.

```csharp
public static partial class S1DS
{
}
```

## Access Modifiers

* Use explicit access modifiers at all times.
* `public` is for intentional, supported external surface only.
* `internal` is the default for assembly-only coordination.
* `protected` is for deliberate subclass extension points.
* `private` is for implementation details.
* Use `sealed` on classes when inheritance is not intended.

```csharp
public sealed class ServerConfig
{
}

internal sealed class AdminStatusManager
{
}
```

### API Boundary Rules

DedicatedServerMod is a framework with a supported extension surface, not just an internal mod.

* Do not mark a type or member `public` unless external assemblies are expected to call it directly.
* Do not widen visibility for convenience.
* Do not expose bootstrap, patch, cache, or coordination internals as part of the supported API.
* Do not leak raw Unity, FishNet, or `Il2Cpp*` implementation types through the supported external surface unless that is an explicit and documented design decision.
* If external consumers need narrow access to behavior or state, prefer a deliberate facade or project-owned abstraction.

### Event-First Extension Points

DedicatedServerMod is moving optional extension hooks toward events and disposable registrations rather than more mod-facing interfaces.

* Keep `IServerMod` and `IClientMod` as the coarse lifecycle contracts for discovery and broad participation.
* Prefer `ModManager` events or subsystem-owned events for optional hooks such as player lifecycle and custom messaging.
* Prefer typed payloads such as `ConnectedPlayerInfo` or dedicated context objects over ambiguous identifiers like `playerId` when exposing new extension points.
* If a hook needs ordering, teardown, or scoped ownership, prefer an explicit registration API that returns a project-owned registration/disposable type.
* Do not add a new public interface only to expose a small optional callback surface when an event or registration API would be clearer.

## Documentation

* All public and protected API declarations must have XML documentation.
* Document parameters and return values when applicable.
* Use `<remarks>` and `<example>` for behavior that is not obvious from the signature.
* Never widen a member to `public` just to make it appear in generated docs.

```csharp
/// <summary>
/// Gets a player by Steam ID.
/// </summary>
/// <param name="steamId">The Steam ID to resolve.</param>
/// <returns>The connected player info, or <see langword="null"/> when not found.</returns>
public ConnectedPlayerInfo GetPlayer(string steamId)
{
    // Implementation
}
```

Public API documentation is enforced by build warnings. Keep those warnings clean.

## Code Organization

* Group related members together.
* Organize members in this order:
  * Constants
  * Static fields
  * Fields
  * Constructors
  * Properties
  * Public methods
  * Internal methods
  * Protected methods
  * Private methods
  * Event handlers
  * Nested types
* Use regions sparingly. If a class needs many regions just to stay readable, it should probably be split.
* Static utility types should be marked `static`.
* Keep bootstrap classes focused on orchestration. Do not let them accumulate config interpretation, persistence policy, command behavior, or feature-specific logic that belongs in dedicated collaborators.

### Expression Bodies

* Use arrow members for simple properties and simple one-line methods.
* Place the expression body on the next line and indent once.
* Use a normal block body for anything non-trivial.

```csharp
public string ServerName =>
    _config.ServerName;

public bool IsReady() =>
    _playerCount > 0;
```

### Immutability and Nullability

* Use `readonly` or `const` for immutable values.
* Declare nullable references with `?` when null is an expected state.
* Use null-forgiving operators only when the safety argument is obvious and local.

```csharp
private readonly ServerConfig _config;
public ConnectedPlayerInfo? TryGetPlayer(string steamId)
{
    return null;
}
```

## What Not To Do

* Do not mix unrelated concerns in one type.
* Do not add redundant forwarding properties or methods just to mirror another subsystem.
* Do not use magic strings or numbers when a named constant or enum should exist.
* Do not ignore compiler warnings.
* Do not leave commented-out code in committed changes.
* Do not use `var` when the resulting type is not obvious from the right-hand side.

```csharp
// Bad
public static bool AutoSaveEnabled =>
    ServerConfig.Instance.AutoSaveEnabled;

// Good
bool autoSaveEnabled = ServerConfig.Instance.AutoSaveEnabled;
```

```csharp
// Bad
if (messageType == "exec_console")
{
}

// Good
if (messageType == Constants.Messages.EXEC_CONSOLE)
{
}
```

## Validation Expectations

Before finishing a meaningful change:

* Build the relevant configuration or configurations.
* Keep XML documentation warnings clean.
* Regenerate DocFX output when public API docs or docs structure changed.
* Update related docs when a public API or configuration behavior changes.

If you are unsure about a standard:

1. Check the existing codebase for patterns.
2. Prefer the established DedicatedServerMod convention when there is a conflict.
3. Use S1API as style inspiration, not as a reason to ignore this repo's side-aware and framework-boundary requirements.

These standards exist to keep the codebase maintainable and professional. They are not about personal preference; they are about consistency and quality for contributors and integrators.
