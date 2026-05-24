using DedicatedServerMod.Utils;
using UnityEngine;
#if IL2CPP
using Il2CppFishNet;
using BlackjackGameControllerType = Il2CppScheduleOne.Casino.BlackjackGameController;
using CasinoGameControllerType = Il2CppScheduleOne.Casino.CasinoGameController;
using CasinoGamePlayersType = Il2CppScheduleOne.Casino.CasinoGamePlayers;
using RideTheBusGameControllerType = Il2CppScheduleOne.Casino.RTBGameController;
#else
using FishNet;
using BlackjackGameControllerType = ScheduleOne.Casino.BlackjackGameController;
using CasinoGameControllerType = ScheduleOne.Casino.CasinoGameController;
using CasinoGamePlayersType = ScheduleOne.Casino.CasinoGamePlayers;
using RideTheBusGameControllerType = ScheduleOne.Casino.RTBGameController;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Gameplay
{
    /// <summary>
    /// Re-evaluates casino waiting-state transitions on the authoritative server when
    /// remote clients update table readiness or the seated player list changes.
    /// </summary>
    internal static class CasinoGamePatches
    {
        private const string WaitingForPlayersStageName = "WaitingForPlayers";
        private const string ReadyKey = "Ready";

        /// <summary>
        /// Resolves the runtime casino player-list type for dynamic patching.
        /// </summary>
        public static Type GetCasinoGamePlayersType()
        {
            return typeof(CasinoGamePlayersType);
        }

        /// <summary>
        /// Re-checks waiting casino games after a player's networked bool value changes.
        /// </summary>
        /// <param name="__instance">The casino player-list component receiving the update.</param>
        /// <param name="key">The updated bool key.</param>
        public static void ReceivePlayerBoolPostfix(object __instance, string key)
        {
            if (!InstanceFinder.IsServer || !string.Equals(key, ReadyKey, StringComparison.Ordinal))
            {
                return;
            }

            TryAdvanceWaitingGames(__instance, "ready update");
        }

        /// <summary>
        /// Re-checks waiting casino games after the seated player list changes.
        /// </summary>
        /// <param name="__instance">The casino player-list component receiving the update.</param>
        public static void SetPlayerListPostfix(object __instance)
        {
            if (!InstanceFinder.IsServer)
            {
                return;
            }

            TryAdvanceWaitingGames(__instance, "player list update");
        }

        private static void TryAdvanceWaitingGames(object casinoGamePlayers, string reason)
        {
            if (casinoGamePlayers is not CasinoGamePlayersType typedPlayers)
            {
                return;
            }

            TryAdvanceBlackjack(typedPlayers, reason);
            TryAdvanceRideTheBus(typedPlayers, reason);
        }

        private static void TryAdvanceBlackjack(CasinoGamePlayersType casinoGamePlayers, string reason)
        {
            try
            {
                BlackjackGameControllerType controller = FindControllerForPlayers<BlackjackGameControllerType>(casinoGamePlayers);
                if (controller == null)
                {
                    return;
                }

                if (!string.Equals(controller.CurrentStage.ToString(), WaitingForPlayersStageName, StringComparison.Ordinal))
                {
                    return;
                }

                if (!controller.AreAllPlayersReady())
                {
                    return;
                }

                DebugLog.Debug($"CasinoGamePatches: Advancing Blackjack after {reason} on the dedicated server.");
                controller.TryStartGame();
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"CasinoGamePatches: Failed to advance Blackjack after {reason}: {ex.Message}");
            }
        }

        private static void TryAdvanceRideTheBus(CasinoGamePlayersType casinoGamePlayers, string reason)
        {
            try
            {
                RideTheBusGameControllerType controller = FindControllerForPlayers<RideTheBusGameControllerType>(casinoGamePlayers);
                if (controller == null)
                {
                    return;
                }

                if (!string.Equals(controller.CurrentStage.ToString(), WaitingForPlayersStageName, StringComparison.Ordinal))
                {
                    return;
                }

                if (!controller.AreAllPlayersReady())
                {
                    return;
                }

                DebugLog.Debug($"CasinoGamePatches: Advancing Ride the Bus after {reason} on the dedicated server.");
                controller.TryNextStage();
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"CasinoGamePatches: Failed to advance Ride the Bus after {reason}: {ex.Message}");
            }
        }

        private static TController FindControllerForPlayers<TController>(CasinoGamePlayersType casinoGamePlayers)
            where TController : CasinoGameControllerType
        {
            MonoBehaviour[] controllers = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            for (int i = 0; i < controllers.Length; i++)
            {
                if (controllers[i] is not TController candidate)
                {
                    continue;
                }

                if (candidate.Players == casinoGamePlayers)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
