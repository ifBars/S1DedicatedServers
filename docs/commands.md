## Commands

Console commands are executed in-game. On dedicated servers, only authorized players may use console commands.

### Roles and permissions
- Players: may execute only commands listed in `playerAllowedCommands`.
- Admins: may execute commands listed in `allowedCommands`, except those in `restrictedCommands`.
- Operators: full access to all commands except those in `globalDisabledCommands`.

### Precedence and global rules
- `globalDisabledCommands` are denied for everyone (including operators).
- Being able to open the console UI is gated by `enableConsoleForOps`, `enableConsoleForAdmins`, and `enableConsoleForPlayers`, but every command still undergoes the checks above. See [Permissions](configuration/permissions.md) for details.

### Examples (typical)
- `save` – Triggers a save.
- `shutdown` – Gracefully stops the server.
- `server.info` – Prints server status.
- `op <steamId>` – Grants operator.
- `deop <steamId>` – Revokes operator.
- `ban <steamId>` – Bans a player.
- `unban <steamId>` – Unbans a player.
- `kick <steamId>` – Kicks a player.
- `players` – Lists connected players.

Most base game commands should work as expected.


