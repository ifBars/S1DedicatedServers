using DedicatedServerMod.Shared.Permissions;

namespace DedicatedServerMod.Server.Permissions
{
    /// <summary>
    /// Provides built-in permission definitions and seeded group topology.
    /// </summary>
    internal static class PermissionDefaults
    {
        private static readonly IReadOnlyDictionary<string, BuiltInGroupTemplate> BuiltInGroups =
            new Dictionary<string, BuiltInGroupTemplate>(StringComparer.OrdinalIgnoreCase)
            {
                [PermissionBuiltIns.Groups.Default] = new BuiltInGroupTemplate(
                    PermissionBuiltIns.Groups.Default,
                    priority: 0,
                    inherits: Array.Empty<string>(),
                    allow: new[]
                    {
                        PermissionBuiltIns.Nodes.ServerHelp
                    }),
                [PermissionBuiltIns.Groups.Support] = new BuiltInGroupTemplate(
                    PermissionBuiltIns.Groups.Support,
                    priority: 10,
                    inherits: new[]
                    {
                        PermissionBuiltIns.Groups.Default
                    },
                    allow: new[]
                    {
                        PermissionBuiltIns.Nodes.ServerInfo
                    }),
                [PermissionBuiltIns.Groups.Moderator] = new BuiltInGroupTemplate(
                    PermissionBuiltIns.Groups.Moderator,
                    priority: 20,
                    inherits: new[]
                    {
                        PermissionBuiltIns.Groups.Support
                    },
                    allow: new[]
                    {
                        PermissionBuiltIns.Nodes.PlayerList,
                        PermissionBuiltIns.Nodes.PlayerKick,
                        PermissionBuiltIns.Nodes.PlayerBan,
                        PermissionBuiltIns.Nodes.PlayerUnban
                    }),
                [PermissionBuiltIns.Groups.Administrator] = new BuiltInGroupTemplate(
                    PermissionBuiltIns.Groups.Administrator,
                    priority: 30,
                    inherits: new[]
                    {
                        PermissionBuiltIns.Groups.Moderator
                    },
                    allow: new[]
                    {
                        PermissionBuiltIns.Nodes.ServerSave,
                        PermissionBuiltIns.Nodes.ServerReloadConfig,
                        PermissionBuiltIns.Nodes.PermissionsInfo,
                        PermissionBuiltIns.Nodes.PermissionsGroupList
                    }),
                [PermissionBuiltIns.Groups.Operator] = new BuiltInGroupTemplate(
                    PermissionBuiltIns.Groups.Operator,
                    priority: 40,
                    inherits: new[]
                    {
                        PermissionBuiltIns.Groups.Administrator
                    },
                    allow: new[]
                    {
                        PermissionBuiltIns.Nodes.ClientModPolicyBypass,
                        PermissionBuiltIns.Nodes.ServerStop,
                        PermissionBuiltIns.Nodes.PermissionsReload,
                        PermissionBuiltIns.Nodes.PermissionsGrant,
                        PermissionBuiltIns.Nodes.PermissionsDeny,
                        PermissionBuiltIns.Nodes.PermissionsRevoke,
                        PermissionBuiltIns.Nodes.PermissionsTempGrant,
                        PermissionBuiltIns.Nodes.PermissionsGroupAssign,
                        PermissionBuiltIns.Nodes.PermissionsGroupUnassign
                    })
            };

        /// <summary>
        /// Creates the default permission store data.
        /// </summary>
        /// <param name="migratedFrom">The source description for migration metadata.</param>
        /// <returns>A seeded permissions data graph.</returns>
        public static PermissionStoreData CreateSeedData(string migratedFrom)
        {
            PermissionStoreData data = new PermissionStoreData
            {
                SchemaVersion = 1,
                MigrationVersion = 1,
                MigratedFrom = migratedFrom ?? string.Empty,
                MigratedAtUtc = DateTime.UtcNow
            };

            ApplyBuiltInGroups(data);
            return data;
        }

        /// <summary>
        /// Ensures the built-in groups exist in the target data graph.
        /// </summary>
        /// <param name="data">The target permission data.</param>
        public static void ApplyBuiltInGroups(PermissionStoreData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            data.Groups ??= new Dictionary<string, PermissionGroupDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (BuiltInGroupTemplate template in BuiltInGroups.Values)
            {
                string normalizedGroupName = PermissionNode.NormalizeGroupName(template.Name);
                if (!data.Groups.TryGetValue(normalizedGroupName, out PermissionGroupDefinition group))
                {
                    group = new PermissionGroupDefinition
                    {
                        Name = normalizedGroupName
                    };
                    data.Groups[normalizedGroupName] = group;
                }

                group.Name = normalizedGroupName;
                group.Priority = template.Priority;
                group.Inherits = MergeNormalizedStrings(group.Inherits, template.Inherits, isGroupName: true);
                group.Allow = MergeNormalizedStrings(group.Allow, template.Allow, isGroupName: false);
                group.Deny = MergeNormalizedStrings(group.Deny, Array.Empty<string>(), isGroupName: false);
            }
        }

        /// <summary>
        /// Gets the built-in priority for a group.
        /// </summary>
        /// <param name="groupName">The group name.</param>
        /// <returns>The built-in priority, or 0 when not built-in.</returns>
        public static int GetBuiltInGroupPriority(string groupName)
        {
            string normalizedGroupName = PermissionNode.NormalizeGroupName(groupName);
            return BuiltInGroups.TryGetValue(normalizedGroupName, out BuiltInGroupTemplate template)
                ? template.Priority
                : 0;
        }

        /// <summary>
        /// Gets the built-in permission node definitions.
        /// </summary>
        /// <returns>The built-in permission definitions.</returns>
        public static IEnumerable<PermissionDefinition> GetBuiltInDefinitions()
        {
            return new[]
            {
                CreateDefinition(PermissionBuiltIns.Nodes.ConsoleOpen, "Console", "Open the remote admin console."),
                CreateDefinition(PermissionBuiltIns.Nodes.ConsoleCommandWildcard, "Console", "Execute any relayed remote console command."),
                CreateDefinition(PermissionBuiltIns.Nodes.ClientModPolicyBypass, "Client Mods", "Bypass client mod policy verification."),
                CreateDefinition(PermissionBuiltIns.Nodes.PlayerList, "Players", "List connected players."),
                CreateDefinition(PermissionBuiltIns.Nodes.PlayerKick, "Players", "Kick connected players."),
                CreateDefinition(PermissionBuiltIns.Nodes.PlayerBan, "Players", "Ban players from the server."),
                CreateDefinition(PermissionBuiltIns.Nodes.PlayerUnban, "Players", "Remove bans from players."),
                CreateDefinition(PermissionBuiltIns.Nodes.ServerHelp, "Server", "View help for built-in server commands."),
                CreateDefinition(PermissionBuiltIns.Nodes.ServerInfo, "Server", "View dedicated server status information."),
                CreateDefinition(PermissionBuiltIns.Nodes.ServerSave, "Server", "Trigger a server save."),
                CreateDefinition(PermissionBuiltIns.Nodes.ServerReloadConfig, "Server", "Reload server configuration from disk."),
                CreateDefinition(PermissionBuiltIns.Nodes.ServerStop, "Server", "Stop the dedicated server."),
                CreateDefinition(PermissionBuiltIns.Nodes.PermissionsReload, "Permissions", "Reload permissions from disk."),
                CreateDefinition(PermissionBuiltIns.Nodes.PermissionsInfo, "Permissions", "Inspect permission assignments."),
                CreateDefinition(PermissionBuiltIns.Nodes.PermissionsGrant, "Permissions", "Grant a direct permission node."),
                CreateDefinition(PermissionBuiltIns.Nodes.PermissionsDeny, "Permissions", "Deny a direct permission node."),
                CreateDefinition(PermissionBuiltIns.Nodes.PermissionsRevoke, "Permissions", "Revoke a direct permission node."),
                CreateDefinition(PermissionBuiltIns.Nodes.PermissionsTempGrant, "Permissions", "Grant a temporary permission node."),
                CreateDefinition(PermissionBuiltIns.Nodes.PermissionsGroupList, "Permissions", "List permission groups."),
                CreateDefinition(PermissionBuiltIns.Nodes.PermissionsGroupAssign, "Permissions", "Assign groups to users."),
                CreateDefinition(PermissionBuiltIns.Nodes.PermissionsGroupUnassign, "Permissions", "Remove groups from users.")
            };
        }

        private static PermissionDefinition CreateDefinition(string node, string category, string description)
        {
            return new PermissionDefinition
            {
                Node = node,
                Category = category,
                Description = description
            };
        }

        private static List<string> MergeNormalizedStrings(IEnumerable<string> first, IEnumerable<string> second, bool isGroupName)
        {
            IEnumerable<string> values = (first ?? Enumerable.Empty<string>())
                .Concat(second ?? Enumerable.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => isGroupName ? PermissionNode.NormalizeGroupName(value) : PermissionNode.Normalize(value))
                .Where(value => !string.IsNullOrWhiteSpace(value));

            return values
                .Distinct(isGroupName ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
        }

        private sealed class BuiltInGroupTemplate(
            string name,
            int priority,
            IReadOnlyList<string> inherits,
            IReadOnlyList<string> allow)
        {
            public string Name { get; } = name;

            public int Priority { get; } = priority;

            public IReadOnlyList<string> Inherits { get; } = inherits ?? Array.Empty<string>();

            public IReadOnlyList<string> Allow { get; } = allow ?? Array.Empty<string>();
        }
    }
}
