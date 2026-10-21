using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Server.Player.Runtime;
using DedicatedServerMod.Utils;
using MelonLoader;
using ScheduleOne.DevUtilities;
using ScheduleOne.Economy;
using ScheduleOne.Persistence;
using UnityEngine;

Run("waits-for-player-and-world", (service, player, registry, dealer) =>
{
    service.StartInitialJoinBootstrap(player);
    MelonCoroutines.Tick();
    Assert(dealer.Replays == 0, "Must wait for player data and the world.");
    player.PlayerInstance.PlayerLoaded = true;
    MelonCoroutines.Tick();
    Assert(dealer.Replays == 0, "Player data alone must not allow replay.");
    Singleton<LoadManager>.Instance.IsGameLoaded = true;
    Singleton<LoadManager>.Instance.IsLoading = true;
    MelonCoroutines.Tick();
    Assert(dealer.Replays == 0, "Must wait until world loading finishes.");
    Singleton<LoadManager>.Instance.IsLoading = false;
    Drain();
    Assert(dealer.Replays == 1, "Replay the saved recommendation once when ready, independently of quest readiness.");
    Assert(UnityEngine.Object.IncludedInactive, "Dealers in inactive interiors must be included.");
});

Run("filters-and-isolates-dealers", (service, player, registry, dealer) =>
{
    var unrecommended = new Dealer();
    var recruited = new Dealer { HasBeenRecommended = true, IsRecruited = true };
    var unspawned = new Dealer { HasBeenRecommended = true, IsSpawned = false };
    var broken = new Dealer { HasBeenRecommended = true, ThrowOnReplay = true };
    UnityEngine.Object.Instances = new object[] { null, broken, dealer, unrecommended, recruited, unspawned };
    Ready(player);
    service.StartInitialJoinBootstrap(player);
    Drain();
    Assert(dealer.Replays == 1, "One failed dealer must not stop the remaining replays.");
    Assert(unrecommended.Replays == 0 && recruited.Replays == 0 && unspawned.Replays == 0,
        "Only recommended, unrecruited, spawned dealers should be replayed.");
    Assert(!unrecommended.HasBeenRecommended && recruited.IsRecruited, "Replay must not change authoritative eligibility.");
    Assert(DebugLog.Warnings.Any(warning => warning.Contains("Synthetic dealer RPC failure")), "Report RPC failures.");
});

Run("world-ready-before-player", (service, player, registry, dealer) =>
{
    Singleton<LoadManager>.Instance.IsGameLoaded = true;
    player.PlayerInstance = null;
    service.StartInitialJoinBootstrap(player);
    MelonCoroutines.Tick();
    player.PlayerInstance = new PlayerStub();
    MelonCoroutines.Tick();
    Assert(dealer.Replays == 0, "A loaded world must not bypass the joining player's load state.");
    player.PlayerInstance.PlayerLoaded = true;
    Drain();
    Assert(dealer.Replays == 1, "Replay when the delayed player finishes loading.");
});

Run("cancels-disconnected-session", (service, player, registry, dealer) =>
{
    service.StartInitialJoinBootstrap(player);
    MelonCoroutines.Tick();
    registry.Active = false;
    Ready(player);
    Drain();
    Assert(dealer.Replays == 0, "A stale/disconnected player must not trigger replay.");
});

Run("skips-loopback-and-invalid-connections", (service, player, registry, dealer) =>
{
    Ready(player);
    player.IsLoopbackConnection = true;
    service.StartInitialJoinBootstrap(player);
    Drain();
    player.Connection = null;
    service.StartInitialJoinBootstrap(player);
    service.StartInitialJoinBootstrap(null);
    Drain();
    Assert(dealer.Replays == 0, "The loopback host and missing connections must not replay recommendations.");
});

Run("times-out-without-late-replay", (service, player, registry, dealer) =>
{
    service.StartInitialJoinBootstrap(player);
    Drain();
    Ready(player);
    MelonCoroutines.Tick();
    Assert(dealer.Replays == 0, "A timed-out coroutine must remain stopped.");
    Assert(DebugLog.Warnings.Any(warning => warning.Contains("Timed out replaying dealer recommendations")), "Log the readiness timeout.");
});

Run("replays-on-each-new-join", (service, player, registry, dealer) =>
{
    Ready(player);
    service.StartInitialJoinBootstrap(player);
    Drain();
    service.StartInitialJoinBootstrap(new ConnectedPlayerInfo { PlayerInstance = new PlayerStub { PlayerLoaded = true } });
    Drain();
    Assert(dealer.Replays == 2, "Each joining session needs its own replay.");
});

static void Run(string name, Action<PlayerJoinBootstrapService, ConnectedPlayerInfo, PlayerSessionRegistry, Dealer> test)
{
    try
    {
        Time.realtimeSinceStartup = 0f;
        Singleton<LoadManager>.Instance = new LoadManager();
        MelonCoroutines.Pending.Clear();
        DebugLog.Warnings.Clear();
        UnityEngine.Object.IncludedInactive = false;
        var dealer = new Dealer { HasBeenRecommended = true };
        UnityEngine.Object.Instances = new object[] { dealer };
        var registry = new PlayerSessionRegistry();
        test(new PlayerJoinBootstrapService(registry), new ConnectedPlayerInfo(), registry, dealer);
        Console.WriteLine($"PASS|{name}");
    }
    finally
    {
        MelonCoroutines.Pending.Clear();
        UnityEngine.Object.Instances = Array.Empty<object>();
        Singleton<LoadManager>.Instance = null;
        DebugLog.Warnings.Clear();
    }
}

static void Ready(ConnectedPlayerInfo player)
{
    player.PlayerInstance.PlayerLoaded = true;
    Singleton<LoadManager>.Instance.IsGameLoaded = true;
}

static void Drain()
{
    for (int tick = 0; tick < 80 && MelonCoroutines.Pending.Count > 0; tick++) MelonCoroutines.Tick();
    Assert(MelonCoroutines.Pending.Count == 0, "Join bootstrap coroutines must finish within their timeout.");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
