using System;
using System.Collections.Generic;
using System.Linq;
#if SERVER
using DedicatedServerMod.Server.Core;
#endif
using DedicatedServerMod.Shared.Configuration;
using MelonLoader;
#if IL2CPP
using Il2CppScheduleOne.PlayerScripts;
#else
using ScheduleOne.PlayerScripts;
#endif

namespace DedicatedServerMod.Shared.Permissions
{
    /// <summary>
    /// Compatibility facade over the authoritative node-based permission service.
    /// </summary>
    /// <remarks>
    /// This static facade preserves the existing permission call sites during the transition
    /// release while delegating runtime authority to <c>permissions.toml</c> on the server.
    /// </remarks>
    public static class PermissionManager
    {
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
        /// Raised when permissions change.
        /// </summary>
        public static event Action PermissionsChanged;

        private static MelonLogger.Instance _logger;

        /// <summary>
        /// Gets the server configuration instance.
        /// </summary>
        internal static ServerConfig Config => ServerConfig.Instance;

        private static MelonLogger.Instance Logger => _logger ?? new MelonLogger.Instance("PermissionManager");

        /// <summary>
        /// Initializes the compatibility facade.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        public static void Initialize(MelonLogger.Instance logger)
        {
            _logger = logger;
            Logger.Msg("PermissionManager compatibility facade initialized");
        }

        /// <summary>
        /// Checks if a subject has operator privileges.
        /// </summary>
        /// <param name="steamId">The subject identifier.</param>
        /// <returns><see langword="true"/> if the subject is an operator.</returns>
        public static bool IsOperator(string steamId)
        {
#if SERVER
            return ServerBootstrap.Permissions?.GetEffectiveGroups(steamId).Contains(PermissionBuiltIns.Groups.Operator, StringComparer.OrdinalIgnoreCase) == true;
#else
            return false;
#endif
        }

        /// <summary>
        /// Checks if a player has operator privileges.
        /// </summary>
        /// <param name="player">The player to check.</param>
        /// <returns><see langword="true"/> if the player is an operator.</returns>
        public static bool IsOperator(Player player)
        {
            return IsOperator(PlayerResolver.GetSteamId(player));
        }

        /// <summary>
        /// Checks if a subject has administrator privileges.
        /// </summary>
        /// <param name="steamId">The subject identifier.</param>
        /// <returns><see langword="true"/> if the subject is an administrator.</returns>
        public static bool IsAdmin(string steamId)
        {
#if SERVER
            return ServerBootstrap.Permissions?.GetEffectiveGroups(steamId).Contains(PermissionBuiltIns.Groups.Administrator, StringComparer.OrdinalIgnoreCase) == true;
#else
            return false;
#endif
        }

        /// <summary>
        /// Checks if a player has administrator privileges.
        /// </summary>
        /// <param name="player">The player to check.</param>
        /// <returns><see langword="true"/> if the player is an administrator.</returns>
        public static bool IsAdmin(Player player)
        {
            return IsAdmin(PlayerResolver.GetSteamId(player));
        }

        /// <summary>
        /// Adds a subject to the operator group.
        /// </summary>
        /// <param name="steamId">The subject identifier.</param>
        /// <returns><see langword="true"/> if the operator assignment changed.</returns>
        public static bool AddOperator(string steamId)
        {
#if SERVER
            bool changed = ServerBootstrap.Permissions?.AssignGroup(null, steamId, PermissionBuiltIns.Groups.Operator, "compat_add_operator") == true;
            if (changed)
            {
                OperatorAdded?.Invoke(steamId);
                PermissionsChanged?.Invoke();
            }

            return changed;
#else
            return false;
#endif
        }

        /// <summary>
        /// Removes a subject from the operator group.
        /// </summary>
        /// <param name="steamId">The subject identifier.</param>
        /// <returns><see langword="true"/> if the operator assignment changed.</returns>
        public static bool RemoveOperator(string steamId)
        {
#if SERVER
            bool changed = ServerBootstrap.Permissions?.UnassignGroup(null, steamId, PermissionBuiltIns.Groups.Operator, "compat_remove_operator") == true;
            if (changed)
            {
                OperatorRemoved?.Invoke(steamId);
                PermissionsChanged?.Invoke();
            }

            return changed;
#else
            return false;
#endif
        }

        /// <summary>
        /// Adds a subject to the administrator group.
        /// </summary>
        /// <param name="steamId">The subject identifier.</param>
        /// <returns><see langword="true"/> if the administrator assignment changed.</returns>
        public static bool AddAdmin(string steamId)
        {
#if SERVER
            bool changed = ServerBootstrap.Permissions?.AssignGroup(null, steamId, PermissionBuiltIns.Groups.Administrator, "compat_add_admin") == true;
            if (changed)
            {
                AdminAdded?.Invoke(steamId);
                PermissionsChanged?.Invoke();
            }

            return changed;
#else
            return false;
#endif
        }

        /// <summary>
        /// Removes a subject from the administrator group.
        /// </summary>
        /// <param name="steamId">The subject identifier.</param>
        /// <returns><see langword="true"/> if the administrator assignment changed.</returns>
        public static bool RemoveAdmin(string steamId)
        {
#if SERVER
            bool changed = ServerBootstrap.Permissions?.UnassignGroup(null, steamId, PermissionBuiltIns.Groups.Administrator, "compat_remove_admin") == true;
            if (changed)
            {
                AdminRemoved?.Invoke(steamId);
                PermissionsChanged?.Invoke();
            }

            return changed;
#else
            return false;
#endif
        }

        /// <summary>
        /// Gets all directly assigned operators.
        /// </summary>
        /// <returns>The operator identifiers.</returns>
        public static IReadOnlyList<string> GetAllOperators()
        {
#if SERVER
            return ServerBootstrap.Permissions?.GetDirectUsersInGroup(PermissionBuiltIns.Groups.Operator) ?? Array.Empty<string>();
#else
            return Array.Empty<string>();
#endif
        }

        /// <summary>
        /// Gets all directly assigned administrators.
        /// </summary>
        /// <returns>The administrator identifiers.</returns>
        public static IReadOnlyList<string> GetAllAdmins()
        {
#if SERVER
            return ServerBootstrap.Permissions?.GetDirectUsersInGroup(PermissionBuiltIns.Groups.Administrator) ?? Array.Empty<string>();
#else
            return Array.Empty<string>();
#endif
        }

        /// <summary>
        /// Checks whether a subject is banned.
        /// </summary>
        /// <param name="steamId">The subject identifier.</param>
        /// <returns><see langword="true"/> if banned.</returns>
        public static bool IsBanned(string steamId)
        {
#if SERVER
            return ServerBootstrap.Permissions?.IsBanned(steamId) == true;
#else
            return false;
#endif
        }

        /// <summary>
        /// Adds a ban for a subject.
        /// </summary>
        /// <param name="steamId">The subject identifier.</param>
        /// <returns><see langword="true"/> if the ban changed.</returns>
        public static bool AddBan(string steamId)
        {
#if SERVER
            bool changed = ServerBootstrap.Permissions?.AddBan(null, steamId, "compat_add_ban") == true;
            if (changed)
            {
                PlayerBanned?.Invoke(steamId);
                PermissionsChanged?.Invoke();
            }

            return changed;
#else
            return false;
#endif
        }

        /// <summary>
        /// Removes a ban for a subject.
        /// </summary>
        /// <param name="steamId">The subject identifier.</param>
        /// <returns><see langword="true"/> if the ban changed.</returns>
        public static bool RemoveBan(string steamId)
        {
#if SERVER
            bool changed = ServerBootstrap.Permissions?.RemoveBan(null, steamId, "compat_remove_ban") == true;
            if (changed)
            {
                BanRemoved?.Invoke(steamId);
                PermissionsChanged?.Invoke();
            }

            return changed;
#else
            return false;
#endif
        }

        /// <summary>
        /// Gets all banned subject identifiers.
        /// </summary>
        /// <returns>The banned identifiers.</returns>
        public static IReadOnlyList<string> GetAllBanned()
        {
#if SERVER
            IReadOnlyCollection<BanEntry> bans = ServerBootstrap.Permissions?.GetBanEntries();
            return bans == null
                ? Array.Empty<string>()
                : bans.Select(entry => entry.SubjectId).ToList().AsReadOnly();
#else
            return Array.Empty<string>();
#endif
        }

        /// <summary>
        /// Checks if a player can open the remote admin console.
        /// </summary>
        /// <param name="player">The player to evaluate.</param>
        /// <returns><see langword="true"/> if the player can open the console.</returns>
        public static bool CanUseConsole(Player player)
        {
#if SERVER
            return player != null && ServerBootstrap.Permissions?.HasPermission(PlayerResolver.GetSteamId(player), PermissionBuiltIns.Nodes.ConsoleOpen) == true;
#else
            return false;
#endif
        }

        /// <summary>
        /// Checks if a player can use a specific command.
        /// </summary>
        /// <param name="player">The player to evaluate.</param>
        /// <param name="command">The command word.</param>
        /// <returns><see langword="true"/> if the command is permitted.</returns>
        public static bool CanUseCommand(Player player, string command)
        {
            return CanUseCommand(PlayerResolver.GetSteamId(player), command);
        }

        /// <summary>
        /// Checks if a subject can use a specific command.
        /// </summary>
        /// <param name="steamId">The subject identifier.</param>
        /// <param name="command">The command word.</param>
        /// <returns><see langword="true"/> if the command is permitted.</returns>
        public static bool CanUseCommand(string steamId, string command)
        {
#if SERVER
            if (string.IsNullOrWhiteSpace(steamId) || string.IsNullOrWhiteSpace(command))
            {
                return false;
            }

            string node = MapCommandToPermissionNode(command);
            return ServerBootstrap.Permissions?.HasPermission(steamId, node) == true;
#else
            return false;
#endif
        }

        /// <summary>
        /// Checks if a player is allowed to connect.
        /// </summary>
        /// <param name="player">The player to evaluate.</param>
        /// <returns><see langword="true"/> if allowed.</returns>
        public static bool CanPlayerConnect(Player player)
        {
            return player != null && CanPlayerConnect(PlayerResolver.GetSteamId(player));
        }

        /// <summary>
        /// Checks if a subject is allowed to connect.
        /// </summary>
        /// <param name="steamId">The subject identifier.</param>
        /// <returns><see langword="true"/> if allowed.</returns>
        public static bool CanPlayerConnect(string steamId)
        {
            return !IsBanned(steamId);
        }

        /// <summary>
        /// Logs an administrative action to the admin log.
        /// </summary>
        /// <param name="player">The acting player.</param>
        /// <param name="command">The command word.</param>
        /// <param name="args">The optional argument text.</param>
        public static void LogAdminAction(Player player, string command, string args = "")
        {
            if (!Config.LogAdminCommands)
            {
                return;
            }

            string steamId = PlayerResolver.GetSteamId(player);
            string playerName = player?.PlayerName ?? "Unknown";
            string message = $"Admin Action - Player: {playerName} ({steamId}) | Command: {command}";
            if (!string.IsNullOrWhiteSpace(args))
            {
                message += $" | Args: {args}";
            }

            Logger.Msg(message);
            Utils.DebugLog.WriteToAdminLog(message);
        }

        /// <summary>
        /// Gets a formatted permission summary.
        /// </summary>
        /// <returns>The permission summary text.</returns>
        public static string GetPermissionSummary()
        {
#if SERVER
            DedicatedServerMod.Shared.Permissions.PermissionSummary summary = ServerBootstrap.Permissions?.GetSummary();
            if (summary == null)
            {
                return "Permissions unavailable";
            }

            return $"Permissions: {summary.TotalGroups} groups, {summary.TotalUsers} users, {summary.TotalBans} banned, {summary.TotalOperators} operators, {summary.TotalAdministrators} administrators";
#else
            return "Permissions unavailable";
#endif
        }

#if SERVER
        private static string MapCommandToPermissionNode(string command)
        {
            string normalizedCommand = command.Trim().ToLowerInvariant();

            switch (normalizedCommand)
            {
                case "help":
                    return PermissionBuiltIns.Nodes.ServerHelp;
                case "serverinfo":
                    return PermissionBuiltIns.Nodes.ServerInfo;
                case "save":
                    return PermissionBuiltIns.Nodes.ServerSave;
                case "reloadconfig":
                    return PermissionBuiltIns.Nodes.ServerReloadConfig;
                case "shutdown":
                    return PermissionBuiltIns.Nodes.ServerStop;
                case "listplayers":
                    return PermissionBuiltIns.Nodes.PlayerList;
                case "kick":
                    return PermissionBuiltIns.Nodes.PlayerKick;
                case "ban":
                    return PermissionBuiltIns.Nodes.PlayerBan;
                case "unban":
                    return PermissionBuiltIns.Nodes.PlayerUnban;
                case "reloadpermissions":
                    return PermissionBuiltIns.Nodes.PermissionsReload;
                case "listops":
                case "listadmins":
                    return PermissionBuiltIns.Nodes.PermissionsGroupList;
                case "op":
                case "admin":
                    return PermissionBuiltIns.Nodes.PermissionsGroupAssign;
                case "deop":
                case "deadmin":
                    return PermissionBuiltIns.Nodes.PermissionsGroupUnassign;
                default:
                    return PermissionNode.CreateConsoleCommandNode(normalizedCommand);
            }
        }
#endif
    }
}
