using System;
using System.Collections.Generic;
using System.Linq;
#if IL2CPP
using Il2CppFishNet;
#else
using FishNet;
#endif
using MelonLoader;
#if IL2CPP
using Il2CppScheduleOne.PlayerScripts;
#else
using ScheduleOne.PlayerScripts;
#endif

namespace DedicatedServerMod.Shared.Permissions
{
    /// <summary>
    /// Manages player permissions (operators, admins, banned players) and command access control.
    /// Provides centralized permission checking for the dedicated server.
    /// </summary>
    /// <remarks>
    /// This class handles all permission-related logic that was previously in ServerConfig.
    /// For Steam ID resolution, see <see cref="PlayerResolver"/>. For configuration, see
    /// <see cref="Configuration.ServerConfig"/>. This class coordinates between both.
    /// </remarks>
    public static class PermissionManager
    {
        #region Events

        /// <summary>
        /// Raised when an operator is added.
        /// </summary>
        public static event Action<string> OperatorAdded;

        /// <summary>
        /// Raised when an operator is removed.
        /// </summary>
        public static event Action<string> OperatorRemoved;

        /// <summary>
        /// Raised when an admin is added.
        /// </summary>
        public static event Action<string> AdminAdded;

        /// <summary>
        /// Raised when an admin is removed.
        /// </summary>
        public static event Action<string> AdminRemoved;

        /// <summary>
        /// Raised when a player is banned.
        /// </summary>
        public static event Action<string> PlayerBanned;

        /// <summary>
        /// Raised when a ban is removed.
        /// </summary>
        public static event Action<string> BanRemoved;

        /// <summary>
        /// Raised when permissions have changed (operator/admin/ban added/removed).
        /// </summary>
        public static event Action PermissionsChanged;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the server configuration instance.
        /// </summary>
        internal static Configuration.ServerConfig Config => Configuration.ServerConfig.Instance;

        /// <summary>
        /// Reference to the logger.
        /// </summary>
        private static MelonLogger.Instance Logger => _logger ?? new MelonLogger.Instance("PermissionManager");

        /// <summary>
        /// Cached logger instance.
        /// </summary>
        private static MelonLogger.Instance _logger;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the permission manager with a logger instance.
        /// </summary>
        /// <param name="logger">The logger to use for permission-related messages</param>
        public static void Initialize(MelonLogger.Instance logger)
        {
            _logger = logger;
            Logger.Msg("PermissionManager initialized");
        }

        #endregion

        #region Operator Management

        /// <summary>
        /// Checks if a Steam ID has operator privileges.
        /// </summary>
        /// <param name="steamId">The Steam ID to check</param>
        /// <returns>True if the Steam ID is an operator</returns>
        public static bool IsOperator(string steamId)
        {
            if (string.IsNullOrEmpty(steamId))
                return false;

            return Config.Operators.Contains(steamId);
        }

        /// <summary>
        /// Checks if a player has operator privileges.
        /// </summary>
        /// <param name="player">The player to check</param>
        /// <returns>True if the player is an operator</returns>
        public static bool IsOperator(Player player)
        {
            if (player?.Owner?.ClientId == null)
                return false;

            string steamId = PlayerResolver.GetSteamId(player);
            return IsOperator(steamId);
        }

        /// <summary>
        /// Adds a Steam ID to the operator list.
        /// </summary>
        /// <param name="steamId">The Steam ID to add</param>
        /// <returns>True if the operator was added (false if already an operator)</returns>
        public static bool AddOperator(string steamId)
        {
            if (string.IsNullOrEmpty(steamId))
                return false;

            bool added = Config.Operators.Add(steamId);
            if (added)
            {
                // Operators also get admin
                Config.Admins.Add(steamId);
                Configuration.ServerConfig.SaveConfig();
                Logger.Msg($"Added operator: {steamId}");
                OperatorAdded?.Invoke(steamId);
                PermissionsChanged?.Invoke();
            }
            return added;
        }

        /// <summary>
        /// Removes a Steam ID from the operator list.
        /// </summary>
        /// <param name="steamId">The Steam ID to remove</param>
        /// <returns>True if the operator was removed</returns>
        public static bool RemoveOperator(string steamId)
        {
            if (string.IsNullOrEmpty(steamId))
                return false;

            bool removed = Config.Operators.Remove(steamId);
            if (removed)
            {
                Configuration.ServerConfig.SaveConfig();
                Logger.Msg($"Removed operator: {steamId}");
                OperatorRemoved?.Invoke(steamId);
                PermissionsChanged?.Invoke();
            }
            return removed;
        }

        /// <summary>
        /// Gets all current operators.
        /// </summary>
        /// <returns>A read-only list of operator Steam IDs</returns>
        public static IReadOnlyList<string> GetAllOperators()
        {
            return Config.Operators.ToList().AsReadOnly();
        }

        #endregion

        #region Admin Management

        /// <summary>
        /// Checks if a Steam ID has admin privileges.
        /// Operators automatically have admin privileges.
        /// </summary>
        /// <param name="steamId">The Steam ID to check</param>
        /// <returns>True if the Steam ID is an admin or operator</returns>
        public static bool IsAdmin(string steamId)
        {
            if (string.IsNullOrEmpty(steamId))
                return false;

            return Config.Admins.Contains(steamId) || IsOperator(steamId);
        }

        /// <summary>
        /// Checks if a player has admin privileges.
        /// </summary>
        /// <param name="player">The player to check</param>
        /// <returns>True if the player is an admin or operator</returns>
        public static bool IsAdmin(Player player)
        {
            if (player?.Owner?.ClientId == null)
                return false;

            string steamId = PlayerResolver.GetSteamId(player);
            return IsAdmin(steamId);
        }

        /// <summary>
        /// Adds a Steam ID to the admin list.
        /// </summary>
        /// <param name="steamId">The Steam ID to add</param>
        /// <returns>True if the admin was added (false if already an admin or is operator)</returns>
        public static bool AddAdmin(string steamId)
        {
            if (string.IsNullOrEmpty(steamId))
                return false;

            // Don't add admin if already operator (operator already implies admin)
            if (Config.Operators.Contains(steamId))
                return Config.Admins.Add(steamId);

            bool added = Config.Admins.Add(steamId);
            if (added)
            {
                Configuration.ServerConfig.SaveConfig();
                Logger.Msg($"Added admin: {steamId}");
                AdminAdded?.Invoke(steamId);
                PermissionsChanged?.Invoke();
            }
            return added;
        }

        /// <summary>
        /// Removes a Steam ID from the admin list.
        /// </summary>
        /// <param name="steamId">The Steam ID to remove</param>
        /// <returns>True if the admin was removed (false if still an operator)</returns>
        public static bool RemoveAdmin(string steamId)
        {
            if (string.IsNullOrEmpty(steamId))
                return false;

            // Never remove admin if user is still operator
            if (Config.Operators.Contains(steamId))
                return false;

            bool removed = Config.Admins.Remove(steamId);
            if (removed)
            {
                Configuration.ServerConfig.SaveConfig();
                Logger.Msg($"Removed admin: {steamId}");
                AdminRemoved?.Invoke(steamId);
                PermissionsChanged?.Invoke();
            }
            return removed;
        }

        /// <summary>
        /// Gets all current admins.
        /// </summary>
        /// <returns>A read-only list of admin Steam IDs</returns>
        public static IReadOnlyList<string> GetAllAdmins()
        {
            return Config.Admins.ToList().AsReadOnly();
        }

        #endregion

        #region Ban Management

        /// <summary>
        /// Checks if a Steam ID is banned.
        /// </summary>
        /// <param name="steamId">The Steam ID to check</param>
        /// <returns>True if the Steam ID is banned</returns>
        public static bool IsBanned(string steamId)
        {
            if (string.IsNullOrEmpty(steamId))
                return false;

            return Config.BannedPlayers.Contains(steamId);
        }

        /// <summary>
        /// Checks if a player is banned.
        /// </summary>
        /// <param name="player">The player to check</param>
        /// <returns>True if the player is banned</returns>
        public static bool IsBanned(Player player)
        {
            if (player?.Owner?.ClientId == null)
                return false;

            string steamId = PlayerResolver.GetSteamId(player);
            return IsBanned(steamId);
        }

        /// <summary>
        /// Bans a Steam ID from the server.
        /// </summary>
        /// <param name="steamId">The Steam ID to ban</param>
        /// <returns>True if the ban was added (false if already banned)</returns>
        public static bool AddBan(string steamId)
        {
            if (string.IsNullOrEmpty(steamId))
                return false;

            bool added = Config.BannedPlayers.Add(steamId);
            if (added)
            {
                // Also remove from operators/admins if present
                Config.Operators.Remove(steamId);
                Config.Admins.Remove(steamId);
                Configuration.ServerConfig.SaveConfig();
                Logger.Msg($"Added ban: {steamId}");
                PlayerBanned?.Invoke(steamId);
                PermissionsChanged?.Invoke();
            }
            return added;
        }

        /// <summary>
        /// Removes a ban from a Steam ID.
        /// </summary>
        /// <param name="steamId">The Steam ID to unban</param>
        /// <returns>True if the ban was removed</returns>
        public static bool RemoveBan(string steamId)
        {
            if (string.IsNullOrEmpty(steamId))
                return false;

            bool removed = Config.BannedPlayers.Remove(steamId);
            if (removed)
            {
                Configuration.ServerConfig.SaveConfig();
                Logger.Msg($"Removed ban: {steamId}");
                BanRemoved?.Invoke(steamId);
                PermissionsChanged?.Invoke();
            }
            return removed;
        }

        /// <summary>
        /// Gets all banned Steam IDs.
        /// </summary>
        /// <returns>A read-only list of banned Steam IDs</returns>
        public static IReadOnlyList<string> GetAllBanned()
        {
            return Config.BannedPlayers.ToList().AsReadOnly();
        }

        #endregion

        #region Console Access Control

        /// <summary>
        /// Checks if a player can use the admin console.
        /// </summary>
        /// <param name="player">The player to check</param>
        /// <returns>True if the player can open the console</returns>
        public static bool CanUseConsole(Player player)
        {
            if (player?.Owner?.ClientId == null)
                return false;

            // Only check on server
            if (!InstanceFinder.IsServer)
                return false;

            // Determine if this is a remote client
            bool isRemoteClient = player.Owner != null && !player.Owner.IsLocalClient;
            if (!isRemoteClient)
                return false;

            string steamId = PlayerResolver.GetSteamId(player);
            if (IsOperator(player) && Config.EnableConsoleForOps)
                return true;

            if (IsAdmin(player) && Config.EnableConsoleForAdmins)
                return true;

            // Check if regular players have any allowed commands
            if (Config.EnableConsoleForPlayers && Config.PlayerAllowedCommands.Count > 0)
                return true;

            Logger.Warning($"CanUseConsole: Denying console access for {player.PlayerName} - not operator/admin or console disabled");
            return false;
        }

        /// <summary>
        /// Checks if a player can use a specific command.
        /// </summary>
        /// <param name="player">The player attempting to use the command</param>
        /// <param name="command">The command name (case-insensitive)</param>
        /// <returns>True if the player can use the command</returns>
        public static bool CanUseCommand(Player player, string command)
        {
            if (player?.Owner?.ClientId == null)
            {
                Logger.Warning("CanUseCommand: Player or Owner or ClientId is null");
                return false;
            }

            if (string.IsNullOrEmpty(command))
            {
                Logger.Warning("CanUseCommand: Command is null or empty");
                return false;
            }

            string normalizedCommand = command.ToLower();

            // 0) Global disables override everything
            if (Config.GlobalDisabledCommands.Contains(normalizedCommand))
                return false;

            // Operators can use all commands
            if (IsOperator(player))
                return true;

            // Admins can use allowed commands but not restricted ones
            if (IsAdmin(player))
            {
                if (Config.RestrictedCommands.Contains(normalizedCommand))
                    return false;

                return Config.AllowedCommands.Contains(normalizedCommand);
            }

            // Regular player: only allow if present in PlayerAllowedCommands
            return Config.PlayerAllowedCommands.Contains(normalizedCommand);
        }

        /// <summary>
        /// Checks if a Steam ID can use a specific command (without player context).
        /// </summary>
        /// <param name="steamId">The Steam ID to check</param>
        /// <param name="command">The command name</param>
        /// <returns>True if the Steam ID can use the command</returns>
        public static bool CanUseCommand(string steamId, string command)
        {
            if (string.IsNullOrEmpty(steamId) || string.IsNullOrEmpty(command))
                return false;

            string normalizedCommand = command.ToLower();

            // 0) Global disables override everything
            if (Config.GlobalDisabledCommands.Contains(normalizedCommand))
                return false;

            // Operators can use all commands
            if (IsOperator(steamId))
                return true;

            // Admins can use allowed commands but not restricted ones
            if (IsAdmin(steamId))
            {
                if (Config.RestrictedCommands.Contains(normalizedCommand))
                    return false;

                return Config.AllowedCommands.Contains(normalizedCommand);
            }

            // Regular player: only allow if present in PlayerAllowedCommands
            return Config.PlayerAllowedCommands.Contains(normalizedCommand);
        }

        #endregion

        #region Player Access Control

        /// <summary>
        /// Checks if a player is allowed to connect based on ban status.
        /// </summary>
        /// <param name="player">The player attempting to connect</param>
        /// <returns>True if the player is allowed to connect</returns>
        public static bool CanPlayerConnect(Player player)
        {
            if (player == null)
                return false;

            string steamId = PlayerResolver.GetSteamId(player);
            return !IsBanned(steamId);
        }

        /// <summary>
        /// Checks if a Steam ID is allowed to connect.
        /// </summary>
        /// <param name="steamId">The Steam ID to check</param>
        /// <returns>True if the Steam ID is allowed to connect</returns>
        public static bool CanPlayerConnect(string steamId)
        {
            return !IsBanned(steamId);
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
            if (!Config.LogAdminCommands)
                return;

            var steamId = PlayerResolver.GetSteamId(player);
            var playerName = player?.PlayerName ?? "Unknown";

            var logMessage = $"Admin Action - Player: {playerName} ({steamId}) | Command: {command}";

            if (!string.IsNullOrEmpty(args))
            {
                logMessage += $" | Args: {args}";
            }

            Logger.Msg(logMessage);
            Utils.DebugLog.WriteToAdminLog(logMessage);
        }

        #endregion

        #region Permission Information

        /// <summary>
        /// Gets a summary of the permission configuration.
        /// </summary>
        /// <returns>A formatted string describing permission settings</returns>
        public static string GetPermissionSummary()
        {
            return $"Permissions: {Config.Operators.Count} operators, " +
                   $"{Config.Admins.Count} admins, " +
                   $"{Config.BannedPlayers.Count} banned, " +
                   $"Console: ops={Config.EnableConsoleForOps}, " +
                   $"admins={Config.EnableConsoleForAdmins}, " +
                   $"players={Config.EnableConsoleForPlayers}";
        }

        #endregion
    }
}
