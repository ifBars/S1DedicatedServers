## Host Console

DedicatedServerMod supports two host console transports:

- TCP console for direct socket-based remote administration
- stdio host console for platforms that inject commands through process stdin and capture logs from stdout/stderr

The optional web panel is a separate loopback-only surface intended for local operators on the server host. Hosted panels should usually keep it disabled and rely on stdio instead.

For native Windows server installs, the packaged `start_server.bat` enables `--stdio-console` by default. That lets operators type DedicatedServerMod commands directly into the MelonLoader console without opening a separate TCP console session.

### Recommended mode

Use:

```toml
[tcpConsole]
stdioConsoleMode = 'Auto'
```

`Auto` starts the stdio host console only when stdin is redirected. That matches hosted environments while avoiding accidental takeover of the local interactive console on normal desktop launches.

### Pterodactyl and Wings

Pterodactyl-style hosts manage the game server as a process whose lifecycle and console are exposed through a control plane. In that environment the server should behave well as a line-oriented stdin/stdout process rather than assuming telnet-style administration or a visible local desktop console.

Reference: [Pterodactyl Wings README](https://github.com/pterodactyl/wings)

### Startup guidance

For panel environments, prefer startup flags that keep Unity and MelonLoader output on stdout:

```text
-logFile -
```

That lets the panel capture the game log stream directly. Hidden desktop console settings are not a substitute for stdout logging in hosted environments.

Some Wine/panel setups do not expose Unity output with `-logFile -`. In that case, use a writable Windows path visible to the game, such as `-logFile "Z:\home\container\UnityPlayer.log"`, and retain that file alongside `MelonLoader/Latest.log`. See [Wine launch settings](configuration/startup-and-deployment.md#linux-wine-and-hosted-panels) for UTF-8 locale requirements and log-path details.

If you launch the executable manually instead of using the packaged batch file, add `--stdio-console` to force the stdio host console on:

```batch
"Schedule I.exe" --batchmode --nographics --dedicated-server --stdio-console
```

### Behavior

- `stdin` lines are parsed with the same command grammar used by TCP console and admin relay paths
- quoted arguments are preserved consistently
- stdio mode does not print prompts
- stdio mode does not echo typed input
- stdio command replies are written to a single hosted-console reply stream (`stderr` in the current implementation), so informational replies such as `help` and `serverinfo` remain visible on hosted panels
- warnings and errors keep `[WARN]` and `[ERR]` prefixes in console-like transports
- EOF on stdin detaches the stdio reader and does not shut down the server
- `exit` and `quit` remain TCP-session commands only and are not special in stdio mode
- TCP console sessions do not have a server-side idle read timeout, so long-lived `nc` or telnet sessions stay usable until the client or network closes them
- TCP console sessions are command sessions, not live stdout mirrors. Use `logs [lines]` or `tail [lines]` to print a bounded snapshot from the current MelonLoader log when a hosted panel proxies the TCP console instead of forwarding process logs.

### Readiness and Stop Actions

TCP sessions emit the exact line `Type 'help' for commands.` before the extended session hints. Existing host integrations, including Cybrancee's, use this connection banner as a panel readiness signal. Preserve the exact text when matching that integration; it confirms command-session availability, not a completed world load or successful save. Check `=== DEDICATED SERVER READY (orchestrator) ===` and run the [installation checks](configuration/startup-and-deployment.md#verify-a-new-install-or-upgrade) as well.

Configure the panel's Stop/Restart action to send `shutdown` and wait for its final save, `=== Server Shutdown Complete ===`, and process exit. Do not map it to TCP-session `quit`/`exit`, stdin EOF, or an immediate process kill. Failed final saves intentionally leave the server running. See [safe shutdown](configuration/startup-and-deployment.md#stop-and-restart-safely) and [save-failure recovery](troubleshooting/save-load.md#saves-fail-or-shutdown-is-cancelled).

### When to use TCP instead

Use the TCP console when you want an explicitly separate remote admin surface with password protection and prompt-driven sessions. Use stdio when the host already owns the process console.

### Exposure guidance

- `tcpConsolePort` defaults to `4050` and uses TCP.
- The default bind address is `127.0.0.1`, which keeps the console local-only.
- If you change `tcpConsoleBindAddress` to `0.0.0.0` or another non-loopback address, the console becomes reachable on that interface and you must open or forward `tcpConsolePort` separately.
- If you expose the TCP console beyond localhost, require a password and treat it as a trusted admin surface, not a public service.
- The built-in web panel does not support LAN/public bind addresses. If you need a browser UI from another machine, use a hosted panel such as Pterodactyl or build an authenticated web panel on top of the TCP console or another supported control surface.
