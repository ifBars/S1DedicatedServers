using System.Reflection;
using DedicatedServerMod.Utils;
using HarmonyLib;
using UnityEngine;
#if IL2CPP
using Il2CppFishNet;
#else
using FishNet;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Gameplay
{
    /// <summary>
    /// Re-evaluates casino waiting-state transitions on the authoritative server when
    /// remote clients update table readiness or the seated player list changes.
    /// </summary>
    internal static class CasinoGamePatches
    {
#if IL2CPP
        private const string CasinoGamePlayersTypeName = "Il2CppScheduleOne.Casino.CasinoGamePlayers";
        private const string BlackjackGameControllerTypeName = "Il2CppScheduleOne.Casino.BlackjackGameController";
        private const string RideTheBusGameControllerTypeName = "Il2CppScheduleOne.Casino.RTBGameController";
#else
        private const string CasinoGamePlayersTypeName = "ScheduleOne.Casino.CasinoGamePlayers";
        private const string BlackjackGameControllerTypeName = "ScheduleOne.Casino.BlackjackGameController";
        private const string RideTheBusGameControllerTypeName = "ScheduleOne.Casino.RTBGameController";
#endif

        private const string WaitingForPlayersStageName = "WaitingForPlayers";
        private const string ReadyKey = "Ready";

        private static readonly Type BlackjackGameControllerType = AccessTools.TypeByName(BlackjackGameControllerTypeName);
        private static readonly Type RideTheBusGameControllerType = AccessTools.TypeByName(RideTheBusGameControllerTypeName);

        private static readonly FieldInfo BlackjackPlayersField = AccessTools.Field(BlackjackGameControllerType, "Players");
        private static readonly FieldInfo RideTheBusPlayersField = AccessTools.Field(RideTheBusGameControllerType, "Players");

        private static readonly PropertyInfo BlackjackCurrentStageProperty = AccessTools.Property(BlackjackGameControllerType, "CurrentStage");
        private static readonly PropertyInfo RideTheBusCurrentStageProperty = AccessTools.Property(RideTheBusGameControllerType, "CurrentStage");

        private static readonly MethodInfo BlackjackAreAllPlayersReadyMethod = AccessTools.Method(BlackjackGameControllerType, "AreAllPlayersReady");
        private static readonly MethodInfo RideTheBusAreAllPlayersReadyMethod = AccessTools.Method(RideTheBusGameControllerType, "AreAllPlayersReady");

        private static readonly MethodInfo BlackjackTryStartGameMethod = AccessTools.Method(BlackjackGameControllerType, "TryStartGame");
        private static readonly MethodInfo RideTheBusTryNextStageMethod = AccessTools.Method(RideTheBusGameControllerType, "TryNextStage");

        /// <summary>
        /// Resolves the runtime casino player-list type for dynamic patching.
        /// </summary>
        public static Type GetCasinoGamePlayersType()
        {
            return AccessTools.TypeByName(CasinoGamePlayersTypeName);
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
            if (casinoGamePlayers == null)
            {
                return;
            }

            TryAdvanceWaitingController(
                casinoGamePlayers,
                BlackjackGameControllerType,
                BlackjackPlayersField,
                BlackjackCurrentStageProperty,
                BlackjackAreAllPlayersReadyMethod,
                BlackjackTryStartGameMethod,
                "Blackjack",
                reason);

            TryAdvanceWaitingController(
                casinoGamePlayers,
                RideTheBusGameControllerType,
                RideTheBusPlayersField,
                RideTheBusCurrentStageProperty,
                RideTheBusAreAllPlayersReadyMethod,
                RideTheBusTryNextStageMethod,
                "Ride the Bus",
                reason);
        }

        private static void TryAdvanceWaitingController(
            object casinoGamePlayers,
            Type controllerType,
            FieldInfo playersField,
            PropertyInfo currentStageProperty,
            MethodInfo areAllPlayersReadyMethod,
            MethodInfo advanceMethod,
            string gameName,
            string reason)
        {
            if (controllerType == null ||
                playersField == null ||
                currentStageProperty == null ||
                areAllPlayersReadyMethod == null ||
                advanceMethod == null)
            {
                return;
            }

            try
            {
                object controller = FindControllerForPlayers(controllerType, playersField, casinoGamePlayers);
                if (controller == null)
                {
                    return;
                }

                object currentStage = currentStageProperty.GetValue(controller, null);
                if (!string.Equals(currentStage?.ToString(), WaitingForPlayersStageName, StringComparison.Ordinal))
                {
                    return;
                }

                if (areAllPlayersReadyMethod.Invoke(controller, null) is not bool allPlayersReady || !allPlayersReady)
                {
                    return;
                }

                DebugLog.Debug($"CasinoGamePatches: Advancing {gameName} after {reason} on the dedicated server.");
                advanceMethod.Invoke(controller, null);
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"CasinoGamePatches: Failed to advance {gameName} after {reason}: {ex.Message}");
            }
        }

        private static object FindControllerForPlayers(Type controllerType, FieldInfo playersField, object casinoGamePlayers)
        {
            MonoBehaviour[] controllers = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            for (int i = 0; i < controllers.Length; i++)
            {
                object candidate = controllers[i];
                if (candidate == null || !controllerType.IsInstanceOfType(candidate))
                {
                    continue;
                }

                object boundPlayers = playersField.GetValue(candidate);
                if (Equals(boundPlayers, casinoGamePlayers))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
