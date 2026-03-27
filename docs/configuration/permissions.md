## Permissions

There are three permission levels: Players, Admins, and Operators. Which console commands each level can run is controlled entirely by `server_config.json`.

### Roles
- Players: default role for everyone not listed as admin/operator.
- Admins: elevated users; a subset of operator permissions.
- Operators: highest level; full server control.

### Command execution rules
- Global denies: `globalDisabledCommands` are denied for everyone (including operators).
- Operators: can run all commands except those in `globalDisabledCommands`.
- Admins: can run only commands in `allowedCommands` and are blocked from any in `restrictedCommands` and `globalDisabledCommands`.
- Players: can run only commands in `playerAllowedCommands` (and are still blocked by `globalDisabledCommands`).

### Console access vs. command permission
- Opening the console UI on dedicated servers is controlled by:
  - `enableConsoleForOps`, `enableConsoleForAdmins`, `enableConsoleForPlayers`.
- Even if a user can open the console, each command is still checked against the rules above. Opening ≠ permission to execute.

### Configure in `server_config.json`
- `operators`: ["SteamID64", ...]
- `admins`: ["SteamID64", ...]
- `bannedPlayers`: ["SteamID64", ...]
- `allowedCommands`: commands admins may use
- `restrictedCommands`: commands denied to admins (operators still allowed)
- `playerAllowedCommands`: commands regular players may use
- `globalDisabledCommands`: commands denied for everyone
- `enableConsoleForOps` / `enableConsoleForAdmins` / `enableConsoleForPlayers`: who can open the console UI

