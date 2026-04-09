using System.Collections.Generic;

namespace DedicatedServerMod.Server.Commands.Output
{
    /// <summary>
    /// Normalizes and formats structured command replies for console-like transports.
    /// </summary>
    internal static class CommandReplyRenderer
    {
        /// <summary>
        /// Splits a reply into normalized per-line entries while preserving severity.
        /// </summary>
        public static IEnumerable<CommandReplyLine> Expand(CommandReplyLine line)
        {
            string normalized = (line.Message ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                yield return new CommandReplyLine(line.Level, lines[i]);
            }
        }

        /// <summary>
        /// Renders a single normalized reply line into transport text.
        /// </summary>
        public static string RenderText(CommandReplyLine line)
        {
            return GetPrefix(line.Level) + (line.Message ?? string.Empty);
        }

        private static string GetPrefix(CommandReplyLevel level)
        {
            switch (level)
            {
                case CommandReplyLevel.Warning:
                    return "[WARN] ";
                case CommandReplyLevel.Error:
                    return "[ERR] ";
                default:
                    return string.Empty;
            }
        }
    }
}
