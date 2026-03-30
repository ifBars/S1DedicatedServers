## Host Console

DedicatedServerMod supports two host console transports:

- TCP console for direct socket-based remote administration
- stdio host console for platforms that inject commands through process stdin and capture logs from stdout/stderr

The optional localhost web panel is a separate surface intended for local operators. Hosted panels should usually keep it disabled and rely on stdio instead.

### Recommended mode

Use:

```json
{
  "stdioConsoleMode": "Auto"
}
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

### Behavior

- `stdin` lines are parsed with the same command grammar used by TCP console and admin relay paths
- quoted arguments are preserved consistently
- stdio mode does not print prompts
- stdio mode does not echo typed input
- warnings and errors are written to `stderr`
- EOF on stdin detaches the stdio reader and does not shut down the server
- `exit` and `quit` remain TCP-session commands only and are not special in stdio mode

### When to use TCP instead

Use the TCP console when you want an explicitly separate remote admin surface with password protection and prompt-driven sessions. Use stdio when the host already owns the process console.
