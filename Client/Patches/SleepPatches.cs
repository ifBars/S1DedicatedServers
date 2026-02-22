using System;
using HarmonyLib;
using MelonLoader;
using ScheduleOne.PlayerScripts;
using ScheduleOne.UI;
using DedicatedServerMod.Utils;

namespace DedicatedServerMod.Client.Patches
{
    /// <summary>
    /// Harmony patches for the sleep system to support dedicated servers.
    /// Filters out the ghost loopback host player from sleep readiness checks
    /// and enforces the server's AllowSleeping configuration.
    /// Uses <see cref="GhostHostIdentifier"/> for centralized ghost detection.
    /// </summary>
    internal static class SleepPatches
    {
        private static MelonLogger.Instance _logger;
        private static bool _ignoreGhostHostForSleep = true;

        public static void Initialize(MelonLogger.Instance logger)
        {
            _logger = logger;
            _logger.Msg("Sleep patches initialized");
        }

        public static bool IgnoreGhostHostForSleep
        {
            get => _ignoreGhostHostForSleep;
            set
            {
                _ignoreGhostHostForSleep = value;
                _logger?.Msg($"SleepPatches: Ignore ghost host set to: {_ignoreGhostHostForSleep}");
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.AreAllPlayersReadyToSleep))]
        [HarmonyPrefix]
        private static bool AreAllPlayersReadyToSleepPrefix(ref bool __result)
        {
            if (!_ignoreGhostHostForSleep || !FishNet.InstanceFinder.IsClient)
                return true;

            try
            {
                var playerList = Player.PlayerList;
                if (playerList.Count == 0)
                {
                    __result = false;
                    return false;
                }

                for (int i = 0; i < playerList.Count; i++)
                {
                    var player = playerList[i];
                    if (player == null) continue;

                    if (GhostHostIdentifier.IsGhostHost(player))
                        continue;

                    if (!player.IsReadyToSleep)
                    {
                        __result = false;
                        return false;
                    }
                }

                __result = true;
                return false;
            }
            catch (Exception ex)
            {
                _logger?.Error($"Error in AreAllPlayersReadyToSleep patch: {ex}");
                return true;
            }
        }

        [HarmonyPatch(typeof(SleepCanvas), nameof(SleepCanvas.SetIsOpen))]
        [HarmonyPrefix]
        private static bool SleepCanvas_SetIsOpen_Prefix(SleepCanvas __instance, bool open)
        {
            try
            {
                if (!open) return true;

                if (FishNet.InstanceFinder.IsClient && !FishNet.InstanceFinder.IsHost)
                {
                    if (!Managers.ServerDataStore.AllowSleeping)
                    {
                        _logger?.Msg("Server has disabled sleeping; suppressing SleepCanvas open");
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger?.Warning($"SleepCanvas_SetIsOpen_Prefix error: {ex.Message}");
                return true;
            }
        }
    }
}
