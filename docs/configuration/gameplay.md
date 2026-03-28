## Gameplay settings

- `ignoreGhostHostForSleep`: ignore the server loopback client in sleep readiness.
- `allowSleeping`: allow players to use beds to skip time. Bed sleep can still desync clients, so test carefully.
- `pauseGameWhenEmpty`: when true, pauses the world simulation while no players are connected.
- `timeProgressionMultiplier`: speed multiplier for time progression. Keep this above zero.

The dedicated server already patches headless time flow to avoid the old 04:00 stall behavior, so there is no separate `timeNeverStops` option in the current config surface.
