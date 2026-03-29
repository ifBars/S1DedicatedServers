---
title: Addon Configuration
---

## Addon Configuration

DedicatedServerMod now exposes the same TOML platform that powers `server_config.toml` and `permissions.toml`.

Use the typed configuration API for normal addon settings. Use the low-level document API only when your file shape is genuinely dynamic.

## Choose The Right API

Use `DedicatedServerMod.API.Configuration` when:

- you have a normal settings object
- the keys and sections are known ahead of time
- you want generated comments and deterministic ordering
- you want validation, aliases, defaults, and normalization

Use `DedicatedServerMod.API.Toml` when:

- your file contains user-defined table names
- you need a document model similar to `permissions.toml`
- you are building a mapper over tables like `rule.<id>` or `profile.<name>`
- you want direct access to comments, tables, entries, and value kinds

## Typed Config Quick Start

This is the recommended path for most addons.

```csharp
using System;
using System.Collections.Generic;
using DedicatedServerMod.API.Configuration;

public sealed class MyAddonConfig
{
    public bool Enabled { get; set; } = true;

    public string ChannelName { get; set; } = "global";

    public List<string> BroadcastTargets { get; set; } = new List<string>
    {
        "discord"
    };
}

TomlConfigSchema<MyAddonConfig> schema = TomlConfigSchemaBuilder
    .For<MyAddonConfig>()
    .FileHeader(
        "Configuration for MyAddon.",
        "Edit this file while the server is stopped when possible.")
    .Section("general", section => section
        .Comment("Core behavior.")
        .Option(x => x.Enabled, option => option
            .Key("enabled")
            .Comment("Enable or disable the addon.")
            .Default(true))
        .Option(x => x.ChannelName, option => option
            .Key("channelName")
            .Comment("Channel used for status broadcasts.")
            .Default("global"))
        .Option(x => x.BroadcastTargets, option => option
            .Key("broadcastTargets")
            .Comment("Targets that should receive addon broadcasts.")))
    .Normalize(config =>
    {
        config.ChannelName = (config.ChannelName ?? "global").Trim();
    })
    .Validate(config =>
    {
        if (string.IsNullOrWhiteSpace(config.ChannelName))
        {
            return new[]
            {
                new TomlConfigValidationIssue("general", "channelName", "channelName cannot be empty.")
            };
        }

        return Array.Empty<TomlConfigValidationIssue>();
    })
    .Build();

TomlConfigStore<MyAddonConfig> store = new TomlConfigStore<MyAddonConfig>(
    schema,
    new TomlConfigStoreOptions<MyAddonConfig>
    {
        Path = ModConfigPaths.GetDefault("MyAddon"),
        CreateInstance = () => new MyAddonConfig(),
        SaveOnNormalize = true
    });

TomlConfigLoadResult<MyAddonConfig> loadResult = store.LoadOrCreate();
MyAddonConfig config = loadResult.Config;

if (loadResult.RequiresSave)
{
    store.Save(config);
}
```

### What The Typed Layer Gives You

- `FileHeader(...)`: generates top-of-file comments
- `Section(...)`: controls section order
- `Comment(...)`: emits inline documentation into the TOML file
- `Key(...)`: overrides the persisted TOML key name
- `Alias(...)`: accepts old key names during load
- `Default(...)`: fills missing values without forcing you to hard-code them elsewhere
- `Normalize(...)`: clamps or cleans values after binding
- `Validate(...)`: returns structured `TomlConfigValidationIssue` records

### Load Result Behavior

`TomlConfigLoadResult<TConfig>` tells you what happened during load:

- `Config`: the typed config object
- `WasCreated`: the file did not exist and had to be created
- `WasNormalized`: schema or store normalization ran
- `RequiresSave`: managed keys were missing or normalization wants a rewrite
- `MissingManagedKeys`: canonical keys missing from the source document
- `UsedAliases`: alias keys consumed during binding
- `Diagnostics`: parse warnings from the TOML reader
- `ValidationIssues`: binding and validation warnings from the typed layer

## Standard Addon Paths

Use `ModConfigPaths` instead of inventing your own folder convention:

- `ModConfigPaths.GetDefault("MyAddon")` returns `UserData/DedicatedServerMod/Mods/MyAddon/config.toml`
- `ModConfigPaths.GetPath("MyAddon", "profiles.toml")` gives you another addon-scoped file in the same folder

That keeps addon config files predictable for server owners.

## When To Use The Low-Level Document API

The document API is a better fit when your file shape is dynamic. `permissions.toml` is the model here: the framework cannot describe `[group.<name>]` or `[ban.<subjectId>]` with a fixed typed schema, so it reads and writes named tables directly.

Example:

```csharp
using System.IO;
using DedicatedServerMod.API.Toml;

string path = ModConfigPaths.GetPath("MyAddon", "rules.toml");

TomlDocument document = File.Exists(path)
    ? TomlParser.ParseFile(path).Document
    : new TomlDocument();

TomlTable ruleTable = document.GetOrAddTable("rule.spawn-rate");
ruleTable.Comments.Clear();
ruleTable.Comments.Add("Per-rule overrides.");
ruleTable.Set("enabled", TomlValue.FromBoolean(true));
ruleTable.Set("multiplier", TomlValue.FromFloat(1.5));
ruleTable.Set("tags", TomlValue.FromArray(new[]
{
    TomlValue.FromString("night"),
    TomlValue.FromString("event")
}));

TomlWriter.WriteFile(document, path);
```

Useful low-level types:

- `TomlDocument`: root document plus ordered tables
- `TomlTable`: named table with ordered entries and typed getters
- `TomlEntry`: one key, one value, plus leading comments
- `TomlValue`: typed wrapper for string, bool, integer, float, or array values
- `TomlParser`: parse text or files
- `TomlWriter`: write text or files

## Supported TOML Subset

The reusable platform intentionally supports a constrained TOML subset in this release:

- quoted strings
- booleans
- integers
- floats
- arrays
- named tables
- comments

The typed schema layer currently converts these CLR types cleanly:

- `string`
- `bool`
- `int`
- `long`
- `float`
- `double`
- enums
- `List<string>`
- `HashSet<string>`

What it does not target yet:

- inline tables
- native TOML datetime tokens
- arbitrary nested object graphs
- codegen-heavy binding tricks that would be hostile to Mono or IL2CPP

## Practical Guidance

- Use the typed API unless you truly need dynamic table names.
- Keep the config class as the source of runtime state. Do not duplicate defaults in separate parsing helpers.
- Put migration aliases in the schema instead of carrying ad hoc compatibility code around your mod.
- Surface validation issues in logs so server owners know what was ignored or corrected.
- If you invent your own document format, keep it small and comment-rich. Server owners will edit these files by hand.

## Related Documentation

- [Configuration Overview](../configuration.md)
- [Permissions](../configuration/permissions.md)
- [Mod API Overview](overview.md)
