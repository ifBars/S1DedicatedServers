## Gameplay Settings

Gameplay settings live under `[gameplay]` in `server_config.toml`.

```toml
[gameplay]
ignoreGhostHostForSleep = true
timeProgressionMultiplier = 1.0
allowSleeping = true
pauseGameWhenEmpty = false
freshSaveQuestBootstrapMode = 'StartFromBeginning'
```

## Setting Reference

| Key | Type | Default | Notes |
| --- | --- | --- | --- |
| `ignoreGhostHostForSleep` | `bool` | `true` | Ignores the dedicated-server loopback client when checking whether real players are ready to sleep. Keep this enabled for normal servers. |
| `timeProgressionMultiplier` | `float` | `1.0` | Multiplies authoritative time progression. Keep this above zero. |
| `allowSleeping` | `bool` | `true` | Allows players to use beds to skip time. Test heavily on modded servers because sleep touches quests, time, and client UI state. |
| `pauseGameWhenEmpty` | `bool` | `false` | Pauses simulation while no real players are connected. Useful for private worlds that should not progress unattended. |
| `freshSaveQuestBootstrapMode` | `string` | `StartFromBeginning` | Chooses how a fresh prepared save initializes the vanilla quest line. |

## Fresh Save Quest Bootstrap

`freshSaveQuestBootstrapMode` has two supported values:

- `StartFromBeginning`: recommended for most servers. Starts with `Welcome to Hyland Point` and follows the native first-time quest flow.
- `StartPostIntro`: starts later at the `Getting Started` checkpoint, after the deaddrop cash pickup and RV explosion/fix sequence. This keeps the RV repaired, but skips early opening beats and is not recommended for most players.

Changing this setting does not rewrite an already-progressed save. Treat it as a fresh-save bootstrap choice.

## Time And Sleep Notes

The dedicated server already patches headless time flow to avoid the old 04:00 stall behavior, so there is no separate `timeNeverStops` option in the current config surface.

Use `settime <HHMM>` for one-off recovery or testing, for example `settime 1800`. Use `timeProgressionMultiplier` for persistent time tuning.

## Related Documentation

- [Auto-Save](autosave.md)
- [Time and Sleep Troubleshooting](../troubleshooting/time-sleep.md)
- [Server Commands](../commands/server-commands.md)
