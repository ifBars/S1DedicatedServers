using System;
using System.IO;
using DedicatedServerMod.Utils;
using MelonLoader;
using MelonLoader.Utils;
using ScheduleOne.PlayerScripts;
using Steamworks;

namespace DedicatedServerMod.Shared.Permissions
{
    /// <summary>
    /// Provides utilities for resolving Steam IDs from players and vice versa.
    /// Centralizes all player identification logic for the dedicated server.
    /// </summary>
    /// <remarks>
    /// This class handles the complex logic of determining a player's Steam ID
    /// from various sources, with fallback strategies for different server configurations.
    /// </remarks>
    public static class PlayerResolver
    {
        #region Private Fields

        /// <summary>
        /// Reference to the logger.
        /// </summary>
        private static MelonLogger.Instance Logger => _logger ?? new MelonLogger.Instance("PlayerResolver");

        /// <summary>
        /// Cached logger instance.
        /// </summary>
        private static MelonLogger.Instance _logger;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the player resolver with a logger instance.
        /// </summary>
        /// <param name="logger">The logger to use for player resolution messages</param>
        public static void Initialize(MelonLogger.Instance logger)
        {
            _logger = logger;
            Logger.Msg("PlayerResolver initialized");
        }

        #endregion

        #region Steam ID Resolution

        /// <summary>
        /// Gets the Steam ID for a player.
        /// Uses multiple strategies in order of preference.
        /// </summary>
        /// <param name="player">The player to get the Steam ID for</param>
        /// <returns>The Steam ID string, or null if not resolvable</returns>
        public static string GetSteamId(Player player)
        {
            try
            {
                if (player == null || player.Owner == null)
                    return null;

                // Strategy 1: PlayerCode is synced from client and holds SteamID
                if (!string.IsNullOrEmpty(player.PlayerCode))
                    return player.PlayerCode;

                // Strategy 2: Server-side player manager lookup (if available)
#if SERVER
                try
                {
                    var serverCore = Type.GetType("Server.Core.ServerBootstrap, DedicatedServerMod");
                    if (serverCore != null)
                    {
                        var playersProperty = serverCore.GetProperty("Players", 
                            System.Reflection.BindingFlags.Public | 
                            System.Reflection.BindingFlags.Static);
                        if (playersProperty != null)
                        {
                            var players = playersProperty.GetValue(null);
                            if (players != null)
                            {
                                var getPlayerMethod = players.GetType().GetMethod("GetPlayer");
                                if (getPlayerMethod != null)
                                {
                                    var connectedInfo = getPlayerMethod.Invoke(players, new object[] { player.Owner });
                                    if (connectedInfo != null)
                                    {
                                        var steamIdProperty = connectedInfo.GetType().GetProperty("SteamId");
                                        if (steamIdProperty != null)
                                        {
                                            var steamId = steamIdProperty.GetValue(connectedInfo) as string;
                                            if (!string.IsNullOrEmpty(steamId))
                                                return steamId;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"PlayerResolver: ServerBootstrap lookup failed: {ex.Message}");
                }
#endif

                // Strategy 3: Local Steam API (if running with Steam and local client)
                if (SteamAPI.IsSteamRunning() && player.Owner.IsLocalClient)
                {
                    try
                    {
                        var localSteamId = SteamUser.GetSteamID().m_SteamID.ToString();
                        return localSteamId;
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"PlayerResolver: Failed local SteamID read: {ex.Message}");
                    }
                }

                // Strategy 4: Fallback to FishNet ClientId (NOT a real SteamID)
                string fallbackId = player.Owner.ClientId.ToString();
                Logger.Warning($"PlayerResolver: Falling back to ClientId (not a SteamID): {fallbackId}");
                return fallbackId;
            }
            catch (Exception ex)
            {
                Logger.Error($"PlayerResolver.GetSteamId error: {ex}");
                return null;
            }
        }

        #endregion

        #region Player Lookup

        /// <summary>
        /// Finds a player by their Steam ID.
        /// </summary>
        /// <param name="steamId">The Steam ID to search for</param>
        /// <returns>The player with the matching Steam ID, or null if not found</returns>
        public static Player GetPlayerBySteamId(string steamId)
        {
            if (string.IsNullOrEmpty(steamId))
                return null;

            foreach (var player in Player.PlayerList)
            {
                string playerSteamId = GetSteamId(player);
                if (playerSteamId == steamId)
                    return player;
            }

            Logger.Warning($"GetPlayerBySteamId: No player found for steamId {steamId}");
            return null;
        }

        /// <summary>
        /// Finds a player by their connection.
        /// </summary>
        /// <param name="connection">The network connection</param>
        /// <returns>The player with the matching connection, or null if not found</returns>
        public static Player GetPlayerByConnection(FishNet.Connection.NetworkConnection connection)
        {
            if (connection == null)
            {
                Logger.Warning("GetPlayerByConnection: Connection is null");
                return null;
            }

            foreach (var player in Player.PlayerList)
            {
                if (player?.Owner == connection)
                {
                    return player;
                }
            }

            Logger.Warning($"GetPlayerByConnection: No player found for connection {connection.ClientId}");
            return null;
        }

        /// <summary>
        /// Finds a player by their name (case-insensitive, partial match supported).
        /// </summary>
        /// <param name="name">The name to search for</param>
        /// <returns>The first matching player, or null if not found</returns>
        public static Player GetPlayerByName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            foreach (var player in Player.PlayerList)
            {
                if (player?.PlayerName != null && 
                    player.PlayerName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return player;
                }
            }

            Logger.Warning($"GetPlayerByName: No player found for name {name}");
            return null;
        }

        #endregion

        #region Local Player Operations

        /// <summary>
        /// Gets the Steam ID of the local player (if running with Steam).
        /// </summary>
        /// <returns>The local player's Steam ID, or null if Steam not initialized</returns>
        public static string GetLocalPlayerSteamId()
        {
            try
            {
                if (SteamManager.Initialized)
                {
                    CSteamID steamId = SteamUser.GetSteamID();
                    return steamId.m_SteamID.ToString();
                }
                else
                {
                    Logger.Warning("GetLocalPlayerSteamId: Steam not initialized");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"GetLocalPlayerSteamId error: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Adds the local player as an operator (for testing/admin purposes).
        /// </summary>
        /// <returns>True if the operator was added successfully</returns>
        public static bool AddLocalPlayerAsOperator()
        {
            try
            {
                string steamId = GetLocalPlayerSteamId();
                if (string.IsNullOrEmpty(steamId))
                {
                    Logger.Warning("AddLocalPlayerAsOperator: Could not get local Steam ID");
                    return false;
                }

                Logger.Msg($"AddLocalPlayerAsOperator: Adding local player {steamId} as operator");
                return PermissionManager.AddOperator(steamId);
            }
            catch (Exception ex)
            {
                Logger.Error($"AddLocalPlayerAsOperator error: {ex}");
                return false;
            }
        }

        #endregion

        #region Admin Action Logging

        /// <summary>
        /// Logs an admin action to the console and admin log file.
        /// </summary>
        /// <param name="player">The player who performed the action</param>
        /// <param name="command">The command executed</param>
        /// <param name="args">The command arguments</param>
        public static void LogAdminAction(Player player, string command, string args = "")
        {
            if (!PermissionManager.Config?.LogAdminCommands ?? false)
                return;

            string steamId = GetSteamId(player);
            string playerName = player?.PlayerName ?? "Unknown";

            var logMessage = $"Admin Action - Player: {playerName} ({steamId}) | Command: {command}";

            if (!string.IsNullOrEmpty(args))
            {
                logMessage += $" | Args: {args}";
            }

            Logger.Msg(logMessage);
            WriteToAdminLog(logMessage);
        }

        /// <summary>
        /// Writes a message to the admin actions log file.
        /// </summary>
        /// <param name="message">The message to write</param>
        private static void WriteToAdminLog(string message)
        {
            try
            {
                string logPath = Path.Combine(MelonEnvironment.UserDataDirectory, Utils.Constants.AdminLOGFileName);
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n";
                File.AppendAllText(logPath, logEntry);
                Logger.Msg($"LogAdminAction: Wrote to admin log file: {logPath}");
            }
            catch (Exception ex)
            {
                Logger.Error($"LogAdminAction: Failed to write to admin log: {ex}");
            }
        }

        #endregion

        #region Validation

        /// <summary>
        /// Validates a Steam ID format.
        /// </summary>
        /// <param name="steamId">The Steam ID to validate</param>
        /// <returns>True if the Steam ID appears valid</returns>
        public static bool IsValidSteamId(string steamId)
        {
            if (string.IsNullOrEmpty(steamId))
                return false;

            // Steam IDs are typically 17 digits for 64-bit
            // But we also accept fallback ClientIds which are just numbers
            return steamId.Length >= 8 && steamId.Length <= 20 &&
                   ulong.TryParse(steamId, out _);
        }

        #endregion
    }
}
