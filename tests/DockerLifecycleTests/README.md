# Docker lifecycle regression checks

Run on Linux, including WSL:

```bash
bash -n Docker/run.sh
bash -n Docker/server-lifecycle.sh
python3 -m unittest discover -s tests/DockerLifecycleTests -v
```

The tests run the production Bash supervisor with a fake stdin-driven game process. They verify that SIGTERM/SIGINT request one shutdown, the wrapper waits for a delayed final save and process exit, early stops are queued, external stdin EOF does not close the command pipe, a failed save keeps the process alive for a manual retry, Xvfb is retained until exit, and natural failures preserve the child's exit code. No game files or Docker daemon are needed.

These are process-coordination checks. They do not prove Wine's console delivery or game persistence. Live acceptance requires building the image from the same source, loading a disposable world on each runtime, stopping through Docker, confirming `Save completed: server_shutdown` before process exit, and checking progression and object identifiers after restart. A forced native write failure must cancel shutdown and leave the game running until repaired/retried or externally killed. Docker's stop timeout must allow that work to finish.
