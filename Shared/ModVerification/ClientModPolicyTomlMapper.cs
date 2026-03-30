using DedicatedServerMod.API.Toml;

namespace DedicatedServerMod.Shared.ModVerification
{
    /// <summary>
    /// Maps <see cref="ClientModPolicy"/> to and from TOML documents.
    /// </summary>
    internal static class ClientModPolicyTomlMapper
    {
        private static readonly IReadOnlyList<string> FileHeaderComments = new[]
        {
            "DedicatedServerMod client mod policy",
            "Use this file for explicit deny rules and strict-mode hash overrides."
        };

        private const string PolicyTableName = "policy";
        private const string ApprovedUnpairedPrefix = "approvedUnpairedClientMods.";
        private const string StrictPinnedCompanionPrefix = "strictPinnedCompanionHashes.";

        private static class Keys
        {
            public const string DeniedClientModIds = "deniedClientModIds";
            public const string DeniedClientModNames = "deniedClientModNames";
            public const string DeniedClientModHashes = "deniedClientModHashes";
            public const string ModId = "modId";
            public const string DisplayName = "displayName";
            public const string PinnedSha256 = "pinnedSha256";
        }

        private static readonly IReadOnlyList<string> PolicyManagedKeys = new[]
        {
            Keys.DeniedClientModIds,
            Keys.DeniedClientModNames,
            Keys.DeniedClientModHashes
        };

        private static readonly IReadOnlyList<string> ApprovedEntryManagedKeys = new[]
        {
            Keys.ModId,
            Keys.DisplayName,
            Keys.PinnedSha256
        };

        private static readonly IReadOnlyList<string> StrictCompanionManagedKeys = new[]
        {
            Keys.ModId,
            Keys.PinnedSha256
        };

        public static ClientModPolicy Read(TomlDocument document, ICollection<TomlDiagnostic> diagnostics)
        {
            ClientModPolicy policy = new ClientModPolicy();
            if (document == null)
            {
                return policy;
            }

            TomlTable policyTable = document.GetTable(PolicyTableName);
            if (policyTable != null)
            {
                policy.DeniedClientModIds = ReadStringArray(policyTable, Keys.DeniedClientModIds);
                policy.DeniedClientModNames = ReadStringArray(policyTable, Keys.DeniedClientModNames);
                policy.DeniedClientModHashes = ReadStringArray(policyTable, Keys.DeniedClientModHashes);
            }

            foreach (TomlTable table in document.Tables)
            {
                string tableName = table.Name ?? string.Empty;
                if (string.Equals(tableName, PolicyTableName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (tableName.StartsWith(ApprovedUnpairedPrefix, StringComparison.Ordinal))
                {
                    ApprovedUnpairedClientModPolicyEntry entry = ReadApprovedUnpairedEntry(table);
                    if (entry != null)
                    {
                        policy.ApprovedUnpairedClientMods.Add(entry);
                    }

                    continue;
                }

                if (tableName.StartsWith(StrictPinnedCompanionPrefix, StringComparison.Ordinal))
                {
                    ReadStrictPinnedCompanionEntry(table, policy, diagnostics);
                }
            }

            policy.Normalize();
            return policy;
        }

        public static void Write(TomlDocument document, ClientModPolicy policy)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (policy == null)
            {
                throw new ArgumentNullException(nameof(policy));
            }

            policy.Normalize();
            ReplaceComments(document.FileHeaderComments, FileHeaderComments);

            List<ClientModPolicyTableDefinition> desiredTables = BuildDesiredTables(policy);
            HashSet<string> desiredTableNames = new HashSet<string>(desiredTables.Select(table => table.Name), StringComparer.Ordinal);
            document.RemoveTablesWhere(table => IsManagedTable(table.Name) && !desiredTableNames.Contains(table.Name));

            for (int index = 0; index < desiredTables.Count; index++)
            {
                ClientModPolicyTableDefinition desiredTable = desiredTables[index];
                TomlTable table = document.GetOrAddTable(desiredTable.Name);
                document.MoveTableToIndex(desiredTable.Name, index);
                ClearManagedKeys(table, desiredTable.ManagedKeys);
                desiredTable.Apply(table);
            }
        }

        private static List<ClientModPolicyTableDefinition> BuildDesiredTables(ClientModPolicy policy)
        {
            List<ClientModPolicyTableDefinition> tables = new List<ClientModPolicyTableDefinition>
            {
                new ClientModPolicyTableDefinition(
                    PolicyTableName,
                    PolicyManagedKeys,
                    table =>
                    {
                        table.Set(Keys.DeniedClientModIds, CreateStringArray(policy.DeniedClientModIds));
                        table.Set(Keys.DeniedClientModNames, CreateStringArray(policy.DeniedClientModNames));
                        table.Set(Keys.DeniedClientModHashes, CreateStringArray(policy.DeniedClientModHashes));
                    })
            };

            HashSet<string> usedApprovedNames = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < policy.ApprovedUnpairedClientMods.Count; index++)
            {
                ApprovedUnpairedClientModPolicyEntry entry = policy.ApprovedUnpairedClientMods[index];
                if (entry == null)
                {
                    continue;
                }

                string tableName = BuildUniqueTableName(
                    ApprovedUnpairedPrefix,
                    entry.ModId,
                    entry.DisplayName,
                    index,
                    usedApprovedNames);

                tables.Add(new ClientModPolicyTableDefinition(
                    tableName,
                    ApprovedEntryManagedKeys,
                    table =>
                    {
                        table.Set(Keys.ModId, TomlValue.FromString(entry.ModId ?? string.Empty));
                        table.Set(Keys.DisplayName, TomlValue.FromString(entry.DisplayName ?? string.Empty));
                        table.Set(Keys.PinnedSha256, CreateStringArray(entry.PinnedSha256));
                    }));
            }

            HashSet<string> usedStrictNames = new HashSet<string>(StringComparer.Ordinal);
            int strictIndex = 0;
            foreach (KeyValuePair<string, List<string>> pair in policy.StrictPinnedCompanionHashes.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
            {
                string modId = ClientModPolicy.NormalizeValue(pair.Key);
                if (string.IsNullOrEmpty(modId))
                {
                    continue;
                }

                string tableName = BuildUniqueTableName(
                    StrictPinnedCompanionPrefix,
                    modId,
                    modId,
                    strictIndex,
                    usedStrictNames);

                tables.Add(new ClientModPolicyTableDefinition(
                    tableName,
                    StrictCompanionManagedKeys,
                    table =>
                    {
                        table.Set(Keys.ModId, TomlValue.FromString(modId));
                        table.Set(Keys.PinnedSha256, CreateStringArray(pair.Value));
                    }));

                strictIndex++;
            }

            return tables;
        }

        private static ApprovedUnpairedClientModPolicyEntry ReadApprovedUnpairedEntry(TomlTable table)
        {
            if (table == null)
            {
                return null;
            }

            return new ApprovedUnpairedClientModPolicyEntry
            {
                ModId = table.TryGetString(Keys.ModId, out string modId) ? ClientModPolicy.NormalizeValue(modId) : string.Empty,
                DisplayName = table.TryGetString(Keys.DisplayName, out string displayName) ? ClientModPolicy.NormalizeValue(displayName) : string.Empty,
                PinnedSha256 = ReadStringArray(table, Keys.PinnedSha256)
            };
        }

        private static void ReadStrictPinnedCompanionEntry(TomlTable table, ClientModPolicy policy, ICollection<TomlDiagnostic> diagnostics)
        {
            if (table == null || policy == null)
            {
                return;
            }

            if (!table.TryGetString(Keys.ModId, out string modId))
            {
                diagnostics?.Add(new TomlDiagnostic(0, table.Name, Keys.ModId, "Strict companion hash entry is missing modId."));
                return;
            }

            string normalizedModId = ClientModPolicy.NormalizeValue(modId);
            if (string.IsNullOrEmpty(normalizedModId))
            {
                diagnostics?.Add(new TomlDiagnostic(0, table.Name, Keys.ModId, "Strict companion hash entry has an empty modId."));
                return;
            }

            policy.StrictPinnedCompanionHashes[normalizedModId] = ReadStringArray(table, Keys.PinnedSha256);
        }

        private static List<string> ReadStringArray(TomlTable table, string key)
        {
            if (table == null || !table.TryGetArray(key, out IReadOnlyList<TomlValue> values))
            {
                return new List<string>();
            }

            return values
                .Select(value => value.TryGetString(out string item) ? item : string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();
        }

        private static TomlValue CreateStringArray(IEnumerable<string> values)
        {
            IReadOnlyList<TomlValue> items = (values ?? Enumerable.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(TomlValue.FromString)
                .ToList();

            return TomlValue.FromArray(items);
        }

        private static string BuildUniqueTableName(
            string prefix,
            string preferredIdentifier,
            string fallbackIdentifier,
            int index,
            ISet<string> usedNames)
        {
            string baseIdentifier = SanitizeTableIdentifier(preferredIdentifier);
            if (string.IsNullOrEmpty(baseIdentifier))
            {
                baseIdentifier = SanitizeTableIdentifier(fallbackIdentifier);
            }

            if (string.IsNullOrEmpty(baseIdentifier))
            {
                baseIdentifier = $"entry{index + 1:D3}";
            }

            string tableName = prefix + baseIdentifier;
            if (usedNames.Add(tableName))
            {
                return tableName;
            }

            int suffix = 2;
            while (!usedNames.Add($"{tableName}-{suffix}"))
            {
                suffix++;
            }

            return $"{tableName}-{suffix}";
        }

        private static string SanitizeTableIdentifier(string value)
        {
            string normalized = ClientModPolicy.NormalizeValue(value);
            if (string.IsNullOrEmpty(normalized))
            {
                return string.Empty;
            }

            char[] characters = normalized
                .Select(character => char.IsLetterOrDigit(character) || character == '.' || character == '-' || character == '_'
                    ? character
                    : '-')
                .ToArray();

            string sanitized = new string(characters).Trim('-');
            while (sanitized.Contains("--", StringComparison.Ordinal))
            {
                sanitized = sanitized.Replace("--", "-", StringComparison.Ordinal);
            }

            return sanitized;
        }

        private static bool IsManagedTable(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                return false;
            }

            return string.Equals(tableName, PolicyTableName, StringComparison.Ordinal) ||
                   tableName.StartsWith(ApprovedUnpairedPrefix, StringComparison.Ordinal) ||
                   tableName.StartsWith(StrictPinnedCompanionPrefix, StringComparison.Ordinal);
        }

        private static void ClearManagedKeys(TomlTable table, IEnumerable<string> managedKeys)
        {
            foreach (string key in managedKeys ?? Array.Empty<string>())
            {
                table.Remove(key);
            }
        }

        private static void ReplaceComments(IList<string> destination, IEnumerable<string> source)
        {
            destination.Clear();
            foreach (string line in source ?? Array.Empty<string>())
            {
                destination.Add(line ?? string.Empty);
            }
        }

        private sealed class ClientModPolicyTableDefinition(
            string name,
            IReadOnlyList<string> managedKeys,
            Action<TomlTable> apply)
        {
            public string Name { get; } = name;

            public IReadOnlyList<string> ManagedKeys { get; } = managedKeys ?? Array.Empty<string>();

            public Action<TomlTable> Apply { get; } = apply ?? throw new ArgumentNullException(nameof(apply));
        }
    }
}
