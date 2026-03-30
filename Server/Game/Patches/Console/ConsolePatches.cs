#if IL2CPP
using Il2CppFishNet;
using PlayerType = Il2CppScheduleOne.PlayerScripts.Player;
#else
using FishNet;
using PlayerType = ScheduleOne.PlayerScripts.Player;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Console
{
    internal static class ConsolePatches
    {
        public static bool SubmitCommandPrefix(List<string> args)
        {
            if (args == null || args.Count == 0)
            {
                return true;
            }

            if (!(InstanceFinder.IsServer && !InstanceFinder.IsHost))
            {
                return true;
            }

            var local = PlayerType.Local;
            if (local == null)
            {
                return true;
            }

            string cmd = args[0]?.ToLower() ?? string.Empty;
            if (!Shared.Permissions.PermissionManager.CanUseConsole(local))
            {
                return false;
            }

            if (!Shared.Permissions.PermissionManager.CanUseCommand(local, cmd))
            {
                return false;
            }

            return true;
        }
    }
}
