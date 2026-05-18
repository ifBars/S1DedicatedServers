## Save/Load

Symptoms and fixes:

- Failed to load save info: set `saveGamePath`; make sure the path isn't invalid.
- Load errors, player-state issues, messaging/UI exceptions, or repeat client hitching after importing a normal game save: start a fresh server-managed save first, then copy world files into it without replacing `Players\Player_0`. The dedicated server expects `Player_0` to be its loopback host data. See [Importing an existing single-player save](../configuration/save-path.md#importing-an-existing-single-player-save).
- Otherwise, open an issue on the [GitHub repository](https://github.com/ifBars/S1DedicatedServers/issues).


