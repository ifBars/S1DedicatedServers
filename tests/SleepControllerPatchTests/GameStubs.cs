using System.Runtime.CompilerServices;

namespace FishNet
{
    internal static class InstanceFinder
    {
        internal static bool IsServer { get; set; } = true;
    }
}

namespace DedicatedServerMod.Server.Game.Patches.Common
{
    internal static class DedicatedServerPatchCommon
    {
        internal static bool Headless { get; set; } = true;
        internal static int EligiblePlayers { get; set; } = 1;
        internal static bool IsDedicatedHeadlessServer() => Headless;
        internal static int CountSleepEligiblePlayers() => EligiblePlayers;
    }
}

namespace ScheduleOne.GameTime
{
    public interface ISleepEvent { }

    public sealed class SleepController
    {
        public List<ISleepEvent> SleepEvents { get; } = new();
        public List<ISleepEvent> PostSleepEvents { get; } = new();
        public int Starts { get; private set; }
        public bool IsSleepInProgress { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void StartSleep() => RpcLogic___StartSleep_2166136261();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void RpcLogic___StartSleep_2166136261() => Starts++;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void AddSleepEvent(ISleepEvent sleepEvent) => SleepEvents.Add(sleepEvent);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void AddPostSleepEvent(ISleepEvent sleepEvent) => PostSleepEvents.Add(sleepEvent);
    }
}

namespace ScheduleOne.PlayerScripts
{
    public sealed class Player
    {
        public static List<Player> PlayerList { get; } = new();
        public bool IsReadyToSleep { get; set; }
    }
}

namespace ScheduleOne.UI
{
    public class DailySummary : GameTime.ISleepEvent { }
    public class RankUpCanvas : GameTime.ISleepEvent { }
    public class RegionUnlockedCanvas : GameTime.ISleepEvent { }
}

namespace ScheduleOne.UI.SleepMessage
{
    public class SleepMessage : GameTime.ISleepEvent { }
}

namespace ScheduleOne.SpecialCustomers.UI
{
    public class IncomingSpecialCustomerInfoPopup : GameTime.ISleepEvent { }
}
