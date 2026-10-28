# Game update investigations and publication checks

The archive watcher provides early warnings. Automated checks establish that our
regression harnesses and builds pass; neither establishes live-game compatibility.

## Archive watcher

`Investigate Game Archive Updates` runs daily at 09:23 UTC on the default branch.
It can also be dispatched manually, with dry-run enabled by default. Scheduling
starts only after the workflow reaches the default branch. No upstream webhook,
additional secret, or game installation is required.

`.github/archive-watch.json` tracks `alternate` and `alternate-beta` independently.
The initial baselines are the archive heads inspected when introducing the watcher,
not claims that those game builds have passed compatibility testing. The watchlist
starts with console, input, pause-menu and player declarations.

Every later archive version commit produces an investigation, even if no watched
declarations changed. Other commits trigger only when watched paths change. The
watcher compares each revision to its parent, including deleted and renamed paths,
and includes links and affected behavior in the issue. It does not execute archive
code or automatically apply suggestions from upstream content.

An embedded branch/revision marker deduplicates against all open and closed issues.
Closing an investigation leaves it closed; reruns recover partial publication
without duplicating previously created issues. Do not remove these markers. Runs
are serialized. API, fetch, or ancestry errors fail the workflow rather than
silently advancing a cursor. Branch history rewrites require maintainer review.

History is replayed from the checked-in baseline, so missed schedules do not lose
updates and no disposable Actions cache is used as authoritative state. Baselines
may be advanced in a reviewed PR after all intervening revisions are accounted for.
Do not advance them to suppress an uninvestigated update.

## Automated publication gate

PR builds and the release workflow call the same `Regression Tests` workflow from
the caller's commit. It runs watcher tests and these game-independent harnesses:

- Persistence, player join bootstrap and console input
- Sleep patch registration/readiness and dedicated runtime policy
- Headless message visibility and loopback public-server status queries

The suite is maintained in `scripts/ci/Run-RegressionTests.ps1`. It stops on a
failed harness. Release creation depends on its successful completion, then builds
all four runtime/side configurations before publishing packages. Docker publication
depends on release creation. This also applies to manual release dispatches; it
does not rely on a green check from an older commit or another branch.

The Build and Test summary fails if either regression or build jobs fail or skip.
Actions logs retain the test output. Repository administrators may additionally
require this summary through branch protection; this change does not alter rulesets.

Run locally from the repository root:

```powershell
python -m unittest discover -s tests/ArchiveWatcherTests -v
./scripts/ci/Run-RegressionTests.ps1
$env:GH_REPO = 'ifBars/S1DedicatedServers'
$env:WATCH_DRY_RUN = 'true'
python scripts/ci/archive_watch.py
```

The dry run reads GitHub with the authenticated `gh` CLI and prints proposed issues
without posting them. The scheduled workflow uses its scoped `GITHUB_TOKEN`.

## Investigation evidence

Before closing an investigation, record the game version, private assembly
revisions, proposed mod commit, automated run links, and disposition of relevant
changes. Refresh assembly repositories deliberately: passing builds against older
assemblies do not establish support for the newly archived game.

Live game testing is not available in CI and is not a publication prerequisite.
Record it as not performed unless separately verified. In particular, stub-based
console tests cannot detect native IL2CPP getter bypass or Unity focus callbacks.
Local console checks should use the actual input action, not directly invoke the
getter or `SetIsOpen`, and cover open/close/reopen, denied and revoked permissions,
pause and reconnect on both runtimes. An open investigation is a tracking item,
not an automatic release veto or a confirmed defect.
