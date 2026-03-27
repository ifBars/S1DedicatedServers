## Admin commands

These commands require elevated roles. See each command's required role.

- `op <player_name_or_steamid>` – Grant operator privileges. (Operator)
- `deop <player_name_or_steamid>` – Revoke operator privileges. (Operator)
- `admin <player_name_or_steamid>` – Grant administrator privileges. (Operator)
- `deadmin <player_name_or_steamid>` – Revoke administrator privileges. (Operator)

- `listops` – List configured operators and whether online. (Operator)
- `listadmins` – List configured administrators and whether online. (Operator)
- `listplayers` – List connected players with status and roles. (Administrator)

- `kick <player_name_or_id> [reason]` – Disconnect a player. (Administrator)
- `ban <player_name_or_id> [reason]` – Ban a player. (Operator)
- `unban <steamid>` – Remove a ban by SteamID. (Operator)

Notes:

- Admin execution is also constrained by `allowedCommands` and `restrictedCommands`.
- Operators can run all commands except those in `globalDisabledCommands`.


