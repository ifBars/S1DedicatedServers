using DedicatedServerMod.Server.Core;
using DedicatedServerMod.Shared.Permissions;

namespace DedicatedServerMod.Server.Player
{
    /// <summary>
    /// Compatibility wrapper for legacy player permission queries.
    /// </summary>
    public sealed class PlayerPermissions
    {
        /// <summary>
        /// Initializes the permission wrapper.
        /// </summary>
        public void Initialize()
        {
            Utils.DebugLog.Info("Player permissions compatibility wrapper initialized");
        }

        /// <summary>
        /// Checks if a subject is an operator.
        /// </summary>
        public bool IsOperator(string steamId)
        {
            return PermissionManager.IsOperator(steamId);
        }

        /// <summary>
        /// Checks if a subject is an administrator.
        /// </summary>
        public bool IsAdministrator(string steamId)
        {
            return PermissionManager.IsAdmin(steamId);
        }

        /// <summary>
        /// Checks if a player is an operator.
        /// </summary>
        public bool IsOperator(ConnectedPlayerInfo player)
        {
            return IsOperator(player?.TrustedUniqueId);
        }

        /// <summary>
        /// Checks if a player is an administrator.
        /// </summary>
        public bool IsAdministrator(ConnectedPlayerInfo player)
        {
            return IsAdministrator(player?.TrustedUniqueId);
        }

        /// <summary>
        /// Checks if a player has elevated privileges.
        /// </summary>
        public bool HasElevatedPrivileges(string steamId)
        {
            PermissionLevel level = GetPermissionLevel(steamId);
            return level >= PermissionLevel.Administrator;
        }

        /// <summary>
        /// Checks if a player has elevated privileges.
        /// </summary>
        public bool HasElevatedPrivileges(ConnectedPlayerInfo player)
        {
            return HasElevatedPrivileges(player?.TrustedUniqueId);
        }

        /// <summary>
        /// Adds a subject as an operator.
        /// </summary>
        public bool AddOperator(string steamId)
        {
            return PermissionManager.AddOperator(steamId);
        }

        /// <summary>
        /// Removes a subject from operators.
        /// </summary>
        public bool RemoveOperator(string steamId)
        {
            return PermissionManager.RemoveOperator(steamId);
        }

        /// <summary>
        /// Adds a subject as an administrator.
        /// </summary>
        public bool AddAdministrator(string steamId)
        {
            return PermissionManager.AddAdmin(steamId);
        }

        /// <summary>
        /// Removes a subject from administrators.
        /// </summary>
        public bool RemoveAdministrator(string steamId)
        {
            return PermissionManager.RemoveAdmin(steamId);
        }

        /// <summary>
        /// Gets directly assigned operators.
        /// </summary>
        public List<string> GetOperators()
        {
            return new List<string>(PermissionManager.GetAllOperators());
        }

        /// <summary>
        /// Gets directly assigned administrators.
        /// </summary>
        public List<string> GetAdministrators()
        {
            return new List<string>(PermissionManager.GetAllAdmins());
        }

        /// <summary>
        /// Gets the legacy permission level for a subject.
        /// </summary>
        public PermissionLevel GetPermissionLevel(string steamId)
        {
            if (string.IsNullOrWhiteSpace(steamId))
            {
                return PermissionLevel.None;
            }

            if (ServerBootstrap.Permissions?.GetEffectiveGroups(steamId).Contains(PermissionBuiltIns.Groups.Operator, StringComparer.OrdinalIgnoreCase) == true)
            {
                return PermissionLevel.Operator;
            }

            if (ServerBootstrap.Permissions?.GetEffectiveGroups(steamId).Contains(PermissionBuiltIns.Groups.Administrator, StringComparer.OrdinalIgnoreCase) == true ||
                ServerBootstrap.Permissions?.GetEffectiveGroups(steamId).Contains(PermissionBuiltIns.Groups.Moderator, StringComparer.OrdinalIgnoreCase) == true)
            {
                return PermissionLevel.Administrator;
            }

            return PermissionLevel.Player;
        }

        /// <summary>
        /// Gets the legacy permission level for a player.
        /// </summary>
        public PermissionLevel GetPermissionLevel(ConnectedPlayerInfo player)
        {
            return GetPermissionLevel(player?.TrustedUniqueId);
        }

        /// <summary>
        /// Checks if a player can execute a permission node.
        /// </summary>
        public bool CanExecuteCommand(ConnectedPlayerInfo player, string requiredPermissionNode)
        {
            if (string.IsNullOrWhiteSpace(requiredPermissionNode) || player == null)
            {
                return true;
            }

            return ServerBootstrap.Permissions?.HasPermission(player.TrustedUniqueId, requiredPermissionNode) == true;
        }

        /// <summary>
        /// Checks if a subject can execute a permission node.
        /// </summary>
        public bool CanExecuteCommand(string steamId, string requiredPermissionNode)
        {
            if (string.IsNullOrWhiteSpace(requiredPermissionNode) || string.IsNullOrWhiteSpace(steamId))
            {
                return true;
            }

            return ServerBootstrap.Permissions?.HasPermission(steamId, requiredPermissionNode) == true;
        }

        /// <summary>
        /// Checks if a player can execute a legacy command level.
        /// </summary>
        public bool CanExecuteCommand(ConnectedPlayerInfo player, PermissionLevel requiredLevel)
        {
            return GetPermissionLevel(player) >= requiredLevel;
        }

        /// <summary>
        /// Checks if a subject can execute a legacy command level.
        /// </summary>
        public bool CanExecuteCommand(string steamId, PermissionLevel requiredLevel)
        {
            return GetPermissionLevel(steamId) >= requiredLevel;
        }

        /// <summary>
        /// Checks whether an actor strictly outranks a target.
        /// </summary>
        public bool HasDominanceOver(ConnectedPlayerInfo actor, ConnectedPlayerInfo target)
        {
            return ServerBootstrap.Permissions?.HasDominanceOver(actor?.TrustedUniqueId, target?.TrustedUniqueId) == true;
        }

        /// <summary>
        /// Gets a summary of the current permission state.
        /// </summary>
        public PermissionSummary GetPermissionSummary()
        {
            DedicatedServerMod.Shared.Permissions.PermissionSummary summary = ServerBootstrap.Permissions?.GetSummary()
                ?? new DedicatedServerMod.Shared.Permissions.PermissionSummary();

            return new PermissionSummary
            {
                TotalOperators = summary.TotalOperators,
                TotalAdministrators = summary.TotalAdministrators,
                Operators = GetOperators(),
                Administrators = GetAdministrators()
            };
        }
    }

    /// <summary>
    /// Legacy permission levels preserved for transition compatibility.
    /// </summary>
    public enum PermissionLevel
    {
        None = 0,
        Player = 1,
        Administrator = 2,
        Operator = 3
    }

    /// <summary>
    /// Legacy permission summary model preserved for transition compatibility.
    /// </summary>
    public sealed class PermissionSummary
    {
        public int TotalOperators { get; set; }

        public int TotalAdministrators { get; set; }

        public List<string> Operators { get; set; } = new List<string>();

        public List<string> Administrators { get; set; } = new List<string>();

        public override string ToString()
        {
            return $"Operators: {TotalOperators}, Administrators: {TotalAdministrators}";
        }
    }
}
