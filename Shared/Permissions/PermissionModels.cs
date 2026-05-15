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
        /// <summary>
        /// Gets or sets the normalized permission node.
        /// </summary>
        public string Node { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display category used by help and diagnostics surfaces.
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the human-readable purpose of the node.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the built-in groups that commonly receive this node.
        /// </summary>
        public List<string> SuggestedGroups { get; set; } = new List<string>();
    }

    /// <summary>
    /// Represents a permanent permission group.
    /// </summary>
    public sealed class PermissionGroupDefinition
    {
        /// <summary>
        /// Gets or sets the normalized group name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the group priority used to break equally specific group-rule ties.
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Gets or sets the parent groups inherited by this group.
        /// </summary>
        public List<string> Inherits { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the allow node patterns granted by this group.
        /// </summary>
        public List<string> Allow { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the deny node patterns applied by this group.
        /// </summary>
        public List<string> Deny { get; set; } = new List<string>();
    }

    /// <summary>
    /// Represents a user-specific permission record.
    /// </summary>
    public sealed class PermissionUserRecord
    {
        /// <summary>
        /// Gets or sets the normalized subject identifier, usually a SteamID64.
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the permanent groups directly assigned to the user.
        /// </summary>
        public List<string> Groups { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets direct allow node patterns for the user.
        /// </summary>
        public List<string> Allow { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets direct deny node patterns for the user.
        /// </summary>
        public List<string> Deny { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets temporary group assignments for the user.
        /// </summary>
        public List<TemporaryGroupAssignment> TemporaryGroups { get; set; } = new List<TemporaryGroupAssignment>();

        /// <summary>
        /// Gets or sets temporary allow grants for the user.
        /// </summary>
        public List<TemporaryPermissionGrant> TemporaryAllow { get; set; } = new List<TemporaryPermissionGrant>();

        /// <summary>
        /// Gets or sets temporary deny grants for the user.
        /// </summary>
        public List<TemporaryPermissionGrant> TemporaryDeny { get; set; } = new List<TemporaryPermissionGrant>();
    }

    /// <summary>
    /// Represents a temporary node assignment.
    /// </summary>
    public sealed class TemporaryPermissionGrant
    {
        /// <summary>
        /// Gets or sets the stable temporary grant identifier.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the permission node or pattern granted or denied by this entry.
        /// </summary>
        public string Node { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the UTC expiration timestamp.
        /// </summary>
        public DateTime ExpiresAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the actor that created the temporary grant.
        /// </summary>
        public string GrantedBy { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the operator-provided reason for the temporary grant.
        /// </summary>
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a temporary group assignment.
    /// </summary>
    public sealed class TemporaryGroupAssignment
    {
        /// <summary>
        /// Gets or sets the stable temporary assignment identifier.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the normalized group name assigned by this entry.
        /// </summary>
        public string GroupName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the UTC expiration timestamp.
        /// </summary>
        public DateTime ExpiresAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the actor that created the temporary assignment.
        /// </summary>
        public string GrantedBy { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the operator-provided reason for the temporary assignment.
        /// </summary>
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a ban entry in the permissions store.
    /// </summary>
    public sealed class BanEntry
    {
        /// <summary>
        /// Gets or sets the banned subject identifier, usually a SteamID64.
        /// </summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets when the ban was created in UTC.
        /// </summary>
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the actor that created the ban.
        /// </summary>
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the operator-provided ban reason.
        /// </summary>
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents the persisted permissions data file.
    /// </summary>
    public sealed class PermissionStoreData
    {
        /// <summary>
        /// Gets or sets the persisted permission file schema version.
        /// </summary>
        public int SchemaVersion { get; set; } = 1;

        /// <summary>
        /// Gets or sets the last applied migration version.
        /// </summary>
        public int MigrationVersion { get; set; } = 1;

        /// <summary>
        /// Gets or sets the migration source label when data was imported from an older format.
        /// </summary>
        public string MigratedFrom { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the UTC timestamp when migration occurred.
        /// </summary>
        public DateTime? MigratedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets persisted group definitions keyed by normalized group name.
        /// </summary>
        public Dictionary<string, PermissionGroupDefinition> Groups { get; set; } = new Dictionary<string, PermissionGroupDefinition>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets persisted user records keyed by normalized subject identifier.
        /// </summary>
        public Dictionary<string, PermissionUserRecord> Users { get; set; } = new Dictionary<string, PermissionUserRecord>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets persisted ban entries keyed by normalized subject identifier.
        /// </summary>
        public Dictionary<string, BanEntry> Bans { get; set; } = new Dictionary<string, BanEntry>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Describes the effective result of evaluating a permission node.
    /// </summary>
    public sealed class PermissionEvaluationResult
    {
        /// <summary>
        /// Gets or sets the subject identifier that was evaluated.
        /// </summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the normalized permission node that was evaluated.
        /// </summary>
        public string Node { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the node was granted.
        /// </summary>
        public bool IsGranted { get; set; }

        /// <summary>
        /// Gets or sets the matching rule or pattern responsible for the result.
        /// </summary>
        public string MatchedRule { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the source kind for the matching rule, such as direct user, temporary grant, or group.
        /// </summary>
        public string SourceType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the source name for the matching rule.
        /// </summary>
        public string SourceName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the specificity score used during rule resolution.
        /// </summary>
        public int Specificity { get; set; } = -1;

        /// <summary>
        /// Gets or sets the source priority used during rule resolution.
        /// </summary>
        public int Priority { get; set; }
    }

    /// <summary>
    /// Describes the local client capabilities derived from the server-side permission graph.
    /// </summary>
    public sealed class PermissionCapabilitySnapshot
    {
        /// <summary>
        /// Gets or sets the subject identifier the snapshot describes.
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the client may open the in-game admin console UI.
        /// </summary>
        public bool CanOpenConsole { get; set; }

        /// <summary>
        /// Gets or sets whether the client may execute remote console commands.
        /// </summary>
        public bool CanUseRemoteConsole { get; set; }

        /// <summary>
        /// Gets or sets the remote command words currently visible to the client.
        /// </summary>
        public List<string> AllowedRemoteCommands { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets when the server issued this snapshot in UTC.
        /// </summary>
        public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Summarizes the current permission store state.
    /// </summary>
    public sealed class PermissionSummary
    {
        /// <summary>
        /// Gets or sets the total number of permission groups.
        /// </summary>
        public int TotalGroups { get; set; }

        /// <summary>
        /// Gets or sets the total number of users with direct permission records.
        /// </summary>
        public int TotalUsers { get; set; }

        /// <summary>
        /// Gets or sets the total number of ban entries.
        /// </summary>
        public int TotalBans { get; set; }

        /// <summary>
        /// Gets or sets the total number of users effectively in the built-in operator group.
        /// </summary>
        public int TotalOperators { get; set; }

        /// <summary>
        /// Gets or sets the total number of users effectively in the built-in administrator group.
        /// </summary>
        public int TotalAdministrators { get; set; }
    }
}
