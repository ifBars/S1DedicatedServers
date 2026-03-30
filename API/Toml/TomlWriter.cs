using System.Text;
using DedicatedServerMod.Shared.Toml.Binding;

namespace DedicatedServerMod.API.Toml
{
    /// <summary>
    /// Serializes <see cref="TomlDocument"/> instances to TOML text.
    /// </summary>
    public static class TomlWriter
    {
        /// <summary>
        /// Writes a document to TOML text.
        /// </summary>
        /// <param name="document">The document to serialize.</param>
        /// <returns>The TOML text.</returns>
        public static string Write(TomlDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            StringBuilder builder = new StringBuilder();
            bool hasRootEntries = document.Root.Entries.Count > 0;
            bool hasTables = document.Tables.Count > 0;

            WriteCommentBlock(builder, document.FileHeaderComments);
            if (document.FileHeaderComments.Count > 0 && (hasRootEntries || hasTables))
            {
                builder.AppendLine();
            }

            WriteEntries(builder, document.Root.Entries);

            for (int index = 0; index < document.Tables.Count; index++)
            {
                TomlTable table = document.Tables[index];
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                WriteCommentBlock(builder, table.Comments);
                builder.Append('[').Append(table.Name).AppendLine("]");
                WriteEntries(builder, table.Entries);
            }

            return builder.ToString().TrimEnd() + Environment.NewLine;
        }

        /// <summary>
        /// Writes a document to disk.
        /// </summary>
        /// <param name="document">The document to serialize.</param>
        /// <param name="path">The destination file path.</param>
        public static void WriteFile(TomlDocument document, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("TOML path cannot be null or whitespace.", nameof(path));
            }

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, Write(document));
        }

        private static void WriteEntries(StringBuilder builder, IReadOnlyList<TomlEntry> entries)
        {
            for (int index = 0; index < entries.Count; index++)
            {
                TomlEntry entry = entries[index];
                if (index > 0)
                {
                    builder.AppendLine();
                }

                WriteCommentBlock(builder, entry.Comments);
                builder.Append(entry.Key).Append(" = ");
                TomlValueCodec.AppendFormattedValue(builder, entry.Value);
                builder.AppendLine();
            }
        }

        private static void WriteCommentBlock(StringBuilder builder, IEnumerable<string> comments)
        {
            foreach (string comment in comments)
            {
                builder.Append("# ");
                builder.AppendLine(comment ?? string.Empty);
            }
        }
    }
}
