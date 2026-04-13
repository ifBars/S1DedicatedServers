## Commands

Console commands on dedicated servers are controlled by the permissions system, not by hard-coded role lists in `server_config.toml`.

- `permissions.toml` defines who can open the console and which nodes they can execute
- `console.open` gates console access
- most ordinary commands evaluate against `console.command.<commandword>`
- framework management commands also expose dedicated nodes such as `server.save`, `player.kick`, and `permissions.reload`

The built-in group defaults are:

- `default`: help
- `support`: server info
- `moderator`: player moderation commands
- `administrator`: config reload and save
- `operator`: permission mutation and shutdown

You can change that layout by editing `permissions.toml`.

## Common Commands

- `help`: show command help
- `serverinfo`: print server status
- `save`: trigger a manual save
- `reloadconfig`: reload `server_config.toml`
- `reloadpermissions`: reload `permissions.toml`
- `listplayers`: list connected players
- `kick <player_name_or_id> [reason]`: disconnect a player
- `ban <player_name_or_id> [reason]`: ban a player
- `unban <steamid>`: remove a ban
- `group <list|assign|unassign> ...`: manage permission groups
- `perm <info|grant|deny|revoke|tempgrant> ...`: inspect and mutate direct permission nodes
- `op`, `deop`, `admin`, `deadmin`: compatibility wrappers for built-in staff groups

See [Permissions](configuration/permissions.md) for file structure, group defaults, and rule precedence.
