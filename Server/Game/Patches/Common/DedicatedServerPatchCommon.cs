using MelonLoader;
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
        internal static readonly MelonLogger.Instance Logger = new MelonLogger.Instance("DedicatedServerPatches");

        internal static bool ShouldRunClientVisuals()
        {
            return !InstanceFinder.IsServer && !UnityEngine.Application.isBatchMode;
        }

        internal static bool ShouldRunClientPresentationAudio()
        {
            return !InstanceFinder.IsServer && !UnityEngine.Application.isBatchMode;
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

                if (GhostHostIdentifier.IsGhostHost(player))
                {
                    continue;
                }

                eligible++;
            }

            return eligible;
        }
    }
}
