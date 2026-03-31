#if IL2CPP
using Il2CppFishNet;
using ConsoleArgumentList = Il2CppSystem.Collections.Generic.List<string>;
using PlayerType = Il2CppScheduleOne.PlayerScripts.Player;
#else
using FishNet;
using ConsoleArgumentList = System.Collections.Generic.List<string>;
using PlayerType = ScheduleOne.PlayerScripts.Player;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Console
{
    internal static class ConsolePatches
    {
        public static bool SubmitCommandPrefix(ConsoleArgumentList args)
        {
            if (args == null || args.Count == 0)
            {
                return true;
            }

            string commandWord = args[0]?.ToLowerInvariant() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(commandWord))
            {
                return true;
            }

            return CanSubmitCommand(commandWord);
        }

        private static bool CanSubmitCommand(string commandWord)
        {
            if (!(InstanceFinder.IsServer && !InstanceFinder.IsHost))
            {
                return true;
            }

            var local = PlayerType.Local;
            if (local == null)
            {
                return true;
            }

            if (!Shared.Permissions.PermissionManager.CanUseConsole(local))
            {
                return false;
            }

            if (!Shared.Permissions.PermissionManager.CanUseCommand(local, commandWord))
            {
                return false;
            }

            return true;
        }
    }
}
