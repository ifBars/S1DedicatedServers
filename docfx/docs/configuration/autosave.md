## Auto-Save

Auto-save settings live under `[autosave]` in `server_config.toml`.

```toml
[autosave]
autoSaveEnabled = true
autoSaveIntervalMinutes = 15.0
autoSaveOnPlayerJoin = true
autoSaveOnPlayerLeave = true
```

Saves are serialized to the folder that `LoadManager` loaded for the active session. That is either the resolved `[storage].saveGamePath` value or the server-managed default save folder when the setting is empty.

## Setting Reference

| Key | Type | Default | Notes |
| --- | --- | --- | --- |
| `autoSaveEnabled` | `bool` | `true` | Enables timed auto-save. |
| `autoSaveIntervalMinutes` | `float` | `15.0` | Minutes between timed saves. Keep enough spacing for save/load spikes on small hosts. |
| `autoSaveOnPlayerJoin` | `bool` | `true` | Triggers a save after player join handling. Useful for preserving join-time world state. |
| `autoSaveOnPlayerLeave` | `bool` | `true` | Triggers a save after player disconnect handling. Useful for private worlds where the last player leaving should persist progress. |

## Manual Saves

Use the `save` command for an immediate save:

```text
save
```

The built-in `administrator` group can run `save` by default through the `server.save` permission node. If you customize `permissions.toml`, grant `server.save` rather than a generic command node.

## Operational Notes

- Auto-save does not replace backups. Keep separate copies of important save folders before large updates or mod-stack changes.
- Very short intervals can amplify save spikes on low-CPU hosts.
- If saves fail, verify `[storage].saveGamePath` first, then check MelonLoader logs for persistence errors.

## Related Documentation

- [Save Path](save-path.md)
- [Server Commands](../commands/server-commands.md)
- [Save and Load Troubleshooting](../troubleshooting/save-load.md)
