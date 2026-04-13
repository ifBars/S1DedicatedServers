namespace DedicatedServerMod.Shared.Permissions
{
    /// <summary>
    /// Built-in permission group names and node constants.
    /// </summary>
    public static class PermissionBuiltIns
    {
        /// <summary>
        /// Built-in staff group names.
        /// </summary>
        public static class Groups
        {
            public const string Default = "default";
            public const string Support = "support";
            public const string Moderator = "moderator";
            public const string Administrator = "administrator";
            public const string Operator = "operator";
        }

        /// <summary>
        /// Built-in framework permission nodes.
        /// </summary>
        public static class Nodes
        {
            public const string All = "*";
            public const string ConsoleOpen = "console.open";
            public const string ConsoleCommandWildcard = "console.command.*";
            public const string ClientModPolicyBypass = "clientmods.policy.bypass";
            public const string PlayerList = "player.list";
            public const string PlayerKick = "player.kick";
            public const string PlayerBan = "player.ban";
            public const string PlayerUnban = "player.unban";
            public const string PlayerBring = "player.bring";
            public const string PlayerReturn = "player.return";
            public const string PlayerVanish = "player.vanish";
            public const string ServerHelp = "server.help";
            public const string ServerInfo = "server.info";
            public const string ServerSave = "server.save";
            public const string ServerReloadConfig = "server.reloadconfig";
            public const string ServerStop = "server.stop";
            public const string PermissionsReload = "permissions.reload";
            public const string PermissionsInfo = "permissions.info";
            public const string PermissionsGrant = "permissions.grant";
            public const string PermissionsDeny = "permissions.deny";
            public const string PermissionsRevoke = "permissions.revoke";
            public const string PermissionsTempGrant = "permissions.tempgrant";
            public const string PermissionsGroupList = "permissions.group.list";
            public const string PermissionsGroupAssign = "permissions.group.assign";
            public const string PermissionsGroupUnassign = "permissions.group.unassign";
        }
    }

    /// <summary>
    /// Describes a permission node that can be registered by the framework or addons.
    /// </summary>
    public sealed class PermissionDefinition
    {
        public string Node { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> SuggestedGroups { get; set; } = new List<string>();
    }

    /// <summary>
    /// Represents a permanent permission group.
    /// </summary>
    public sealed class PermissionGroupDefinition
    {
        public string Name { get; set; } = string.Empty;
        public int Priority { get; set; }
        public List<string> Inherits { get; set; } = new List<string>();
        public List<string> Allow { get; set; } = new List<string>();
        public List<string> Deny { get; set; } = new List<string>();
    }

    /// <summary>
    /// Represents a user-specific permission record.
    /// </summary>
    public sealed class PermissionUserRecord
    {
        public string UserId { get; set; } = string.Empty;
        public List<string> Groups { get; set; } = new List<string>();
        public List<string> Allow { get; set; } = new List<string>();
        public List<string> Deny { get; set; } = new List<string>();
        public List<TemporaryGroupAssignment> TemporaryGroups { get; set; } = new List<TemporaryGroupAssignment>();
        public List<TemporaryPermissionGrant> TemporaryAllow { get; set; } = new List<TemporaryPermissionGrant>();
        public List<TemporaryPermissionGrant> TemporaryDeny { get; set; } = new List<TemporaryPermissionGrant>();
    }

    /// <summary>
    /// Represents a temporary node assignment.
    /// </summary>
    public sealed class TemporaryPermissionGrant
    {
        public string Id { get; set; } = string.Empty;
        public string Node { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
        public string GrantedBy { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a temporary group assignment.
    /// </summary>
    public sealed class TemporaryGroupAssignment
    {
        public string Id { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
        public string GrantedBy { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a ban entry in the permissions store.
    /// </summary>
    public sealed class BanEntry
    {
        public string SubjectId { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents the persisted permissions data file.
    /// </summary>
    public sealed class PermissionStoreData
    {
        public int SchemaVersion { get; set; } = 1;
        public int MigrationVersion { get; set; } = 1;
        public string MigratedFrom { get; set; } = string.Empty;
        public DateTime? MigratedAtUtc { get; set; }
        public Dictionary<string, PermissionGroupDefinition> Groups { get; set; } = new Dictionary<string, PermissionGroupDefinition>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, PermissionUserRecord> Users { get; set; } = new Dictionary<string, PermissionUserRecord>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, BanEntry> Bans { get; set; } = new Dictionary<string, BanEntry>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Describes the effective result of evaluating a permission node.
    /// </summary>
    public sealed class PermissionEvaluationResult
    {
        public string SubjectId { get; set; } = string.Empty;
        public string Node { get; set; } = string.Empty;
        public bool IsGranted { get; set; }
        public string MatchedRule { get; set; } = string.Empty;
        public string SourceType { get; set; } = string.Empty;
        public string SourceName { get; set; } = string.Empty;
        public int Specificity { get; set; } = -1;
        public int Priority { get; set; }
    }

    /// <summary>
    /// Describes the local client capabilities derived from the server-side permission graph.
    /// </summary>
    public sealed class PermissionCapabilitySnapshot
    {
        public string UserId { get; set; } = string.Empty;
        public bool CanOpenConsole { get; set; }
        public bool CanUseRemoteConsole { get; set; }
        public List<string> AllowedRemoteCommands { get; set; } = new List<string>();
        public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Summarizes the current permission store state.
    /// </summary>
    public sealed class PermissionSummary
    {
        public int TotalGroups { get; set; }
        public int TotalUsers { get; set; }
        public int TotalBans { get; set; }
        public int TotalOperators { get; set; }
        public int TotalAdministrators { get; set; }
    }
}
