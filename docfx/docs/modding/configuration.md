---
title: Addon Configuration
---

## Addon Configuration

DedicatedServerMod now exposes the same TOML platform that powers `server_config.toml` and `permissions.toml`.

Use the typed configuration API for normal addon settings. Use the low-level document API only when your file shape is genuinely dynamic.

If you have not used a C# builder or fluent API before, read the typed example from top to bottom as "start a schema, add one section at a time, describe each option, then call `Build()` at the end." Each chained call returns the same builder so you can keep adding configuration rules in a single block.

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

TomlConfigSchema<MyAddonConfig> addonConfigSchema = TomlConfigSchemaBuilder
    .For<MyAddonConfig>()
    .FileHeader(
        "Configuration for MyAddon.",
        "Edit this file while the server is stopped when possible.")
    .Section("general", generalSection => generalSection
        .Comment("Core behavior.")
        .Option(config => config.Enabled, optionBuilder => optionBuilder
            .Key("enabled")
            .Comment("Enable or disable the addon.")
            .Default(true))
        .Option(config => config.ChannelName, optionBuilder => optionBuilder
            .Key("channelName")
            .Comment("Channel used for status broadcasts.")
            .Default("global"))
        .Option(config => config.BroadcastTargets, optionBuilder => optionBuilder
            .Key("broadcastTargets")
            .Comment("Targets that should receive addon broadcasts.")))
    .Normalize(normalizedConfig =>
    {
        normalizedConfig.ChannelName = (normalizedConfig.ChannelName ?? "global").Trim();
    })
    .Validate(loadedConfig =>
    {
        if (string.IsNullOrWhiteSpace(loadedConfig.ChannelName))
        {
            return new[]
            {
                new TomlConfigValidationIssue("general", "channelName", "channelName cannot be empty.")
            };
        }

        return Array.Empty<TomlConfigValidationIssue>();
    })
    .Build();

TomlConfigStore<MyAddonConfig> addonConfigStore = new TomlConfigStore<MyAddonConfig>(
    addonConfigSchema,
    new TomlConfigStoreOptions<MyAddonConfig>
    {
        Path = ModConfigPaths.GetDefault("MyAddon"),
        CreateInstance = () => new MyAddonConfig(),
        SaveOnNormalize = true
    });

TomlConfigLoadResult<MyAddonConfig> addonConfigLoadResult = addonConfigStore.LoadOrCreate();
MyAddonConfig addonConfig = addonConfigLoadResult.Config;

if (addonConfigLoadResult.RequiresSave)
{
    addonConfigStore.Save(addonConfig);
}
```

### Reading The Typed Example

The quick-start example above is doing four separate jobs:

1. `MyAddonConfig` defines the in-memory settings object your mod reads at runtime.
2. `TomlConfigSchemaBuilder.For<MyAddonConfig>()` describes how that object maps to TOML.
3. `TomlConfigStore<MyAddonConfig>` decides where the file lives and how instances are created.
4. `LoadOrCreate()` reads the file, applies defaults/normalization/validation, and gives you the usable config object.

The lambda expressions are just selectors and configuration callbacks:

- `config => config.Enabled` means "this TOML option maps to the `Enabled` property on `MyAddonConfig`"
- `generalSection => ...` means "configure the `general` TOML section here"
- `optionBuilder => ...` means "configure this one option here"
- `normalizedConfig => ...` means "clean up loaded values before the mod uses them"
- `loadedConfig => ...` means "inspect the final loaded config and report validation issues"

If those parameter names feel verbose, that is intentional in docs. Use names that describe the role of the object being configured so the code still reads well months later.

### What The Typed Layer Gives You

- `FileHeader(...)`: generates top-of-file comments
- `Section(...)`: controls section order
- `Comment(...)`: emits inline documentation into the TOML file
- `Key(...)`: overrides the persisted TOML key name
- `Alias(...)`: accepts old key names during load
- `Default(...)`: fills missing values without forcing you to hard-code them elsewhere
- `Normalize(...)`: clamps or cleans values after binding
- `Validate(...)`: returns structured `TomlConfigValidationIssue` records

In the property selector passed to `Option(...)`, prefer names like `config`, `settings`, or `addonConfig` over placeholder names. The selector is only there to identify which property the schema should bind.

## Typed Config Example With Alias And Option-Level Validation

This slightly larger example shows a few features new addon authors often need right away:

```csharp
using System;
using DedicatedServerMod.API.Configuration;

public sealed class DiscordRelayConfig
{
    public bool Enabled { get; set; } = true;

    public string WebhookUrl { get; set; } = string.Empty;

    public int FlushIntervalSeconds { get; set; } = 30;
}

TomlConfigSchema<DiscordRelayConfig> discordRelaySchema = TomlConfigSchemaBuilder
    .For<DiscordRelayConfig>()
    .FileHeader(
        "Discord relay settings.",
        "Restart the addon after changing webhook or interval settings.")
    .Section("general", generalSection => generalSection
        .Comments(
            "Core relay behavior.",
            "Keys stay in a stable order when the file is rewritten.")
        .Option(config => config.Enabled, optionBuilder => optionBuilder
            .Key("enabled")
            .Comment("Enable or disable Discord relays.")
            .Default(true))
        .Option(config => config.WebhookUrl, optionBuilder => optionBuilder
            .Key("webhookUrl")
            .Alias("discordWebhook")
            .Comments(
                "Discord webhook used for outbound relay messages.",
                "Leave empty to disable delivery until the webhook is configured.")
            .Default(string.Empty))
        .Option(config => config.FlushIntervalSeconds, optionBuilder => optionBuilder
            .Key("flushIntervalSeconds")
            .Comment("How often queued relay messages are flushed.")
            .Default(30)
            .Validate(value => value < 5 || value > 300
                ? "flushIntervalSeconds must stay between 5 and 300."
                : null)))
    .Normalize(loadedConfig =>
    {
        loadedConfig.WebhookUrl = (loadedConfig.WebhookUrl ?? string.Empty).Trim();
    })
    .Build();
```

What to notice:

- `Alias("discordWebhook")` lets older files keep loading after a key rename.
- `optionBuilder.Validate(...)` is useful when the rule only applies to one field.
- schema-level `Validate(...)` is better when multiple fields must be checked together.
- `Normalize(...)` is the right place for trimming, clamping, and default cleanup.

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

string rulesFilePath = ModConfigPaths.GetPath("MyAddon", "rules.toml");

TomlDocument rulesDocument = File.Exists(rulesFilePath)
    ? TomlParser.ParseFile(rulesFilePath).Document
    : new TomlDocument();

TomlTable spawnRateRuleTable = rulesDocument.GetOrAddTable("rule.spawn-rate");
spawnRateRuleTable.Comments.Clear();
spawnRateRuleTable.Comments.Add("Per-rule overrides.");
spawnRateRuleTable.Set("enabled", TomlValue.FromBoolean(true));
spawnRateRuleTable.Set("multiplier", TomlValue.FromFloat(1.5));
spawnRateRuleTable.Set("tags", TomlValue.FromArray(new[]
{
    TomlValue.FromString("night"),
    TomlValue.FromString("event")
}));

TomlWriter.WriteFile(rulesDocument, rulesFilePath);
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
