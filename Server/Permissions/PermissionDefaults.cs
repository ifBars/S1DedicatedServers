using System;
using System.Collections.Generic;
using DedicatedServerMod.Shared.Permissions;

namespace DedicatedServerMod.Server.Permissions
{
    /// <summary>
    /// Provides built-in permission definitions and seeded group topology.
    /// </summary>
    internal static class PermissionDefaults
    {
        private const int DefaultPriority = 0;
        private const int SupportPriority = 10;
        private const int ModeratorPriority = 20;
        private const int AdministratorPriority = 30;
        private const int OperatorPriority = 40;

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

            data.Groups[PermissionBuiltIns.Groups.Default] = new PermissionGroupDefinition
            {
                Name = PermissionBuiltIns.Groups.Default,
                Priority = DefaultPriority,
                Allow = new List<string>
                {
                    PermissionBuiltIns.Nodes.ServerHelp
                }
            };

            data.Groups[PermissionBuiltIns.Groups.Support] = new PermissionGroupDefinition
            {
                Name = PermissionBuiltIns.Groups.Support,
                Priority = SupportPriority,
                Inherits = new List<string>
                {
                    PermissionBuiltIns.Groups.Default
                },
                Allow = new List<string>
                {
                    PermissionBuiltIns.Nodes.ServerInfo
                }
            };

            data.Groups[PermissionBuiltIns.Groups.Moderator] = new PermissionGroupDefinition
            {
                Name = PermissionBuiltIns.Groups.Moderator,
                Priority = ModeratorPriority,
                Inherits = new List<string>
                {
                    PermissionBuiltIns.Groups.Support
                },
                Allow = new List<string>
                {
                    PermissionBuiltIns.Nodes.PlayerList,
                    PermissionBuiltIns.Nodes.PlayerKick
                }
            };

            data.Groups[PermissionBuiltIns.Groups.Administrator] = new PermissionGroupDefinition
            {
                Name = PermissionBuiltIns.Groups.Administrator,
                Priority = AdministratorPriority,
                Inherits = new List<string>
                {
                    PermissionBuiltIns.Groups.Moderator
                },
                Allow = new List<string>
                {
                    PermissionBuiltIns.Nodes.PlayerBan,
                    PermissionBuiltIns.Nodes.PlayerUnban,
                    PermissionBuiltIns.Nodes.ServerReloadConfig,
                    PermissionBuiltIns.Nodes.PermissionsInfo,
                    PermissionBuiltIns.Nodes.PermissionsGroupList
                }
            };

            data.Groups[PermissionBuiltIns.Groups.Operator] = new PermissionGroupDefinition
            {
                Name = PermissionBuiltIns.Groups.Operator,
                Priority = OperatorPriority,
                Inherits = new List<string>
                {
                    PermissionBuiltIns.Groups.Administrator
                },
                Allow = new List<string>
                {
                    PermissionBuiltIns.Nodes.All
                }
            };

            return data;
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
    }
}
