## Gameplay settings

- `ignoreGhostHostForSleep`: ignore the server loopback client in sleep readiness.
- `allowSleeping`: allow players to use beds to skip time. Bed sleep can still desync clients, so test carefully.
- `pauseGameWhenEmpty`: when true, pauses the world simulation while no players are connected.
- `timeProgressionMultiplier`: speed multiplier for time progression. Keep this above zero.
- `freshSaveQuestBootstrapMode`: choose how a fresh save starts the vanilla quest line.
- `StartFromBeginning`: recommended for most servers. This starts with `Welcome to Hyland Point` and follows the native game's intended first-time quest flow.
- `StartPostIntro`: starts later at the `Getting Started` checkpoint, after the section where players collect cash from deaddrops and the RV explosion/fix event happens. This keeps the RV in its repaired state, but skips early opening beats and is not recommended for most players.

The dedicated server already patches headless time flow to avoid the old 04:00 stall behavior, so there is no separate `timeNeverStops` option in the current config surface.
