using System;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Shared.Permissions;
using MelonLoader;
#if IL2CPP
using Il2CppFishNet;
using Il2CppScheduleOne.DevUtilities;
using TimeManagerType = Il2CppScheduleOne.GameTime.TimeManager;
#else
using FishNet;
using ScheduleOne.DevUtilities;
using TimeManagerType = ScheduleOne.GameTime.TimeManager;
#endif

namespace DedicatedServerMod.Server.Commands.Server
{
    /// <summary>
    /// Command to set the authoritative server time of day.
    /// </summary>
    public class SetTimeCommand : BaseServerCommand
    {
        public SetTimeCommand(MelonLogger.Instance logger, PlayerManager playerMgr)
            : base(logger, playerMgr)
        {
        }

        public override string CommandWord => "settime";

        public override string Description => "Sets the authoritative server time of day";

        public override string Usage => "settime 1800";

        public override string RequiredPermissionNode => PermissionNode.CreateConsoleCommandNode(CommandWord);

        public override void Execute(CommandContext context)
        {
            if (context.Arguments.Count == 0 || !TimeManagerType.IsValid24HourTime(context.Arguments[0]))
            {
                context.ReplyError("Usage: settime HHMM");
                return;
            }

            TimeManagerType timeManager = NetworkSingleton<TimeManagerType>.Instance;
            if (timeManager == null)
            {
                context.ReplyError("Time manager is not available.");
                return;
            }

            if (timeManager.IsSleepInProgress)
            {
                context.ReplyError("Can't set time while sleep is in progress.");
                return;
            }

            int targetTime = int.Parse(context.Arguments[0]);
            timeManager.SetTimeAndSync(targetTime);
            context.Reply($"Server time set to {TimeManagerType.Get12HourTime(targetTime, true)} ({targetTime:D4}).");
            Logger.Msg($"Server time set to {targetTime:D4} by {context.Executor?.DisplayName ?? "Console"}");
        }
    }
}
