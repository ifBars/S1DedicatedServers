namespace DedicatedServerMod.Shared.Permissions
{
    /// <summary>
    /// Normalizes and evaluates dotted permission nodes.
    /// </summary>
    public static class PermissionNode
    {
        /// <summary>
        /// Normalizes a node to a lower-case dotted form.
        /// </summary>
        /// <param name="node">The node to normalize.</param>
        /// <returns>The normalized node, or an empty string when the input is blank.</returns>
        public static string Normalize(string node)
        {
            if (string.IsNullOrWhiteSpace(node))
            {
                return string.Empty;
            }

            string trimmed = node.Trim().Replace(' ', '.');
            while (trimmed.Contains("..", StringComparison.Ordinal))
            {
                trimmed = trimmed.Replace("..", ".", StringComparison.Ordinal);
            }

            return trimmed.Trim('.').ToLowerInvariant();
        }

        /// <summary>
        /// Normalizes a group identifier.
        /// </summary>
        /// <param name="groupName">The group name to normalize.</param>
        /// <returns>The normalized group name.</returns>
        public static string NormalizeGroupName(string groupName)
        {
            return Normalize(groupName).Replace('.', '-');
        }

        /// <summary>
        /// Creates the node used to authorize a remote console command.
        /// </summary>
        /// <param name="commandWord">The command word.</param>
        /// <returns>The remote console node for the command.</returns>
        public static string CreateConsoleCommandNode(string commandWord)
        {
            string normalizedCommand = Normalize(commandWord);
            return string.IsNullOrEmpty(normalizedCommand)
                ? PermissionBuiltIns.Nodes.ConsoleCommandWildcard
                : $"console.command.{normalizedCommand}";
        }

        /// <summary>
        /// Returns a normalized, de-duplicated node list.
        /// </summary>
        /// <param name="nodes">The raw nodes.</param>
        /// <returns>The normalized nodes.</returns>
        public static List<string> NormalizeAll(IEnumerable<string> nodes)
        {
            if (nodes == null)
            {
                return new List<string>();
            }

            return nodes
                .Select(Normalize)
                .Where(node => !string.IsNullOrEmpty(node))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(node => node, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Determines whether a node pattern matches a concrete node.
        /// </summary>
        /// <param name="pattern">The permission pattern.</param>
        /// <param name="node">The concrete node.</param>
        /// <returns>True when the pattern matches.</returns>
        public static bool IsMatch(string pattern, string node)
        {
            return GetSpecificity(pattern, node) >= 0;
        }

        /// <summary>
        /// Gets the specificity score for a pattern/node match.
        /// </summary>
        /// <param name="pattern">The permission pattern.</param>
        /// <param name="node">The concrete node.</param>
        /// <returns>
        /// <see cref="int.MaxValue"/> for an exact match, a non-negative segment count for a
        /// wildcard match, or <c>-1</c> when the pattern does not match.
        /// </returns>
        public static int GetSpecificity(string pattern, string node)
        {
            string normalizedPattern = Normalize(pattern);
            string normalizedNode = Normalize(node);

            if (string.IsNullOrEmpty(normalizedPattern) || string.IsNullOrEmpty(normalizedNode))
            {
                return -1;
            }

            if (string.Equals(normalizedPattern, normalizedNode, StringComparison.Ordinal))
            {
                return int.MaxValue;
            }

            if (string.Equals(normalizedPattern, "*", StringComparison.Ordinal))
            {
                return 0;
            }

            if (!normalizedPattern.EndsWith(".*", StringComparison.Ordinal))
            {
                return -1;
            }

            string prefix = normalizedPattern.Substring(0, normalizedPattern.Length - 2);
            if (normalizedNode.Length <= prefix.Length)
            {
                return -1;
            }

            if (!normalizedNode.StartsWith(prefix, StringComparison.Ordinal) ||
                normalizedNode[prefix.Length] != '.')
            {
                return -1;
            }

            return prefix.Split('.').Length;
        }
    }
}
