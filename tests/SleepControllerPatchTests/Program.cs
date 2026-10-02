using DedicatedServerMod.Server.Game.Patches.Common;
using DedicatedServerMod.Server.Game.Patches.Gameplay;
using HarmonyLib;
using ScheduleOne.GameTime;
using ScheduleOne.SpecialCustomers.UI;
using ScheduleOne.UI;
using ScheduleOne.UI.SleepMessage;
using ScheduleOne.PlayerScripts;

var harmony = new Harmony("DedicatedServerMod.Tests.SleepController");
try
{
    harmony.CreateClassProcessor(typeof(SleepControllerPresentationPatches)).Patch();
    harmony.CreateClassProcessor(typeof(SleepControllerStartPatches)).Patch();

    ISleepEvent[] presentationEvents =
    {
        new DailySummary(), new RankUpCanvas(), new RegionUnlockedCanvas(),
        new IncomingSpecialCustomerInfoPopup(), new SleepMessage()
    };
    var controller = new SleepController();
    foreach (var sleepEvent in presentationEvents)
    {
        controller.AddSleepEvent(sleepEvent);
        controller.AddPostSleepEvent(sleepEvent);
    }
    Assert(controller.SleepEvents.Count == 0, "Headless sleep queue must exclude presentation events.");
    Assert(controller.PostSleepEvents.Count == 0, "Headless post-sleep queue must exclude presentation events.");

    var gameplayEvent = new GameplayEvent();
    controller.AddSleepEvent(gameplayEvent);
    controller.AddPostSleepEvent(gameplayEvent);
    Assert(controller.SleepEvents.Single() == gameplayEvent, "Native/custom gameplay events must remain queued.");
    Assert(controller.PostSleepEvents.Single() == gameplayEvent, "Post-sleep gameplay events must remain queued.");

    DedicatedServerPatchCommon.Headless = false;
    foreach (var sleepEvent in presentationEvents)
    {
        controller.AddSleepEvent(sleepEvent);
        controller.AddPostSleepEvent(sleepEvent);
    }
    Assert(controller.SleepEvents.Count == 6 && controller.PostSleepEvents.Count == 6,
        "Interactive sessions must retain both presentation queues.");

    DedicatedServerPatchCommon.EligiblePlayers = 0;
    controller.StartSleep();
    Assert(controller.Starts == 0, "A server with only the ghost host must not start sleep.");
    DedicatedServerPatchCommon.EligiblePlayers = 1;
    var remotePlayer = new Player { IsReadyToSleep = true };
    Player.PlayerList.Add(remotePlayer);
    controller.StartSleep();
    Assert(controller.Starts == 1, "A populated server must retain the native sleep entry point.");
    Assert(!remotePlayer.IsReadyToSleep, "Accepted votes must be consumed before clients finish their summaries.");
    remotePlayer.IsReadyToSleep = true;
    controller.RpcLogic___StartSleep_2166136261();
    Assert(controller.Starts == 2 && !remotePlayer.IsReadyToSleep,
        "Native RPC logic must consume votes when IL2CPP bypasses the public wrapper.");
    controller.IsSleepInProgress = true;
    controller.StartSleep();
    Assert(controller.Starts == 2, "A running sleep cycle must not be broadcast again.");
    controller.IsSleepInProgress = false;
    FishNet.InstanceFinder.IsServer = false;
    DedicatedServerPatchCommon.EligiblePlayers = 0;
    controller.StartSleep();
    Assert(controller.Starts == 3, "Client sleep delivery must not depend on the server player count.");

    Console.WriteLine("PASS|SleepControllerPatchTests|both-queues|gameplay-events|interactive-session|ghost-host|native-rpc-votes");
    SpecialCustomerGroupTests.Run(harmony);
}
finally
{
    harmony.UnpatchAll(harmony.Id);
    Player.PlayerList.Clear();
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

internal sealed class GameplayEvent : ISleepEvent { }
