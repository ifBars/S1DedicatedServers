## Player Management

These commands are permission-node based. The defaults below assume the built-in group layout from `permissions.toml`.

- `listplayers`: list connected players, status, and effective staff group. Default group: `moderator`
- `kick <player_name_or_id> [reason]`: disconnect a player. Default group: `moderator`
- `ban <player_name_or_id> [reason]`: ban a player. Default group: `moderator`
- `unban <steamid>`: remove a ban. Default group: `moderator`

Use SteamID64 values when specifying offline players or editing `permissions.toml` manually.
