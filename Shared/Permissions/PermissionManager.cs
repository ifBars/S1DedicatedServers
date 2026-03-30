#if SERVER
using DedicatedServerMod.Server.Core;
#endif
using DedicatedServerMod.Shared.Configuration;
using DedicatedServerMod.Utils;
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
    /// Permission mutation events exposed by this facade are authoritative on server builds only.
    /// Most permission queries and mutation helpers also remain server-authoritative. Client builds
    /// expose compatibility stubs for API stability, but those members return fallback values such as
    /// <see langword="false"/>, empty collections, or <c>"Permissions unavailable"</c> instead of
    /// authoritative permission state.
    /// </remarks>
    public static class PermissionManager
    {
        private const string ServerOnlyEventMessage =
            "PermissionManager.{0} is server-only. Client-side subscriptions are ignored because permission mutations are authoritative on the dedicated server.";

        /// <summary>
        /// Raised on the server when an operator is added.
        /// </summary>
        /// <remarks>
        /// This event is only raised in server builds. Client builds do not receive permission
        /// mutation callbacks and ignore subscriptions to this event.
        /// </remarks>
#if CLIENT
        [Obsolete("PermissionManager.OperatorAdded is server-only. Client builds do not raise permission mutation events.", false)]
#endif
        public static event Action<string> OperatorAdded
        {
#if SERVER
            add => _operatorAdded += value;
            remove => _operatorAdded -= value;
#else
            add => WarnServerOnlyEventSubscription(nameof(OperatorAdded));
            remove { }
#endif
        }

        /// <summary>
        /// Raised on the server when an operator is removed.
        /// </summary>
        /// <remarks>
        /// This event is only raised in server builds. Client builds do not receive permission
        /// mutation callbacks and ignore subscriptions to this event.
        /// </remarks>
#if CLIENT
        [Obsolete("PermissionManager.OperatorRemoved is server-only. Client builds do not raise permission mutation events.", false)]
#endif
        public static event Action<string> OperatorRemoved
        {
#if SERVER
            add => _operatorRemoved += value;
            remove => _operatorRemoved -= value;
#else
            add => WarnServerOnlyEventSubscription(nameof(OperatorRemoved));
            remove { }
#endif
        }

        /// <summary>
        /// Raised on the server when an administrator is added.
        /// </summary>
        /// <remarks>
        /// This event is only raised in server builds. Client builds do not receive permission
        /// mutation callbacks and ignore subscriptions to this event.
        /// </remarks>
#if CLIENT
        [Obsolete("PermissionManager.AdminAdded is server-only. Client builds do not raise permission mutation events.", false)]
#endif
        public static event Action<string> AdminAdded
        {
#if SERVER
            add => _adminAdded += value;
            remove => _adminAdded -= value;
#else
            add => WarnServerOnlyEventSubscription(nameof(AdminAdded));
            remove { }
#endif
        }

        /// <summary>
        /// Raised on the server when an administrator is removed.
        /// </summary>
        /// <remarks>
        /// This event is only raised in server builds. Client builds do not receive permission
        /// mutation callbacks and ignore subscriptions to this event.
        /// </remarks>
#if CLIENT
        [Obsolete("PermissionManager.AdminRemoved is server-only. Client builds do not raise permission mutation events.", false)]
#endif
        public static event Action<string> AdminRemoved
        {
#if SERVER
            add => _adminRemoved += value;
            remove => _adminRemoved -= value;
#else
            add => WarnServerOnlyEventSubscription(nameof(AdminRemoved));
            remove { }
#endif
        }

        /// <summary>
        /// Raised on the server when a player is banned.
        /// </summary>
        /// <remarks>
        /// This event is only raised in server builds. Client builds do not receive permission
        /// mutation callbacks and ignore subscriptions to this event.
        /// </remarks>
#if CLIENT
        [Obsolete("PermissionManager.PlayerBanned is server-only. Client builds do not raise permission mutation events.", false)]
#endif
        public static event Action<string> PlayerBanned
        {
#if SERVER
            add => _playerBanned += value;
            remove => _playerBanned -= value;
#else
            add => WarnServerOnlyEventSubscription(nameof(PlayerBanned));
            remove { }
#endif
        }

        /// <summary>
        /// Raised on the server when a ban is removed.
        /// </summary>
        /// <remarks>
        /// This event is only raised in server builds. Client builds do not receive permission
        /// mutation callbacks and ignore subscriptions to this event.
        /// </remarks>
#if CLIENT
        [Obsolete("PermissionManager.BanRemoved is server-only. Client builds do not raise permission mutation events.", false)]
#endif
        public static event Action<string> BanRemoved
        {
#if SERVER
            add => _banRemoved += value;
            remove => _banRemoved -= value;
#else
            add => WarnServerOnlyEventSubscription(nameof(BanRemoved));
            remove { }
#endif
        }

        /// <summary>
        /// Raised on the server when permissions change.
        /// </summary>
        /// <remarks>
        /// This event is only raised in server builds. Client builds do not receive permission
        /// mutation callbacks and ignore subscriptions to this event.
        /// </remarks>
#if CLIENT
        [Obsolete("PermissionManager.PermissionsChanged is server-only. Client builds do not raise permission mutation events.", false)]
#endif
        public static event Action PermissionsChanged
        {
#if SERVER
            add => _permissionsChanged += value;
            remove => _permissionsChanged -= value;
#else
            add => WarnServerOnlyEventSubscription(nameof(PermissionsChanged));
            remove { }
#endif
        }

#if SERVER
        private static Action<string> _operatorAdded;
        private static Action<string> _operatorRemoved;
        private static Action<string> _adminAdded;
        private static Action<string> _adminRemoved;
        private static Action<string> _playerBanned;
        private static Action<string> _banRemoved;
        private static Action _permissionsChanged;
#endif

        /// <summary>
        /// Gets the server configuration instance.
        /// </summary>
        internal static ServerConfig Config => ServerConfig.Instance;

        /// <summary>
        /// Initializes the compatibility facade.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        public static void Initialize(MelonLogger.Instance logger)
        {
            DebugLog.StartupDebug("PermissionManager compatibility facade initialized");
        }

        /// <summary>
        /// Checks if a subject has operator privileges.
        /// </summary>
        /// <param name="steamId">The subject identifier.</param>
        /// <returns><see langword="true"/> if the subject is an operator.</returns>
        /// <remarks>
        /// This check is authoritative on the server. Client builds return <see langword="false"/>
        /// because group membership is not resolved locally by this compatibility facade.
        /// </remarks>
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
        /// <remarks>
        /// This overload delegates to <see cref="IsOperator(string)"/>. Client builds therefore
        /// also return <see langword="false"/>.
        /// </remarks>
        public static bool IsOperator(Player player)
        {
            return IsOperator(PlayerResolver.GetSteamId(player));
        }

        /// <summary>
        /// Checks if a subject has administrator privileges.
        /// </summary>
        /// <param name="steamId">The subject identifier.</param>
        /// <returns><see langword="true"/> if the subject is an administrator.</returns>
        /// <remarks>
        /// This check is authoritative on the server. Client builds return <see langword="false"/>
        /// because administrator group membership is not resolved locally by this compatibility facade.
        /// </remarks>
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
        /// <remarks>
        /// This overload delegates to <see cref="IsAdmin(string)"/>. Client builds therefore also
        /// return <see langword="false"/>.
        /// </remarks>
        public static bool IsAdmin(Player player)
        {
            return IsAdmin(PlayerResolver.GetSteamId(player));
        }

        /// <summary>
        /// Adds a subject to the operator group.
        /// </summary>
        /// <param name="steamId">The subject identifier.</param>
        /// <returns><see langword="true"/> if the operator assignment changed.</returns>
        /// <remarks>
        /// Mutations are server-only. Client builds ignore this request and return
        /// <see langword="false"/>.
        /// </remarks>
        public static bool AddOperator(string steamId)
        {
#if SERVER
            bool changed = ServerBootstrap.Permissions?.AssignGroup(null, steamId, PermissionBuiltIns.Groups.Operator, "compat_add_operator") == true;
            if (changed)
            {
                _operatorAdded?.Invoke(steamId);
                _permissionsChanged?.Invoke();
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
        /// <remarks>
        /// Mutations are server-only. Client builds ignore this request and return
        /// <see langword="false"/>.
        /// </remarks>
        public static bool RemoveOperator(string steamId)
        {
#if SERVER
            bool changed = ServerBootstrap.Permissions?.UnassignGroup(null, steamId, PermissionBuiltIns.Groups.Operator, "compat_remove_operator") == true;
            if (changed)
            {
                _operatorRemoved?.Invoke(steamId);
                _permissionsChanged?.Invoke();
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
        /// <remarks>
        /// Mutations are server-only. Client builds ignore this request and return
        /// <see langword="false"/>.
        /// </remarks>
        public static bool AddAdmin(string steamId)
        {
#if SERVER
            bool changed = ServerBootstrap.Permissions?.AssignGroup(null, steamId, PermissionBuiltIns.Groups.Administrator, "compat_add_admin") == true;
            if (changed)
            {
                _adminAdded?.Invoke(steamId);
                _permissionsChanged?.Invoke();
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
        /// <remarks>
        /// Mutations are server-only. Client builds ignore this request and return
        /// <see langword="false"/>.
        /// </remarks>
        public static bool RemoveAdmin(string steamId)
        {
#if SERVER
            bool changed = ServerBootstrap.Permissions?.UnassignGroup(null, steamId, PermissionBuiltIns.Groups.Administrator, "compat_remove_admin") == true;
            if (changed)
            {
                _adminRemoved?.Invoke(steamId);
                _permissionsChanged?.Invoke();
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
        /// <remarks>
        /// This list is only available on the server. Client builds return an empty collection.
        /// </remarks>
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
        /// <remarks>
        /// This list is only available on the server. Client builds return an empty collection.
        /// </remarks>
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
        /// <remarks>
        /// Ban state is authoritative on the server. Client builds return <see langword="false"/>
        /// because they do not maintain the authoritative ban list through this facade.
        /// </remarks>
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
        /// <remarks>
        /// Mutations are server-only. Client builds ignore this request and return
        /// <see langword="false"/>.
        /// </remarks>
        public static bool AddBan(string steamId)
        {
#if SERVER
            bool changed = ServerBootstrap.Permissions?.AddBan(null, steamId, "compat_add_ban") == true;
            if (changed)
            {
                _playerBanned?.Invoke(steamId);
                _permissionsChanged?.Invoke();
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
        /// <remarks>
        /// Mutations are server-only. Client builds ignore this request and return
        /// <see langword="false"/>.
        /// </remarks>
        public static bool RemoveBan(string steamId)
        {
#if SERVER
            bool changed = ServerBootstrap.Permissions?.RemoveBan(null, steamId, "compat_remove_ban") == true;
            if (changed)
            {
                _banRemoved?.Invoke(steamId);
                _permissionsChanged?.Invoke();
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
        /// <remarks>
        /// This list is only available on the server. Client builds return an empty collection.
        /// </remarks>
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
        /// <remarks>
        /// Permission-node evaluation is authoritative on the server. Client builds return
        /// <see langword="false"/> because this facade does not mirror those permission nodes locally.
        /// </remarks>
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
        /// <remarks>
        /// This overload delegates to <see cref="CanUseCommand(string, string)"/>. Client builds
        /// therefore also return <see langword="false"/>.
        /// </remarks>
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
        /// <remarks>
        /// Permission-node evaluation is authoritative on the server. Client builds return
        /// <see langword="false"/> because command permissions are not resolved locally by this facade.
        /// </remarks>
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
        /// <remarks>
        /// This overload delegates to <see cref="CanPlayerConnect(string)"/>. On client builds the
        /// compatibility facade does not have authoritative ban state, so this method defaults to
        /// <see langword="true"/> for non-null players.
        /// </remarks>
        public static bool CanPlayerConnect(Player player)
        {
            return player != null && CanPlayerConnect(PlayerResolver.GetSteamId(player));
        }

        /// <summary>
        /// Checks if a subject is allowed to connect.
        /// </summary>
        /// <param name="steamId">The subject identifier.</param>
        /// <returns><see langword="true"/> if allowed.</returns>
        /// <remarks>
        /// Server builds deny subjects that are present in the authoritative ban list. Client builds
        /// do not have that authoritative state through this facade and therefore default to
        /// <see langword="true"/>.
        /// </remarks>
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

            DebugLog.Info(message);
            DebugLog.WriteToAdminLog(message);
        }

        /// <summary>
        /// Gets a formatted permission summary.
        /// </summary>
        /// <returns>The permission summary text.</returns>
        /// <remarks>
        /// Summary data is only available on the server. Client builds return
        /// <c>"Permissions unavailable"</c>.
        /// </remarks>
        public static string GetPermissionSummary()
        {
#if SERVER
            PermissionSummary summary = ServerBootstrap.Permissions?.GetSummary();
            if (summary == null)
            {
                return "Permissions unavailable";
            }

            return $"Permissions: {summary.TotalGroups} groups, {summary.TotalUsers} users, {summary.TotalBans} banned, {summary.TotalOperators} operators, {summary.TotalAdministrators} administrators";
#else
            return "Permissions unavailable";
#endif
        }

        private static void WarnServerOnlyEventSubscription(string eventName)
        {
            DebugLog.Warning(string.Format(ServerOnlyEventMessage, eventName));
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
