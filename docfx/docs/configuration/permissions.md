## Permissions

DedicatedServerMod now stores runtime authorization in `UserData/permissions.toml`.

Use `server_config.toml` for server runtime settings such as ports, auth, autosave, and gameplay. Use `permissions.toml` for:

- bans
- built-in staff groups
- direct user grants and denies
- temporary grants
- console command access

On first run, the file is created automatically. If the server finds legacy permission fields in `server_config.toml` or `server_config.json`, it migrates them into `permissions.toml` and future edits should be made there.

## Built-In Groups

The default seeded group chain is:

- `default`: basic read-only server access such as `server.help`
- `support`: inherits `default` and adds `server.info`
- `moderator`: inherits `support` and adds player moderation commands such as `player.list`, `player.kick`, `player.ban`, `player.unban`, `player.bring`, and `player.return`
- `administrator`: inherits `moderator` and adds server maintenance nodes such as `server.save`, `server.reloadconfig`, `permissions.info`, `permissions.group.list`, and `player.vanish`
- `operator`: inherits `administrator` and adds high-impact nodes such as `server.stop`, `permissions.reload`, `permissions.group.assign`, `permissions.group.unassign`, `permissions.grant`, `permissions.deny`, `permissions.revoke`, and `permissions.tempgrant`

You can extend the graph with your own groups and custom permission nodes.

## How Evaluation Works

Permission checks are node based, not role-list based.

- `console.open` controls whether a player can open the remote admin console UI
- most console commands map to `console.command.<commandword>`
- framework management commands also expose dedicated nodes such as `server.save`, `player.kick`, and `permissions.reload`

When multiple rules match the same request, the service resolves them in this order:

1. More specific rules beat broader wildcard rules.
2. Direct user rules beat group rules at the same specificity.
3. Higher-priority groups beat lower-priority groups when specificity ties.
4. Denies beat allows when everything else ties.

Wildcards are supported. Examples:

- `console.command.*`
- `permissions.*`
- `*`

## File Layout

`permissions.toml` is document-driven, so sections are grouped by what they describe:

```toml
[metadata]
schemaVersion = 1
migrationVersion = 1
migratedFrom = 'default'
migratedAtUtc = '2026-03-29T12:00:00.0000000Z'

[group.default]
priority = 0
allow = ['server.help']
deny = []

[group.moderator]
priority = 20
inherits = ['support']
allow = ['player.list', 'player.kick', 'player.ban', 'player.unban', 'player.bring', 'player.return']
deny = []

[group.operator]
priority = 40
inherits = ['administrator']
allow = ['server.stop', 'permissions.reload', 'permissions.group.assign']
deny = []

[user.76561198012345678]
groups = ['operator']
allow = ['console.command.cleartrash']
deny = []

[tempallow.maintenance-window]
userId = '76561198012345678'
node = 'console.command.shutdown'
expiresAtUtc = '2026-03-29T18:30:00.0000000Z'
grantedBy = 'console'
reason = 'maintenance window'

[ban.76561198087654321]
subjectId = '76561198087654321'
createdAtUtc = '2026-03-29T14:00:00.0000000Z'
createdBy = 'console'
reason = 'griefing'
```

Section families:

- `[metadata]`: schema and migration metadata
- `[group.<name>]`: group priority, inheritance, allow, and deny rules
- `[user.<steamid>]`: direct groups plus direct allow and deny rules
- `[tempgroup.<id>]`: temporary group assignments
- `[tempallow.<id>]`: temporary allow grants
- `[tempdeny.<id>]`: temporary deny grants
- `[ban.<subjectId>]`: ban entries

Temporary assignment IDs can be any stable identifier. The framework preserves unknown sections and comments, so hand-edited files round-trip cleanly.

## Useful Commands

These commands work with the live permissions graph:

- `reloadpermissions`: reload `permissions.toml` without restarting
- `group list`: list all known groups
- `group assign <player_or_steamid> <group>`: assign a group
- `group unassign <player_or_steamid> <group>`: remove a group
- `perm info <player_or_steamid>`: inspect effective and direct rules
- `perm grant <player_or_steamid> <node>`: add a direct allow rule
- `perm deny <player_or_steamid> <node>`: add a direct deny rule
- `perm revoke <player_or_steamid> <node>`: remove a direct allow or deny rule
- `perm tempgrant <player_or_steamid> <node> <minutes> [reason]`: add a temporary allow rule
- `op`, `deop`, `admin`, `deadmin`: compatibility wrappers around built-in group assignment

See [Commands](../commands.md) for the command catalog.

## Migration Notes

- `server_config.toml` no longer owns live permissions.
- A legacy `[permissions]` section in `server_config.toml` is treated as migration-only data and is removed from the normalized config file.
- Legacy `operators`, `admins`, `bannedPlayers`, `allowedCommands`, `restrictedCommands`, `playerAllowedCommands`, and console-access flags are read only so the server can seed `permissions.toml` on first run.

## Related Documentation

- [Configuration Overview](../configuration.md)
- [Authentication](authentication.md)
- [Commands](../commands.md)
