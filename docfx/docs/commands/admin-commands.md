## Admin Commands

These commands are usually used by moderator, administrator, or operator staff groups. Exact access comes from `permissions.toml`, so your server can widen or narrow them without recompiling the mod.

## Group Management

- `group list`: list all known permission groups
- `group assign <player_name_or_steamid> <group>`: assign a group to a user
- `group unassign <player_name_or_steamid> <group>`: remove a group from a user
- `op <player_name_or_steamid>`: compatibility wrapper that assigns the built-in `operator` group
- `deop <player_name_or_steamid>`: compatibility wrapper that removes the built-in `operator` group
- `admin <player_name_or_steamid>`: compatibility wrapper that assigns the built-in `administrator` group
- `deadmin <player_name_or_steamid>`: compatibility wrapper that removes the built-in `administrator` group
- `listops`: list users directly assigned to the built-in `operator` group
- `listadmins`: list users directly assigned to the built-in `administrator` group

## Direct Node Management

- `perm info [player_name_or_steamid]`: show permission summary or inspect one subject
- `perm grant <player_name_or_steamid> <node>`: add a direct allow rule
- `perm deny <player_name_or_steamid> <node>`: add a direct deny rule
- `perm revoke <player_name_or_steamid> <node>`: remove a direct rule
- `perm tempgrant <player_name_or_steamid> <node> <minutes> [reason]`: add a temporary allow rule

## Player Moderation

- `listplayers`: list connected players and their effective staff group
- `kick <player_name_or_id> [reason]`: disconnect a player
- `ban <player_name_or_id> [reason]`: ban a player
- `unban <steamid>`: remove a ban
- `bring <player_name_or_id> [destination_player_name_or_id]`: teleport a player to your position or another player's position
- `return [player_name_or_id]`: return yourself or another player to the last saved teleport position
- `vanish [player_name_or_id] [on|off|toggle]`: toggle or set vanish mode

## Notes

- `group` and `perm` are the primary long-term management commands.
- `op`, `admin`, `deop`, and `deadmin` remain for compatibility and convenience.
- A user still needs enough dominance to act on another user. Holding the right node does not let a lower-priority staff member manage a higher-priority one.
