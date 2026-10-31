#!/bin/bash

# Keep a private console writer alive even when Docker supplies no stdin.
console_dir=$(mktemp -d "${TMPDIR:-/tmp}/s1ds-console.XXXXXX") || exit 1
stdin_pid=""

cleanup() {
    if [ -n "$stdin_pid" ]; then
        kill "$stdin_pid" 2>/dev/null || true
        wait "$stdin_pid" 2>/dev/null || true
    fi
    exec 3>&- 4<&-
    rm -f "$console_dir/commands"
    rmdir "$console_dir"
    if [ -n "${S1DS_XVFB_PID:-}" ]; then
        kill "$S1DS_XVFB_PID" 2>/dev/null || true
    fi
}
trap cleanup EXIT

mkfifo "$console_dir/commands" || exit 1
exec 3<>"$console_dir/commands" || exit 1
exec 4<&0 || exit 1
shutdown_requested=false
wait_interrupted=false

request_shutdown() {
    wait_interrupted=true
    if [ "$shutdown_requested" = false ]; then
        shutdown_requested=true
        echo "Requesting graceful S1DS shutdown; waiting for the final save and game exit."
        printf 'shutdown\n' >&3
    fi
}
trap request_shutdown SIGTERM SIGINT

# Install traps before launch so an early stop is queued for the console reader.
"$@" < "$console_dir/commands" 3>&- 4<&- &
game_pid=$!

# Preserve panel/attached-console input without letting EOF close the command pipe.
cat <&4 >&3 3>&- 4<&- &
stdin_pid=$!

while true; do
    wait_interrupted=false
    wait "$game_pid"
    game_exit_code=$?
    # Bash interrupts wait to run a signal trap; that is not the game's exit.
    if [ "$wait_interrupted" = false ]; then
        break
    fi
done

echo "Game server exited with code: $game_exit_code"
exit "$game_exit_code"
