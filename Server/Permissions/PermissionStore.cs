using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DedicatedServerMod.Shared.Permissions;
using DedicatedServerMod.Utils;
using MelonLoader;
using MelonLoader.Utils;

namespace DedicatedServerMod.Server.Permissions
{
    /// <summary>
    /// Reads and writes the dedicated permissions file.
    /// </summary>
    internal sealed class PermissionStore
    {
        private readonly MelonLogger.Instance _logger;

        /// <summary>
        /// Initializes a new permissions store.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        public PermissionStore(MelonLogger.Instance logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Gets the full permissions file path.
        /// </summary>
        public string FilePath => Path.Combine(MelonEnvironment.UserDataDirectory, Constants.PermissionsFileName);

        /// <summary>
        /// Gets whether the permissions file already exists.
        /// </summary>
        public bool Exists => File.Exists(FilePath);

        /// <summary>
        /// Loads the permissions data from disk.
        /// </summary>
        /// <returns>The loaded data.</returns>
        public PermissionStoreData Load()
        {
            if (!Exists)
            {
                return new PermissionStoreData();
            }

            string text = File.ReadAllText(FilePath);
            return Deserialize(text);
        }

        /// <summary>
        /// Saves the permissions data to disk.
        /// </summary>
        /// <param name="data">The data to save.</param>
        public void Save(PermissionStoreData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(FilePath) ?? MelonEnvironment.UserDataDirectory);
            File.WriteAllText(FilePath, Serialize(data));
            _logger.Msg($"Permissions saved to {FilePath}");
        }

        private static string Serialize(PermissionStoreData data)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# DedicatedServerMod permissions file");
            builder.AppendLine();

            builder.AppendLine("[metadata]");
            builder.AppendLine($"schemaVersion = {data.SchemaVersion}");
            builder.AppendLine($"migrationVersion = {data.MigrationVersion}");
            builder.AppendLine($"migratedFrom = {FormatString(data.MigratedFrom)}");
            builder.AppendLine($"migratedAtUtc = {FormatString(data.MigratedAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty)}");
            builder.AppendLine();

            foreach (PermissionGroupDefinition group in data.Groups.Values.OrderBy(group => group.Priority).ThenBy(group => group.Name, StringComparer.Ordinal))
            {
                builder.AppendLine($"[group.{PermissionNode.NormalizeGroupName(group.Name)}]");
                builder.AppendLine($"priority = {group.Priority}");
                builder.AppendLine($"inherits = {FormatStringArray(group.Inherits)}");
                builder.AppendLine($"allow = {FormatStringArray(group.Allow)}");
                builder.AppendLine($"deny = {FormatStringArray(group.Deny)}");
                builder.AppendLine();
            }

            foreach (PermissionUserRecord user in data.Users.Values.OrderBy(user => user.UserId, StringComparer.Ordinal))
            {
                builder.AppendLine($"[user.{user.UserId}]");
                builder.AppendLine($"groups = {FormatStringArray(user.Groups)}");
                builder.AppendLine($"allow = {FormatStringArray(user.Allow)}");
                builder.AppendLine($"deny = {FormatStringArray(user.Deny)}");
                builder.AppendLine();
            }

            foreach (PermissionUserRecord user in data.Users.Values.OrderBy(user => user.UserId, StringComparer.Ordinal))
            {
                foreach (TemporaryGroupAssignment assignment in user.TemporaryGroups.OrderBy(item => item.Id, StringComparer.Ordinal))
                {
                    builder.AppendLine($"[tempgroup.{assignment.Id}]");
                    builder.AppendLine($"userId = {FormatString(user.UserId)}");
                    builder.AppendLine($"group = {FormatString(assignment.GroupName)}");
                    builder.AppendLine($"expiresAtUtc = {FormatString(assignment.ExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture))}");
                    builder.AppendLine($"grantedBy = {FormatString(assignment.GrantedBy)}");
                    builder.AppendLine($"reason = {FormatString(assignment.Reason)}");
                    builder.AppendLine();
                }

                WriteTemporaryNodes(builder, user, user.TemporaryAllow, "tempallow");
                WriteTemporaryNodes(builder, user, user.TemporaryDeny, "tempdeny");
            }

            foreach (BanEntry ban in data.Bans.Values.OrderBy(ban => ban.SubjectId, StringComparer.Ordinal))
            {
                builder.AppendLine($"[ban.{ban.SubjectId}]");
                builder.AppendLine($"subjectId = {FormatString(ban.SubjectId)}");
                builder.AppendLine($"createdAtUtc = {FormatString(ban.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture))}");
                builder.AppendLine($"createdBy = {FormatString(ban.CreatedBy)}");
                builder.AppendLine($"reason = {FormatString(ban.Reason)}");
                builder.AppendLine();
            }

            return builder.ToString();
        }

        private static void WriteTemporaryNodes(StringBuilder builder, PermissionUserRecord user, IEnumerable<TemporaryPermissionGrant> grants, string sectionPrefix)
        {
            foreach (TemporaryPermissionGrant grant in grants.OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                builder.AppendLine($"[{sectionPrefix}.{grant.Id}]");
                builder.AppendLine($"userId = {FormatString(user.UserId)}");
                builder.AppendLine($"node = {FormatString(grant.Node)}");
                builder.AppendLine($"expiresAtUtc = {FormatString(grant.ExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture))}");
                builder.AppendLine($"grantedBy = {FormatString(grant.GrantedBy)}");
                builder.AppendLine($"reason = {FormatString(grant.Reason)}");
                builder.AppendLine();
            }
        }

        private static string FormatString(string value)
        {
            string safeValue = value ?? string.Empty;
            if (!safeValue.Contains('\'') && !safeValue.Contains('\r') && !safeValue.Contains('\n'))
            {
                return $"'{safeValue}'";
            }

            string escaped = safeValue
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal)
                .Replace("\r", "\\r", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal)
                .Replace("\t", "\\t", StringComparison.Ordinal);
            return $"\"{escaped}\"";
        }

        private static string FormatStringArray(IEnumerable<string> values)
        {
            List<string> normalizedValues = (values ?? Enumerable.Empty<string>())
                .Select(value => value ?? string.Empty)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .Select(FormatString)
                .ToList();

            return $"[{string.Join(", ", normalizedValues)}]";
        }

        private PermissionStoreData Deserialize(string text)
        {
            PermissionStoreData data = new PermissionStoreData();
            string currentSection = string.Empty;
            string currentKey = null;
            StringBuilder currentValue = null;
            int currentValueLine = 0;
            Dictionary<string, string> sectionValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] lines = (text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

            void FlushSection()
            {
                if (string.IsNullOrWhiteSpace(currentSection))
                {
                    return;
                }

                ApplySection(data, currentSection, sectionValues);
                sectionValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            for (int index = 0; index < lines.Length; index++)
            {
                int lineNumber = index + 1;
                string rawLine = lines[index];
                string trimmedLine = StripInlineComment(rawLine).Trim();

                if (currentKey != null)
                {
                    if (!string.IsNullOrWhiteSpace(trimmedLine))
                    {
                        currentValue.AppendLine(trimmedLine);
                    }

                    if (IsTomlValueComplete(currentValue.ToString()))
                    {
                        sectionValues[currentKey] = currentValue.ToString().Trim();
                        currentKey = null;
                        currentValue = null;
                    }

                    continue;
                }

                if (string.IsNullOrWhiteSpace(trimmedLine))
                {
                    continue;
                }

                if (trimmedLine.StartsWith("[", StringComparison.Ordinal) && trimmedLine.EndsWith("]", StringComparison.Ordinal))
                {
                    FlushSection();
                    currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2).Trim();
                    continue;
                }

                int equalsIndex = trimmedLine.IndexOf('=');
                if (equalsIndex <= 0)
                {
                    _logger.Warning($"Ignoring malformed permissions line {lineNumber}: {rawLine}");
                    continue;
                }

                string key = trimmedLine.Substring(0, equalsIndex).Trim();
                string value = trimmedLine.Substring(equalsIndex + 1).Trim();
                if (!IsTomlValueComplete(value))
                {
                    currentKey = key;
                    currentValue = new StringBuilder();
                    currentValue.AppendLine(value);
                    currentValueLine = lineNumber;
                    continue;
                }

                sectionValues[key] = value;
            }

            if (currentKey != null)
            {
                _logger.Warning($"Incomplete permissions value '{currentKey}' starting on line {currentValueLine}.");
            }

            FlushSection();
            return data;
        }

        private void ApplySection(PermissionStoreData data, string section, IReadOnlyDictionary<string, string> values)
        {
            if (string.Equals(section, "metadata", StringComparison.OrdinalIgnoreCase))
            {
                data.SchemaVersion = ParseInt(values, "schemaVersion", data.SchemaVersion);
                data.MigrationVersion = ParseInt(values, "migrationVersion", data.MigrationVersion);
                data.MigratedFrom = ParseString(values, "migratedFrom");
                data.MigratedAtUtc = ParseDateTime(values, "migratedAtUtc");
                return;
            }

            if (section.StartsWith("group.", StringComparison.OrdinalIgnoreCase))
            {
                string groupName = PermissionNode.NormalizeGroupName(section.Substring("group.".Length));
                data.Groups[groupName] = new PermissionGroupDefinition
                {
                    Name = groupName,
                    Priority = ParseInt(values, "priority", 0),
                    Inherits = ParseStringArray(values, "inherits"),
                    Allow = ParseStringArray(values, "allow"),
                    Deny = ParseStringArray(values, "deny")
                };
                return;
            }

            if (section.StartsWith("user.", StringComparison.OrdinalIgnoreCase))
            {
                string userId = section.Substring("user.".Length).Trim();
                PermissionUserRecord user = GetOrCreateUser(data, userId);
                user.Groups = ParseStringArray(values, "groups");
                user.Allow = ParseStringArray(values, "allow");
                user.Deny = ParseStringArray(values, "deny");
                return;
            }

            if (section.StartsWith("tempgroup.", StringComparison.OrdinalIgnoreCase))
            {
                string userId = ParseString(values, "userId");
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return;
                }

                PermissionUserRecord user = GetOrCreateUser(data, userId);
                user.TemporaryGroups.Add(new TemporaryGroupAssignment
                {
                    Id = section.Substring("tempgroup.".Length),
                    GroupName = ParseString(values, "group"),
                    ExpiresAtUtc = ParseDateTime(values, "expiresAtUtc") ?? DateTime.UtcNow,
                    GrantedBy = ParseString(values, "grantedBy"),
                    Reason = ParseString(values, "reason")
                });
                return;
            }

            if (section.StartsWith("tempallow.", StringComparison.OrdinalIgnoreCase) ||
                section.StartsWith("tempdeny.", StringComparison.OrdinalIgnoreCase))
            {
                bool isDeny = section.StartsWith("tempdeny.", StringComparison.OrdinalIgnoreCase);
                string userId = ParseString(values, "userId");
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return;
                }

                PermissionUserRecord user = GetOrCreateUser(data, userId);
                TemporaryPermissionGrant grant = new TemporaryPermissionGrant
                {
                    Id = section.Substring(isDeny ? "tempdeny.".Length : "tempallow.".Length),
                    Node = ParseString(values, "node"),
                    ExpiresAtUtc = ParseDateTime(values, "expiresAtUtc") ?? DateTime.UtcNow,
                    GrantedBy = ParseString(values, "grantedBy"),
                    Reason = ParseString(values, "reason")
                };

                if (isDeny)
                {
                    user.TemporaryDeny.Add(grant);
                }
                else
                {
                    user.TemporaryAllow.Add(grant);
                }

                return;
            }

            if (section.StartsWith("ban.", StringComparison.OrdinalIgnoreCase))
            {
                string subjectId = ParseString(values, "subjectId");
                if (string.IsNullOrWhiteSpace(subjectId))
                {
                    subjectId = section.Substring("ban.".Length);
                }

                if (string.IsNullOrWhiteSpace(subjectId))
                {
                    return;
                }

                data.Bans[subjectId] = new BanEntry
                {
                    SubjectId = subjectId,
                    CreatedAtUtc = ParseDateTime(values, "createdAtUtc") ?? DateTime.UtcNow,
                    CreatedBy = ParseString(values, "createdBy"),
                    Reason = ParseString(values, "reason")
                };
            }
        }

        private static PermissionUserRecord GetOrCreateUser(PermissionStoreData data, string userId)
        {
            string normalizedUserId = userId.Trim();
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

        private static int ParseInt(IReadOnlyDictionary<string, string> values, string key, int defaultValue)
        {
            if (!values.TryGetValue(key, out string raw) || !int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                return defaultValue;
            }

            return value;
        }

        private static string ParseString(IReadOnlyDictionary<string, string> values, string key)
        {
            if (!values.TryGetValue(key, out string raw))
            {
                return string.Empty;
            }

            return ParseTomlString(raw);
        }

        private static DateTime? ParseDateTime(IReadOnlyDictionary<string, string> values, string key)
        {
            string raw = ParseString(values, key);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsed))
            {
                return parsed.ToUniversalTime();
            }

            return null;
        }

        private static List<string> ParseStringArray(IReadOnlyDictionary<string, string> values, string key)
        {
            if (!values.TryGetValue(key, out string raw))
            {
                return new List<string>();
            }

            return ParseTomlStringArray(raw);
        }

        private static string StripInlineComment(string line)
        {
            bool inBasicString = false;
            bool inLiteralString = false;
            bool escaped = false;

            for (int i = 0; i < line.Length; i++)
            {
                char character = line[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (inBasicString)
                {
                    if (character == '\\')
                    {
                        escaped = true;
                    }
                    else if (character == '"')
                    {
                        inBasicString = false;
                    }

                    continue;
                }

                if (inLiteralString)
                {
                    if (character == '\'')
                    {
                        inLiteralString = false;
                    }

                    continue;
                }

                if (character == '"')
                {
                    inBasicString = true;
                    continue;
                }

                if (character == '\'')
                {
                    inLiteralString = true;
                    continue;
                }

                if (character == '#')
                {
                    return line.Substring(0, i);
                }
            }

            return line;
        }

        private static bool IsTomlValueComplete(string value)
        {
            int bracketDepth = 0;
            bool inBasicString = false;
            bool inLiteralString = false;
            bool escaped = false;

            foreach (char character in value)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (inBasicString)
                {
                    if (character == '\\')
                    {
                        escaped = true;
                    }
                    else if (character == '"')
                    {
                        inBasicString = false;
                    }

                    continue;
                }

                if (inLiteralString)
                {
                    if (character == '\'')
                    {
                        inLiteralString = false;
                    }

                    continue;
                }

                switch (character)
                {
                    case '"':
                        inBasicString = true;
                        break;
                    case '\'':
                        inLiteralString = true;
                        break;
                    case '[':
                        bracketDepth++;
                        break;
                    case ']':
                        bracketDepth--;
                        break;
                }
            }

            return bracketDepth <= 0 && !inBasicString && !inLiteralString;
        }

        private static string ParseTomlString(string token)
        {
            string trimmed = token?.Trim() ?? string.Empty;
            if (trimmed.Length >= 2 && trimmed.StartsWith("'", StringComparison.Ordinal) && trimmed.EndsWith("'", StringComparison.Ordinal))
            {
                return trimmed.Substring(1, trimmed.Length - 2);
            }

            if (trimmed.Length >= 2 && trimmed.StartsWith("\"", StringComparison.Ordinal) && trimmed.EndsWith("\"", StringComparison.Ordinal))
            {
                return UnescapeBasicString(trimmed.Substring(1, trimmed.Length - 2));
            }

            return trimmed;
        }

        private static string UnescapeBasicString(string value)
        {
            StringBuilder builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (character != '\\' || i == value.Length - 1)
                {
                    builder.Append(character);
                    continue;
                }

                char escapeCode = value[++i];
                switch (escapeCode)
                {
                    case '\\':
                        builder.Append('\\');
                        break;
                    case '"':
                        builder.Append('"');
                        break;
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    default:
                        builder.Append(escapeCode);
                        break;
                }
            }

            return builder.ToString();
        }

        private static List<string> ParseTomlStringArray(string value)
        {
            string trimmed = value?.Trim() ?? string.Empty;
            if (trimmed == "[]")
            {
                return new List<string>();
            }

            if (!trimmed.StartsWith("[", StringComparison.Ordinal) || !trimmed.EndsWith("]", StringComparison.Ordinal))
            {
                return new List<string>();
            }

            string inner = trimmed.Substring(1, trimmed.Length - 2);
            List<string> items = new List<string>();
            StringBuilder current = new StringBuilder();
            bool inBasicString = false;
            bool inLiteralString = false;
            bool escaped = false;

            foreach (char character in inner)
            {
                if (escaped)
                {
                    current.Append(character);
                    escaped = false;
                    continue;
                }

                if (inBasicString)
                {
                    current.Append(character);
                    if (character == '\\')
                    {
                        escaped = true;
                    }
                    else if (character == '"')
                    {
                        inBasicString = false;
                    }

                    continue;
                }

                if (inLiteralString)
                {
                    current.Append(character);
                    if (character == '\'')
                    {
                        inLiteralString = false;
                    }

                    continue;
                }

                if (character == '"')
                {
                    inBasicString = true;
                    current.Append(character);
                    continue;
                }

                if (character == '\'')
                {
                    inLiteralString = true;
                    current.Append(character);
                    continue;
                }

                if (character == ',')
                {
                    string item = current.ToString().Trim();
                    if (item.Length > 0)
                    {
                        items.Add(ParseTomlString(item));
                    }

                    current.Clear();
                    continue;
                }

                current.Append(character);
            }

            string finalItem = current.ToString().Trim();
            if (finalItem.Length > 0)
            {
                items.Add(ParseTomlString(finalItem));
            }

            return items;
        }
    }
}
