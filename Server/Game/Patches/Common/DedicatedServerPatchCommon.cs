using DedicatedServerMod.Utils;
#if IL2CPP
using Il2CppFishNet;
using PlayerType = Il2CppScheduleOne.PlayerScripts.Player;
#else
using FishNet;
using PlayerType = ScheduleOne.PlayerScripts.Player;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Common
{
    internal static class DedicatedServerPatchCommon
    {
        internal static bool IsDedicatedHeadlessServer()
        {
            return true;
        }

        internal static bool IsGhostOrLoopbackPlayer(PlayerType player)
        {
            return player != null && GhostHostIdentifier.IsGhostHost(player);
        }

        internal static bool ShouldRunClientVisuals()
        {
            return false;
        }

        internal static bool ShouldRunClientPresentationAudio()
        {
            return false;
        }

        internal static int CountSleepEligiblePlayers()
        {
            var list = PlayerType.PlayerList;
            if (list == null || list.Count == 0)
            {
                return 0;
            }

            int eligible = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var player = list[i];
                if (player == null)
                {
                    continue;
                }

                if (IsGhostOrLoopbackPlayer(player))
                {
                    continue;
                }

                eligible++;
            }

            return eligible;
        }
    }
}
