using System.Globalization;
using DedicatedServerMod.API.Toml;
using DedicatedServerMod.Shared.Permissions;

namespace DedicatedServerMod.Server.Permissions
{
    /// <summary>
    /// Maps <see cref="PermissionStoreData"/> to and from TOML documents.
    /// </summary>
    internal static class PermissionTomlMapper
    {
        private static readonly IReadOnlyList<string> FileHeaderComments = new[]
        {
            "DedicatedServerMod permissions file"
        };

        public static PermissionStoreData Read(TomlDocument document, ICollection<TomlDiagnostic> diagnostics)
        {
            PermissionStoreData data = new PermissionStoreData();
            if (document == null)
            {
                return data;
            }

            TomlTable metadata = document.GetTable(PermissionTomlLayout.MetadataTable);
            if (metadata != null)
            {
                if (metadata.TryGetInt64(PermissionTomlLayout.Keys.SchemaVersion, out long schemaVersion))
                {
                    data.SchemaVersion = (int)schemaVersion;
                }

                if (metadata.TryGetInt64(PermissionTomlLayout.Keys.MigrationVersion, out long migrationVersion))
                {
                    data.MigrationVersion = (int)migrationVersion;
                }

                metadata.TryGetString(PermissionTomlLayout.Keys.MigratedFrom, out string migratedFrom);
                data.MigratedFrom = migratedFrom ?? string.Empty;

                if (metadata.TryGetString(PermissionTomlLayout.Keys.MigratedAtUtc, out string migratedAtUtc) &&
                    TryParseUtc(migratedAtUtc, out DateTime parsedMigratedAt))
                {
                    data.MigratedAtUtc = parsedMigratedAt;
                }
            }

            foreach (TomlTable table in document.Tables)
            {
                string tableName = table.Name ?? string.Empty;
                if (string.Equals(tableName, PermissionTomlLayout.MetadataTable, StringComparison.Ordinal))
                {
                    continue;
                }

                if (PermissionTomlLayout.IsGroupTable(tableName))
                {
                    PermissionGroupDefinition group = ReadGroup(table);
                    if (!string.IsNullOrWhiteSpace(group.Name))
                    {
                        data.Groups[group.Name] = group;
                    }

                    continue;
                }

                if (PermissionTomlLayout.IsUserTable(tableName))
                {
                    PermissionUserRecord user = ReadUser(table);
                    if (!string.IsNullOrWhiteSpace(user.UserId))
                    {
                        MergeUser(data, user);
                    }

                    continue;
                }

                if (PermissionTomlLayout.IsTemporaryGroupTable(tableName))
                {
                    ReadTemporaryGroup(table, data, diagnostics);
                    continue;
                }

                if (PermissionTomlLayout.IsTemporaryAllowTable(tableName))
                {
                    ReadTemporaryNode(table, data, diagnostics, isAllow: true);
                    continue;
                }

                if (PermissionTomlLayout.IsTemporaryDenyTable(tableName))
                {
                    ReadTemporaryNode(table, data, diagnostics, isAllow: false);
                    continue;
                }

                if (PermissionTomlLayout.IsBanTable(tableName))
                {
                    BanEntry ban = ReadBan(table, diagnostics);
                    if (!string.IsNullOrWhiteSpace(ban.SubjectId))
                    {
                        data.Bans[ban.SubjectId] = ban;
                    }
                }
            }

            return data;
        }

        public static void Write(TomlDocument document, PermissionStoreData data)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            ReplaceComments(document.FileHeaderComments, FileHeaderComments);

            List<PermissionTableDefinition> desiredTables = BuildDesiredTables(data);
            HashSet<string> desiredTableNames = new HashSet<string>(desiredTables.Select(table => table.Name), StringComparer.Ordinal);
            document.RemoveTablesWhere(table => PermissionTomlLayout.IsManagedTable(table.Name) && !desiredTableNames.Contains(table.Name));

            for (int index = 0; index < desiredTables.Count; index++)
            {
                PermissionTableDefinition desiredTable = desiredTables[index];
                TomlTable table = document.GetOrAddTable(desiredTable.Name);
                document.MoveTableToIndex(desiredTable.Name, index);
                ClearManagedKeys(table, desiredTable.ManagedKeys);
                desiredTable.Apply(table);
            }
        }

        private static PermissionGroupDefinition ReadGroup(TomlTable table)
        {
            PermissionGroupDefinition group = new PermissionGroupDefinition
            {
                Name = PermissionNode.NormalizeGroupName(PermissionTomlLayout.GetGroupName(table.Name))
            };

            if (table.TryGetInt64(PermissionTomlLayout.Keys.Priority, out long priority))
            {
                group.Priority = (int)priority;
            }

            group.Inherits = ReadStringArray(table, PermissionTomlLayout.Keys.Inherits, normalizeAsGroups: true);
            group.Allow = ReadStringArray(table, PermissionTomlLayout.Keys.Allow, normalizeAsGroups: false);
            group.Deny = ReadStringArray(table, PermissionTomlLayout.Keys.Deny, normalizeAsGroups: false);
            return group;
        }

        private static PermissionUserRecord ReadUser(TomlTable table)
        {
            return new PermissionUserRecord
            {
                UserId = NormalizeSubjectId(PermissionTomlLayout.GetUserId(table.Name)),
                Groups = ReadStringArray(table, PermissionTomlLayout.Keys.Groups, normalizeAsGroups: true),
                Allow = ReadStringArray(table, PermissionTomlLayout.Keys.Allow, normalizeAsGroups: false),
                Deny = ReadStringArray(table, PermissionTomlLayout.Keys.Deny, normalizeAsGroups: false)
            };
        }

        private static void ReadTemporaryGroup(TomlTable table, PermissionStoreData data, ICollection<TomlDiagnostic> diagnostics)
        {
            if (!table.TryGetString(PermissionTomlLayout.Keys.UserId, out string userId))
            {
                diagnostics?.Add(new TomlDiagnostic(0, table.Name, PermissionTomlLayout.Keys.UserId, "Temporary group entry is missing userId."));
                return;
            }

            if (!table.TryGetString(PermissionTomlLayout.Keys.ExpiresAtUtc, out string expiresAtRaw) || !TryParseUtc(expiresAtRaw, out DateTime expiresAtUtc))
            {
                diagnostics?.Add(new TomlDiagnostic(0, table.Name, PermissionTomlLayout.Keys.ExpiresAtUtc, "Temporary group entry has an invalid expiresAtUtc value."));
                return;
            }

            PermissionUserRecord user = GetOrAddUser(data, userId);
            user.TemporaryGroups.Add(new TemporaryGroupAssignment
            {
                Id = PermissionTomlLayout.GetTemporaryAssignmentId(table.Name),
                GroupName = table.TryGetString(PermissionTomlLayout.Keys.Group, out string groupName)
                    ? PermissionNode.NormalizeGroupName(groupName)
                    : string.Empty,
                ExpiresAtUtc = expiresAtUtc,
                GrantedBy = table.TryGetString(PermissionTomlLayout.Keys.GrantedBy, out string grantedBy) ? grantedBy ?? string.Empty : string.Empty,
                Reason = table.TryGetString(PermissionTomlLayout.Keys.Reason, out string reason) ? reason ?? string.Empty : string.Empty
            });
        }

        private static void ReadTemporaryNode(TomlTable table, PermissionStoreData data, ICollection<TomlDiagnostic> diagnostics, bool isAllow)
        {
            if (!table.TryGetString(PermissionTomlLayout.Keys.UserId, out string userId))
            {
                diagnostics?.Add(new TomlDiagnostic(0, table.Name, PermissionTomlLayout.Keys.UserId, "Temporary permission entry is missing userId."));
                return;
            }

            if (!table.TryGetString(PermissionTomlLayout.Keys.ExpiresAtUtc, out string expiresAtRaw) || !TryParseUtc(expiresAtRaw, out DateTime expiresAtUtc))
            {
                diagnostics?.Add(new TomlDiagnostic(0, table.Name, PermissionTomlLayout.Keys.ExpiresAtUtc, "Temporary permission entry has an invalid expiresAtUtc value."));
                return;
            }

            PermissionUserRecord user = GetOrAddUser(data, userId);
            TemporaryPermissionGrant grant = new TemporaryPermissionGrant
            {
                Id = PermissionTomlLayout.GetTemporaryAssignmentId(table.Name),
                Node = table.TryGetString(PermissionTomlLayout.Keys.Node, out string node) ? PermissionNode.Normalize(node) : string.Empty,
                ExpiresAtUtc = expiresAtUtc,
                GrantedBy = table.TryGetString(PermissionTomlLayout.Keys.GrantedBy, out string grantedBy) ? grantedBy ?? string.Empty : string.Empty,
                Reason = table.TryGetString(PermissionTomlLayout.Keys.Reason, out string reason) ? reason ?? string.Empty : string.Empty
            };

            if (isAllow)
            {
                user.TemporaryAllow.Add(grant);
            }
            else
            {
                user.TemporaryDeny.Add(grant);
            }
        }

        private static BanEntry ReadBan(TomlTable table, ICollection<TomlDiagnostic> diagnostics)
        {
            string subjectId = table.TryGetString(PermissionTomlLayout.Keys.SubjectId, out string explicitSubjectId)
                ? explicitSubjectId
                : PermissionTomlLayout.GetBanSubjectId(table.Name);

            BanEntry entry = new BanEntry
            {
                SubjectId = NormalizeSubjectId(subjectId),
                CreatedBy = table.TryGetString(PermissionTomlLayout.Keys.CreatedBy, out string createdBy) ? createdBy ?? string.Empty : string.Empty,
                Reason = table.TryGetString(PermissionTomlLayout.Keys.Reason, out string reason) ? reason ?? string.Empty : string.Empty
            };

            if (table.TryGetString(PermissionTomlLayout.Keys.CreatedAtUtc, out string createdAtRaw) && TryParseUtc(createdAtRaw, out DateTime createdAtUtc))
            {
                entry.CreatedAtUtc = createdAtUtc;
            }
            else
            {
                diagnostics?.Add(new TomlDiagnostic(0, table.Name, PermissionTomlLayout.Keys.CreatedAtUtc, "Ban entry has an invalid createdAtUtc value."));
            }

            return entry;
        }

        private static List<PermissionTableDefinition> BuildDesiredTables(PermissionStoreData data)
        {
            List<PermissionTableDefinition> tables = new List<PermissionTableDefinition>
            {
                new PermissionTableDefinition(
                    PermissionTomlLayout.MetadataTable,
                    PermissionTomlLayout.MetadataKeys,
                    table =>
                    {
                        table.Set(PermissionTomlLayout.Keys.SchemaVersion, TomlValue.FromInteger(data.SchemaVersion));
                        table.Set(PermissionTomlLayout.Keys.MigrationVersion, TomlValue.FromInteger(data.MigrationVersion));
                        table.Set(PermissionTomlLayout.Keys.MigratedFrom, TomlValue.FromString(data.MigratedFrom ?? string.Empty));
                        table.Set(PermissionTomlLayout.Keys.MigratedAtUtc, TomlValue.FromString(data.MigratedAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty));
                    })
            };

            foreach (PermissionGroupDefinition group in (data.Groups ?? new Dictionary<string, PermissionGroupDefinition>())
                .Values
                .OrderBy(group => group.Priority)
                .ThenBy(group => group.Name, StringComparer.Ordinal))
            {
                string tableName = PermissionTomlLayout.GroupTable(PermissionNode.NormalizeGroupName(group.Name));
                tables.Add(new PermissionTableDefinition(
                    tableName,
                    PermissionTomlLayout.GroupKeys,
                    table =>
                    {
                        table.Set(PermissionTomlLayout.Keys.Priority, TomlValue.FromInteger(group.Priority));
                        table.Set(PermissionTomlLayout.Keys.Inherits, CreateStringArray(group.Inherits, normalizeAsGroups: true));
                        table.Set(PermissionTomlLayout.Keys.Allow, CreateStringArray(group.Allow, normalizeAsGroups: false));
                        table.Set(PermissionTomlLayout.Keys.Deny, CreateStringArray(group.Deny, normalizeAsGroups: false));
                    }));
            }

            foreach (PermissionUserRecord user in (data.Users ?? new Dictionary<string, PermissionUserRecord>())
                .Values
                .OrderBy(user => user.UserId, StringComparer.Ordinal))
            {
                string userId = NormalizeSubjectId(user.UserId);
                tables.Add(new PermissionTableDefinition(
                    PermissionTomlLayout.UserTable(userId),
                    PermissionTomlLayout.UserKeys,
                    table =>
                    {
                        table.Set(PermissionTomlLayout.Keys.Groups, CreateStringArray(user.Groups, normalizeAsGroups: true));
                        table.Set(PermissionTomlLayout.Keys.Allow, CreateStringArray(user.Allow, normalizeAsGroups: false));
                        table.Set(PermissionTomlLayout.Keys.Deny, CreateStringArray(user.Deny, normalizeAsGroups: false));
                    }));

                foreach (TemporaryGroupAssignment assignment in user.TemporaryGroups.OrderBy(item => item.Id, StringComparer.Ordinal))
                {
                    tables.Add(new PermissionTableDefinition(
                        PermissionTomlLayout.TemporaryGroupTable(assignment.Id),
                        PermissionTomlLayout.TemporaryGroupKeys,
                        table =>
                        {
                            table.Set(PermissionTomlLayout.Keys.UserId, TomlValue.FromString(userId));
                            table.Set(PermissionTomlLayout.Keys.Group, TomlValue.FromString(PermissionNode.NormalizeGroupName(assignment.GroupName)));
                            table.Set(PermissionTomlLayout.Keys.ExpiresAtUtc, TomlValue.FromString(assignment.ExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture)));
                            table.Set(PermissionTomlLayout.Keys.GrantedBy, TomlValue.FromString(assignment.GrantedBy ?? string.Empty));
                            table.Set(PermissionTomlLayout.Keys.Reason, TomlValue.FromString(assignment.Reason ?? string.Empty));
                        }));
                }

                AddTemporaryNodeTables(tables, userId, user.TemporaryAllow, PermissionTomlLayout.TemporaryAllowTable);
                AddTemporaryNodeTables(tables, userId, user.TemporaryDeny, PermissionTomlLayout.TemporaryDenyTable);
            }

            foreach (BanEntry ban in (data.Bans ?? new Dictionary<string, BanEntry>())
                .Values
                .OrderBy(ban => ban.SubjectId, StringComparer.Ordinal))
            {
                string subjectId = NormalizeSubjectId(ban.SubjectId);
                tables.Add(new PermissionTableDefinition(
                    PermissionTomlLayout.BanTable(subjectId),
                    PermissionTomlLayout.BanKeys,
                    table =>
                    {
                        table.Set(PermissionTomlLayout.Keys.SubjectId, TomlValue.FromString(subjectId));
                        table.Set(PermissionTomlLayout.Keys.CreatedAtUtc, TomlValue.FromString(ban.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture)));
                        table.Set(PermissionTomlLayout.Keys.CreatedBy, TomlValue.FromString(ban.CreatedBy ?? string.Empty));
                        table.Set(PermissionTomlLayout.Keys.Reason, TomlValue.FromString(ban.Reason ?? string.Empty));
                    }));
            }

            return tables;
        }

        private static void AddTemporaryNodeTables(
            ICollection<PermissionTableDefinition> tables,
            string userId,
            IEnumerable<TemporaryPermissionGrant> grants,
            Func<string, string> tableNameFactory)
        {
            foreach (TemporaryPermissionGrant grant in (grants ?? Enumerable.Empty<TemporaryPermissionGrant>())
                .OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                tables.Add(new PermissionTableDefinition(
                    tableNameFactory(grant.Id),
                    PermissionTomlLayout.TemporaryNodeKeys,
                    table =>
                    {
                        table.Set(PermissionTomlLayout.Keys.UserId, TomlValue.FromString(userId));
                        table.Set(PermissionTomlLayout.Keys.Node, TomlValue.FromString(PermissionNode.Normalize(grant.Node)));
                        table.Set(PermissionTomlLayout.Keys.ExpiresAtUtc, TomlValue.FromString(grant.ExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture)));
                        table.Set(PermissionTomlLayout.Keys.GrantedBy, TomlValue.FromString(grant.GrantedBy ?? string.Empty));
                        table.Set(PermissionTomlLayout.Keys.Reason, TomlValue.FromString(grant.Reason ?? string.Empty));
                    }));
            }
        }

        private static PermissionUserRecord GetOrAddUser(PermissionStoreData data, string userId)
        {
            string normalizedUserId = NormalizeSubjectId(userId);
            if (!data.Users.TryGetValue(normalizedUserId, out PermissionUserRecord user))
            {
                user = new PermissionUserRecord
                {
                    UserId = normalizedUserId
                };
                data.Users[normalizedUserId] = user;
            }

            return user;
        }

        private static void MergeUser(PermissionStoreData data, PermissionUserRecord source)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.UserId))
            {
                return;
            }

            PermissionUserRecord target = GetOrAddUser(data, source.UserId);
            target.Groups = MergeStringLists(target.Groups, source.Groups, normalizeAsGroups: true);
            target.Allow = MergeStringLists(target.Allow, source.Allow, normalizeAsGroups: false);
            target.Deny = MergeStringLists(target.Deny, source.Deny, normalizeAsGroups: false);
        }

        private static List<string> ReadStringArray(TomlTable table, string key, bool normalizeAsGroups)
        {
            if (!table.TryGetArray(key, out IReadOnlyList<TomlValue> values))
            {
                return new List<string>();
            }

            return values
                .Select(value => value.TryGetString(out string item) ? item : string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => normalizeAsGroups ? PermissionNode.NormalizeGroupName(value) : PermissionNode.Normalize(value))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(normalizeAsGroups ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
        }

        private static TomlValue CreateStringArray(IEnumerable<string> values, bool normalizeAsGroups)
        {
            IReadOnlyList<TomlValue> items = MergeStringLists(Array.Empty<string>(), values, normalizeAsGroups)
                .Select(TomlValue.FromString)
                .ToList();

            return TomlValue.FromArray(items);
        }

        private static List<string> MergeStringLists(IEnumerable<string> first, IEnumerable<string> second, bool normalizeAsGroups)
        {
            return (first ?? Enumerable.Empty<string>())
                .Concat(second ?? Enumerable.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => normalizeAsGroups ? PermissionNode.NormalizeGroupName(value) : PermissionNode.Normalize(value))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(normalizeAsGroups ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
        }

        private static bool TryParseUtc(string value, out DateTime parsed)
        {
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime parsedValue))
            {
                parsed = parsedValue.ToUniversalTime();
                return true;
            }

            parsed = DateTime.MinValue;
            return false;
        }

        private static string NormalizeSubjectId(string subjectId)
        {
            return string.IsNullOrWhiteSpace(subjectId)
                ? string.Empty
                : subjectId.Trim();
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

        private sealed class PermissionTableDefinition(
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
