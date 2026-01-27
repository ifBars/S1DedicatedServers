using System;
using HarmonyLib;
using MelonLoader;
using ScheduleOne.PlayerScripts;
using ScheduleOne.UI;

namespace DedicatedServerMod.Client.Patches
{
    /// <summary>
    /// Harmony patches for the sleep system to support dedicated servers.
    /// Handles sleep readiness checks and sleep canvas behavior on client connections.
    /// </summary>
    /// <remarks>
    /// These patches enable proper sleep cycling when connected to dedicated servers
    /// by filtering out the ghost loopback host player that represents the server.
    /// </remarks>
    internal static class SleepPatches
    {
        /// <summary>
        /// The logger instance for this patch class.
        /// </summary>
        private static MelonLogger.Instance _logger;

        /// <summary>
        /// Whether to ignore the ghost host when checking sleep readiness.
        /// </summary>
        private static bool _ignoreGhostHostForSleep = true;

        /// <summary>
        /// Initialize the sleep patches with a logger instance.
        /// </summary>
        /// <param name="logger">The logger instance to use</param>
        public static void Initialize(MelonLogger.Instance logger)
        {
            _logger = logger;
            _logger.Msg("Sleep patches initialized (using attribute-based patching)");
        }

        /// <summary>
        /// Gets or sets whether to ignore the ghost host when checking sleep readiness.
        /// </summary>
        public static bool IgnoreGhostHostForSleep
        {
            get => _ignoreGhostHostForSleep;
            set
            {
                _ignoreGhostHostForSleep = value;
                _logger?.Msg($"SleepPatches: Ignore ghost host for sleep set to: {_ignoreGhostHostForSleep}");
            }
        }

        #region Player.AreAllPlayersReadyToSleep Patch

        /// <summary>
        /// Harmony prefix patch for Player.AreAllPlayersReadyToSleep.
        /// Filters out ghost loopback host players from the sleep readiness check.
        /// </summary>
        /// <remarks>
        /// This patch intercepts the sleep readiness check to exclude the server's loopback player
        /// on dedicated servers, allowing clients to sleep without waiting for the ghost host.
        /// </remarks>
        /// <param name="__result">The result to set if we handle the check ourselves</param>
        /// <returns>False to skip original method if we handled it, true otherwise</returns>
        [HarmonyPatch(typeof(Player), nameof(Player.AreAllPlayersReadyToSleep))]
        [HarmonyPrefix]
        private static bool AreAllPlayersReadyToSleepPrefix(ref bool __result)
        {
            // Only apply our custom logic if the feature is enabled and we're a client connected to server
            if (!_ignoreGhostHostForSleep || !FishNet.InstanceFinder.IsClient)
            {
                return true; // Let the original method run
            }

            try
            {
                var playerList = Player.PlayerList;
                if (playerList.Count == 0)
                {
                    __result = false;
                    return false; // Skip original method
                }

                int realPlayerCount = 0;
                int readyPlayerCount = 0;

                for (int i = 0; i < playerList.Count; i++)
                {
                    var player = playerList[i];
                    if (player == null) continue;

                    // Skip ghost loopback players
                    if (IsGhostLoopbackPlayer(player))
                    {
                        _logger?.Msg($"Client sleep check: Ignoring ghost host player: {player.PlayerName}");
                        continue;
                    }

                    realPlayerCount++;

                    // Check if this real player is ready to sleep
                    if (player.IsReadyToSleep)
                    {
                        readyPlayerCount++;
                    }
                    else
                    {
                        __result = false;
                        return false; // Skip original method
                    }
                }

                _logger?.Msg($"Client sleep check: All {realPlayerCount} real players are ready to sleep ({readyPlayerCount}/{realPlayerCount})");

                __result = true;
                return false; // Skip original method
            }
            catch (Exception ex)
            {
                _logger?.Error($"Error in client AreAllPlayersReadyToSleep patch: {ex}");
                return true; // Let original method run as fallback
            }
        }

        #endregion

        #region SleepCanvas.SetIsOpen Patch

        /// <summary>
        /// Harmony prefix patch for SleepCanvas.SetIsOpen.
        /// Prevents opening the sleep UI if the server has disabled sleeping.
        /// </summary>
        /// <remarks>
        /// This patch enforces the server's AllowSleeping configuration on clients,
        /// preventing players from attempting to sleep when the server has disabled it.
        /// </remarks>
        /// <param name="__instance">The SleepCanvas instance</param>
        /// <param name="open">Whether opening or closing</param>
        /// <returns>False to skip original if we blocked it, true otherwise</returns>
        [HarmonyPatch(typeof(SleepCanvas), nameof(SleepCanvas.SetIsOpen))]
        [HarmonyPrefix]
        private static bool SleepCanvas_SetIsOpen_Prefix(SleepCanvas __instance, bool open)
        {
            try
            {
                // Allow closing
                if (!open) return true;

                // Only enforce when connected to a remote server (not host)
                if (FishNet.InstanceFinder.IsClient && !FishNet.InstanceFinder.IsHost)
                {
                    // Check server's AllowSleeping setting
                    var allowSleeping = Managers.ServerDataStore.AllowSleeping;
                    if (!allowSleeping)
                    {
                        _logger?.Msg("Server has disabled sleeping; suppressing SleepCanvas open");
                        return false; // Skip original, do not open
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

        #endregion

        #region Ghost Loopback Player Detection

        /// <summary>
        /// Determines if a player is a ghost loopback player from the server.
        /// Ghost loopback players represent the server's local player on dedicated servers.
        /// </summary>
        /// <param name="player">The player to check</param>
        /// <returns>True if this is a ghost loopback player</returns>
        public static bool IsGhostLoopbackPlayer(Player player)
        {
            try
            {
                // Method 1: Check by game object name (set by server)
                if (player.gameObject.name == Utils.Constants.GHOST_HOST_OBJECT_NAME)
                {
                    return true;
                }

                // Method 2: Check by network characteristics
                var networkObject = player.GetComponent<FishNet.Object.NetworkObject>();
                if (networkObject?.Owner != null)
                {
                    // Server loopback player characteristics:
                    // - Owner ClientId is 0 (server)
                    // - IsOwner is false (not owned by this client)
                    bool isServerLoopback = (networkObject.Owner.ClientId == 0 && !networkObject.IsOwner);
                    return isServerLoopback;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger?.Error($"Error checking for ghost loopback player: {ex}");
                return false;
            }
        }

        #endregion
    }
}
