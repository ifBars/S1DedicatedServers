using DedicatedServerMod.API.Toml;
using DedicatedServerMod.Shared.Configuration;
using DedicatedServerMod.Shared.Permissions;
using DedicatedServerMod.Utils;
using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json.Linq;

namespace DedicatedServerMod.Server.Permissions
{
    /// <summary>
    /// Coordinates first-run migration from legacy configuration-based permissions.
    /// </summary>
    internal sealed class PermissionMigrationCoordinator
    {
        private static readonly string[] LegacyConfigPermissionKeys =
        {
            Constants.ConfigKeys.Operators,
            Constants.ConfigKeys.Admins,
            Constants.ConfigKeys.BannedPlayers,
            "enableConsoleForOps",
            "enableConsoleForAdmins",
            "enableConsoleForPlayers",
            "allowedCommands",
            "restrictedCommands",
            "playerAllowedCommands",
            "globalDisabledCommands"
        };

        private readonly MelonLogger.Instance _logger;
        private readonly PermissionStore _store;

        /// <summary>
        /// Initializes a new migration coordinator.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        /// <param name="store">The permission store to populate.</param>
        public PermissionMigrationCoordinator(MelonLogger.Instance logger, PermissionStore store)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <summary>
        /// Loads the current permissions file or creates it from legacy config sources.
        /// </summary>
        /// <returns>The active permission store data.</returns>
        public PermissionStoreData LoadOrCreate()
        {
            if (_store.Exists)
            {
                return _store.Load();
            }

            string configPath = ServerConfig.ConfigFilePath;
            string legacyJsonPath = Path.Combine(MelonEnvironment.UserDataDirectory, Constants.LegacyConfigFileName);

            PermissionStoreData migratedData;
            if (HasLegacyPermissionFields(configPath))
            {
                _logger.Warning($"Legacy permission fields detected in '{configPath}'. Migrating them into '{_store.FilePath}'. Future permission changes must be made in the dedicated permissions file.");
                migratedData = ImportFromLegacyConfig(ServerConfig.LoadConfigSnapshot(configPath), Path.GetFileName(configPath));
            }
            else if (HasLegacyPermissionFields(legacyJsonPath))
            {
                _logger.Warning($"Legacy permission fields detected in '{legacyJsonPath}'. Migrating them into '{_store.FilePath}'. Future permission changes must be made in the dedicated permissions file.");
                migratedData = ImportFromLegacyConfig(ServerConfig.LoadConfigSnapshot(legacyJsonPath), Path.GetFileName(legacyJsonPath));
            }
            else
            {
                migratedData = PermissionDefaults.CreateSeedData("default");
            }

            _store.Save(migratedData);
            return migratedData;
        }

        private static PermissionStoreData ImportFromLegacyConfig(ServerConfig config, string sourceLabel)
        {
            PermissionStoreData data = PermissionDefaults.CreateSeedData(sourceLabel);
            PermissionGroupDefinition defaultGroup = data.Groups[PermissionBuiltIns.Groups.Default];
            PermissionGroupDefinition administratorGroup = data.Groups[PermissionBuiltIns.Groups.Administrator];
            PermissionGroupDefinition operatorGroup = data.Groups[PermissionBuiltIns.Groups.Operator];

            if (config.EnableConsoleForPlayers)
            {
                AddNode(defaultGroup.Allow, PermissionBuiltIns.Nodes.ConsoleOpen);
            }

            if (config.EnableConsoleForAdmins)
            {
                AddNode(administratorGroup.Allow, PermissionBuiltIns.Nodes.ConsoleOpen);
            }

            if (config.EnableConsoleForOps)
            {
                AddNode(operatorGroup.Allow, PermissionBuiltIns.Nodes.ConsoleOpen);
            }

            foreach (string command in config.PlayerAllowedCommands)
            {
                AddNode(defaultGroup.Allow, PermissionNode.CreateConsoleCommandNode(command));
            }

            foreach (string command in config.AllowedCommands)
            {
                AddNode(administratorGroup.Allow, PermissionNode.CreateConsoleCommandNode(command));
            }

            foreach (string command in config.RestrictedCommands)
            {
                AddNode(operatorGroup.Allow, PermissionNode.CreateConsoleCommandNode(command));
            }

            foreach (string command in config.GlobalDisabledCommands)
            {
                string commandNode = PermissionNode.CreateConsoleCommandNode(command);
                AddNode(defaultGroup.Deny, commandNode);
                AddNode(data.Groups[PermissionBuiltIns.Groups.Support].Deny, commandNode);
                AddNode(data.Groups[PermissionBuiltIns.Groups.Moderator].Deny, commandNode);
                AddNode(administratorGroup.Deny, commandNode);
                AddNode(operatorGroup.Deny, commandNode);
            }

            foreach (string userId in config.Admins)
            {
                AddUserGroup(data, userId, PermissionBuiltIns.Groups.Administrator);
            }

            foreach (string userId in config.Operators)
            {
                AddUserGroup(data, userId, PermissionBuiltIns.Groups.Operator);
            }

            foreach (string userId in config.BannedPlayers)
            {
                string normalizedUserId = NormalizeUserId(userId);
                if (string.IsNullOrEmpty(normalizedUserId))
                {
                    continue;
                }

                data.Bans[normalizedUserId] = new BanEntry
                {
                    SubjectId = normalizedUserId,
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedBy = "legacy-config",
                    Reason = "Migrated from legacy configuration"
                };
            }

            data.MigrationVersion = 1;
            data.MigratedAtUtc = DateTime.UtcNow;
            return data;
        }

        private static void AddUserGroup(PermissionStoreData data, string userId, string groupName)
        {
            string normalizedUserId = NormalizeUserId(userId);
            if (string.IsNullOrEmpty(normalizedUserId))
            {
                return;
            }

            if (!data.Users.TryGetValue(normalizedUserId, out PermissionUserRecord user))
            {
                user = new PermissionUserRecord
                {
                    UserId = normalizedUserId
                };
                data.Users[normalizedUserId] = user;
            }

            string normalizedGroupName = PermissionNode.NormalizeGroupName(groupName);
            if (!user.Groups.Contains(normalizedGroupName, StringComparer.OrdinalIgnoreCase))
            {
                user.Groups.Add(normalizedGroupName);
            }
        }

        private static void AddNode(ICollection<string> target, string node)
        {
            string normalizedNode = PermissionNode.Normalize(node);
            if (string.IsNullOrEmpty(normalizedNode))
            {
                return;
            }

            if (!target.Contains(normalizedNode))
            {
                target.Add(normalizedNode);
            }
        }

        private static string NormalizeUserId(string userId)
        {
            return string.IsNullOrWhiteSpace(userId)
                ? string.Empty
                : userId.Trim();
        }

        private static bool HasLegacyPermissionFields(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            return string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase)
                ? JsonContainsLegacyPermissionFields(path)
                : TomlContainsLegacyPermissionFields(path);
        }

        private static bool JsonContainsLegacyPermissionFields(string path)
        {
            JToken rootToken;
            try
            {
                rootToken = JToken.Parse(File.ReadAllText(path));
            }
            catch
            {
                return false;
            }

            if (!(rootToken is JObject rootObject))
            {
                return false;
            }

            return LegacyConfigPermissionKeys.Any(key => rootObject.Property(key, StringComparison.OrdinalIgnoreCase) != null);
        }

        private static bool TomlContainsLegacyPermissionFields(string path)
        {
            TomlReadResult readResult;
            try
            {
                readResult = TomlParser.ParseFile(path);
            }
            catch
            {
                return false;
            }

            foreach (string key in LegacyConfigPermissionKeys)
            {
                if (ContainsTomlKey(readResult.Document.Root, key))
                {
                    return true;
                }
            }

            TomlTable permissionsTable = readResult.Document.GetTable("permissions");
            if (permissionsTable == null)
            {
                return false;
            }

            return LegacyConfigPermissionKeys.Any(key => ContainsTomlKey(permissionsTable, key));
        }

        private static bool ContainsTomlKey(TomlTable table, string key)
        {
            return table != null && !string.IsNullOrWhiteSpace(key) && table.ContainsKey(key);
        }
    }
}
