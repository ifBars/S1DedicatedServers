## Server Commands

These commands affect server state directly. The defaults listed below assume the built-in permission groups.

- `help`: display command help. Default group: `default`
- `serverinfo`: display server status. Default group: `support`
- `save`: trigger a manual save. Default group: `administrator`
- `reloadconfig`: reload `server_config.toml`. Default group: `administrator`
- `reloadpermissions`: reload `permissions.toml`. Default group: `operator`
- `shutdown [reason]`: gracefully stop the server. Default group: `operator`

If your server customizes group rules, the actual access level can differ from these defaults.
