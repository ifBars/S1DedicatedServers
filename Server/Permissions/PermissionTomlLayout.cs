namespace DedicatedServerMod.Server.Permissions
{
    /// <summary>
    /// Defines the canonical TOML layout for persisted permissions.
    /// </summary>
    internal static class PermissionTomlLayout
    {
        public const string MetadataTable = "metadata";
        public const string GroupPrefix = "group.";
        public const string UserPrefix = "user.";
        public const string TemporaryGroupPrefix = "tempgroup.";
        public const string TemporaryAllowPrefix = "tempallow.";
        public const string TemporaryDenyPrefix = "tempdeny.";
        public const string BanPrefix = "ban.";

        public static class Keys
        {
            public const string SchemaVersion = "schemaVersion";
            public const string MigrationVersion = "migrationVersion";
            public const string MigratedFrom = "migratedFrom";
            public const string MigratedAtUtc = "migratedAtUtc";
            public const string Priority = "priority";
            public const string Inherits = "inherits";
            public const string Allow = "allow";
            public const string Deny = "deny";
            public const string Groups = "groups";
            public const string UserId = "userId";
            public const string Group = "group";
            public const string Node = "node";
            public const string ExpiresAtUtc = "expiresAtUtc";
            public const string GrantedBy = "grantedBy";
            public const string Reason = "reason";
            public const string SubjectId = "subjectId";
            public const string CreatedAtUtc = "createdAtUtc";
            public const string CreatedBy = "createdBy";
        }

        public static readonly string[] MetadataKeys =
        {
            Keys.SchemaVersion,
            Keys.MigrationVersion,
            Keys.MigratedFrom,
            Keys.MigratedAtUtc
        };

        public static readonly string[] GroupKeys =
        {
            Keys.Priority,
            Keys.Inherits,
            Keys.Allow,
            Keys.Deny
        };

        public static readonly string[] UserKeys =
        {
            Keys.Groups,
            Keys.Allow,
            Keys.Deny
        };

        public static readonly string[] TemporaryGroupKeys =
        {
            Keys.UserId,
            Keys.Group,
            Keys.ExpiresAtUtc,
            Keys.GrantedBy,
            Keys.Reason
        };

        public static readonly string[] TemporaryNodeKeys =
        {
            Keys.UserId,
            Keys.Node,
            Keys.ExpiresAtUtc,
            Keys.GrantedBy,
            Keys.Reason
        };

        public static readonly string[] BanKeys =
        {
            Keys.SubjectId,
            Keys.CreatedAtUtc,
            Keys.CreatedBy,
            Keys.Reason
        };

        public static bool IsManagedTable(string tableName)
        {
            return string.Equals(tableName, MetadataTable, StringComparison.Ordinal) ||
                IsGroupTable(tableName) ||
                IsUserTable(tableName) ||
                IsTemporaryGroupTable(tableName) ||
                IsTemporaryAllowTable(tableName) ||
                IsTemporaryDenyTable(tableName) ||
                IsBanTable(tableName);
        }

        public static bool IsGroupTable(string tableName)
        {
            return HasPrefix(tableName, GroupPrefix);
        }

        public static bool IsUserTable(string tableName)
        {
            return HasPrefix(tableName, UserPrefix);
        }

        public static bool IsTemporaryGroupTable(string tableName)
        {
            return HasPrefix(tableName, TemporaryGroupPrefix);
        }

        public static bool IsTemporaryAllowTable(string tableName)
        {
            return HasPrefix(tableName, TemporaryAllowPrefix);
        }

        public static bool IsTemporaryDenyTable(string tableName)
        {
            return HasPrefix(tableName, TemporaryDenyPrefix);
        }

        public static bool IsBanTable(string tableName)
        {
            return HasPrefix(tableName, BanPrefix);
        }

        public static string GetGroupName(string tableName)
        {
            return GetSuffix(tableName, GroupPrefix);
        }

        public static string GetUserId(string tableName)
        {
            return GetSuffix(tableName, UserPrefix);
        }

        public static string GetTemporaryAssignmentId(string tableName)
        {
            if (IsTemporaryGroupTable(tableName))
            {
                return GetSuffix(tableName, TemporaryGroupPrefix);
            }

            if (IsTemporaryAllowTable(tableName))
            {
                return GetSuffix(tableName, TemporaryAllowPrefix);
            }

            return GetSuffix(tableName, TemporaryDenyPrefix);
        }

        public static string GetBanSubjectId(string tableName)
        {
            return GetSuffix(tableName, BanPrefix);
        }

        public static string GroupTable(string groupName)
        {
            return GroupPrefix + groupName;
        }

        public static string UserTable(string userId)
        {
            return UserPrefix + userId;
        }

        public static string TemporaryGroupTable(string assignmentId)
        {
            return TemporaryGroupPrefix + assignmentId;
        }

        public static string TemporaryAllowTable(string assignmentId)
        {
            return TemporaryAllowPrefix + assignmentId;
        }

        public static string TemporaryDenyTable(string assignmentId)
        {
            return TemporaryDenyPrefix + assignmentId;
        }

        public static string BanTable(string subjectId)
        {
            return BanPrefix + subjectId;
        }

        private static bool HasPrefix(string value, string prefix)
        {
            return !string.IsNullOrWhiteSpace(value) && value.StartsWith(prefix, StringComparison.Ordinal);
        }

        private static string GetSuffix(string value, string prefix)
        {
            return HasPrefix(value, prefix)
                ? value.Substring(prefix.Length)
                : string.Empty;
        }
    }
}
