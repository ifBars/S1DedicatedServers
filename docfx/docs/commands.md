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

## Opening the In-Game Console

The default keyboard binding is **Backquote** (the grave-accent key, usually below Esc and left of `1` on a US keyboard). It shares a key with `~`, but you do not need to hold Shift. DedicatedServerMod uses the game's console toggle action; it does not add a separate console hotkey. Keyboard layouts and customized bindings can affect which key you use.

1. Install the DedicatedServerMod **client** build matching your game's Mono or IL2CPP runtime, and use compatible client/server mod versions. Installing only the server mod does not enable remote players' console UI.
2. On the server, allow `console.open` for the player's group in `UserData/permissions.toml`, and ensure the player's user entry belongs to that group. Adding a permission to a group does not assign players to it. See [Permissions](configuration/permissions.md) for membership and rule precedence.
3. After editing the file while the server is running, run `reloadpermissions` from the server console. This reloads permissions and refreshes connected players' capability snapshots. `reloadconfig` reloads a different file.
4. Join the dedicated server, finish loading, close the pause menu, and press Backquote. Press it again or use Esc to close the console.

`console.open` only permits opening the UI. Commands still require their own permission nodes; you do not need to grant `console.command.*` or make someone an operator just to open it.

### If the Console Still Does Not Open

- Confirm the client mod loaded successfully in the client's `MelonLoader/Latest.log`.
- Check the player's group membership and any matching denies in `permissions.toml`. A group allow can be overridden according to the permission rules.
- With debug logging enabled, check the client's `Permission capability snapshot updated: open=True` message. `open=False` means the latest snapshot does not authorize console access; no snapshot message means permission delivery or client initialization needs investigation.
- The old `Could not find ConsoleUI.IS_CONSOLE_ENABLED property` error is associated with [issue #47](https://github.com/ifBars/S1DedicatedServers/issues/47). Update the client mod if that error appears. Current code supports both `IsConsoleEnabled` and the legacy property name; a similar symptom alone does not establish the same cause.

If access remains broken, include the game version, client and server mod versions, Mono or IL2CPP runtime, keyboard layout, and relevant client log messages in the report. Note whether the console never appears or opens and immediately closes.

## Common Commands

- `help`: show command help
- `serverinfo`: print server status
- `save`: trigger a manual save
- `reloadconfig`: reload `server_config.toml`
- `reloadpermissions`: reload `permissions.toml`
- `shutdown [reason]`: gracefully stop the server
- `settime <HHMM>`: set the authoritative server time of day
- `settimescale <scale>`: set the authoritative Unity time scale
- `listplayers`: list connected players
- `kick <player_name_or_id> [reason]`: disconnect a player
- `ban <player_name_or_id> [reason]`: ban a player
- `unban <steamid>`: remove a ban
- `group <list|assign|unassign> ...`: manage permission groups
- `perm <info|grant|deny|revoke|tempgrant> ...`: inspect and mutate direct permission nodes
- `op`, `deop`, `admin`, `deadmin`: compatibility wrappers for built-in staff groups

`shutdown` waits for any active save, then completes a final save before disconnecting players or stopping game systems. If the final save reports an error, shutdown is cancelled and the server stays running. Resolve the save error and retry. A save that takes longer than 30 seconds logs a warning and continues waiting. Stopping the process externally cannot wait for a final save.

`settime` expects a four-digit game clock value such as `0800`, `1330`, or `1800`. `settimescale` changes Unity's global time scale and should be treated as an operator-only diagnostic or recovery tool, not normal gameplay tuning.

See [Permissions](configuration/permissions.md) for file structure, group defaults, and rule precedence.
