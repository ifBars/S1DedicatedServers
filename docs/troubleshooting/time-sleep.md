## Time & Sleep

- Time stuck at 04:00: check the server log for `TimeManager` patch errors and keep `timeProgressionMultiplier` above zero.
- Clients out-of-sync: wait 1 minute and check again. If the issue persists, open an issue on the [GitHub repository](https://github.com/ifBars/S1DedicatedServers/issues).
- Sleeping says waiting for players: ensure `allowSleeping` is true and `ignoreGhostHostForSleep` is true so the loopback client is ignored. Sleeping in a bed can still desync clients, so use that flow carefully on public servers.
