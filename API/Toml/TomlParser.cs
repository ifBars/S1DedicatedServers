using System.Text;
using DedicatedServerMod.Shared.Toml.Binding;

namespace DedicatedServerMod.API.Toml
{
    /// <summary>
    /// Parses a constrained TOML subset into a <see cref="TomlDocument"/>.
    /// </summary>
    public static class TomlParser
    {
        /// <summary>
        /// Parses TOML text into a document and diagnostic list.
        /// </summary>
        /// <param name="text">The TOML text to parse.</param>
        /// <returns>The parsed document and diagnostics.</returns>
        public static TomlReadResult Parse(string text)
        {
            TomlDocument document = new TomlDocument();
            List<TomlDiagnostic> diagnostics = new List<TomlDiagnostic>();
            List<string> pendingComments = new List<string>();
            TomlTable currentTable = document.Root;
            bool sawAnyEntryOrTable = false;
            string currentKey = null;
            StringBuilder currentValue = null;
            TomlTable currentValueTable = null;
            List<string> currentValueComments = null;
            int currentValueLine = 0;

            string normalizedText = (text ?? string.Empty)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');

            string[] lines = normalizedText.Split('\n');
            for (int index = 0; index < lines.Length; index++)
            {
                int lineNumber = index + 1;
                string rawLine = lines[index];

                if (currentKey != null)
                {
                    string continuedLine = TomlValueCodec.StripInlineComment(rawLine).Trim();
                    if (!string.IsNullOrWhiteSpace(continuedLine))
                    {
                        currentValue.AppendLine(continuedLine);
                    }

                    if (!TomlValueCodec.IsValueComplete(currentValue.ToString()))
                    {
                        continue;
                    }

                    CommitEntry(document, diagnostics, currentValueTable, currentKey, currentValue.ToString(), currentValueComments, currentValueLine);
                    currentKey = null;
                    currentValue = null;
                    currentValueTable = null;
                    currentValueComments = null;
                    sawAnyEntryOrTable = true;
                    continue;
                }

                string trimmedRawLine = rawLine.Trim();
                if (trimmedRawLine.StartsWith("#", StringComparison.Ordinal))
                {
                    pendingComments.Add(ExtractCommentText(trimmedRawLine));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(trimmedRawLine))
                {
                    if (!sawAnyEntryOrTable && document.FileHeaderComments.Count == 0 && pendingComments.Count > 0)
                    {
                        CopyComments(pendingComments, document.FileHeaderComments);
                        pendingComments.Clear();
                    }

                    continue;
                }

                string trimmedLine = TomlValueCodec.StripInlineComment(rawLine).Trim();
                if (string.IsNullOrWhiteSpace(trimmedLine))
                {
                    continue;
                }

                if (trimmedLine.StartsWith("[", StringComparison.Ordinal) &&
                    trimmedLine.EndsWith("]", StringComparison.Ordinal) &&
                    trimmedLine.Length > 2)
                {
                    string tableName = trimmedLine.Substring(1, trimmedLine.Length - 2).Trim();
                    currentTable = document.GetOrAddTable(tableName);
                    if (pendingComments.Count > 0)
                    {
                        currentTable.Comments.Clear();
                        CopyComments(pendingComments, currentTable.Comments);
                        pendingComments.Clear();
                    }

                    sawAnyEntryOrTable = true;
                    continue;
                }

                int equalsIndex = trimmedLine.IndexOf('=');
                if (equalsIndex <= 0)
                {
                    diagnostics.Add(new TomlDiagnostic(lineNumber, currentTable.Name, string.Empty, $"Ignoring malformed TOML line: {rawLine}"));
                    pendingComments.Clear();
                    continue;
                }

                string key = trimmedLine.Substring(0, equalsIndex).Trim();
                string valueText = trimmedLine.Substring(equalsIndex + 1).Trim();
                List<string> entryComments = new List<string>(pendingComments);
                pendingComments.Clear();

                if (!TomlValueCodec.IsValueComplete(valueText))
                {
                    currentKey = key;
                    currentValue = new StringBuilder();
                    currentValue.AppendLine(valueText);
                    currentValueTable = currentTable;
                    currentValueComments = entryComments;
                    currentValueLine = lineNumber;
                    continue;
                }

                CommitEntry(document, diagnostics, currentTable, key, valueText, entryComments, lineNumber);
                sawAnyEntryOrTable = true;
            }

            if (currentKey != null)
            {
                diagnostics.Add(new TomlDiagnostic(currentValueLine, currentValueTable?.Name ?? string.Empty, currentKey, $"Incomplete TOML value for key '{currentKey}'."));
            }

            if (!sawAnyEntryOrTable && document.FileHeaderComments.Count == 0 && pendingComments.Count > 0)
            {
                CopyComments(pendingComments, document.FileHeaderComments);
            }

            return new TomlReadResult(document, diagnostics);
        }

        /// <summary>
        /// Parses a TOML file from disk.
        /// </summary>
        /// <param name="path">The file path to parse.</param>
        /// <returns>The parsed document and diagnostics.</returns>
        public static TomlReadResult ParseFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("TOML path cannot be null or whitespace.", nameof(path));
            }

            return Parse(File.ReadAllText(path));
        }

        private static void CommitEntry(
            TomlDocument document,
            List<TomlDiagnostic> diagnostics,
            TomlTable table,
            string key,
            string rawValue,
            IEnumerable<string> comments,
            int lineNumber)
        {
            TomlTable targetTable = table ?? document.Root;
            if (string.IsNullOrWhiteSpace(key))
            {
                diagnostics.Add(new TomlDiagnostic(lineNumber, targetTable.Name, string.Empty, "Ignoring TOML entry with an empty key."));
                return;
            }

            if (!TomlValueCodec.TryParseToken(rawValue, out TomlValue value, out string error))
            {
                diagnostics.Add(new TomlDiagnostic(lineNumber, targetTable.Name, key, string.IsNullOrWhiteSpace(error) ? "Failed to parse TOML value." : error));
                return;
            }

            targetTable.Set(key, value, comments);
        }

        private static void CopyComments(IEnumerable<string> source, ICollection<string> destination)
        {
            foreach (string line in source)
            {
                destination.Add(line ?? string.Empty);
            }
        }

        private static string ExtractCommentText(string line)
        {
            if (line == null)
            {
                return string.Empty;
            }

            string comment = line.Substring(1);
            return comment.StartsWith(" ", StringComparison.Ordinal)
                ? comment.Substring(1)
                : comment;
        }
    }
}
