# Player join bootstrap regression checks

Run `dotnet run --project tests/PlayerJoinBootstrapTests/PlayerJoinBootstrapTests.csproj` from the repository root.

This test host compiles the production `PlayerJoinBootstrapService` against managed game, networking, and coroutine stubs. It verifies that saved dealer recommendations replay once per joining session after world and player readiness, independently of quest readiness. It also covers eligibility filtering, inactive interiors, per-dealer failure isolation, disconnect cancellation, loopback exclusion, and timeout termination.

These checks require no game assemblies and run in CI. They do not prove RPC delivery or dealer dialogue in Unity. Validate those separately with a dedicated server and a late-joining client on each supported runtime: load a save with an unrecruited, recommended dealer and an unlocked connection, join, verify recruitment eligibility, then disconnect and reconnect. An unrecommended dealer must remain unrecommended.
