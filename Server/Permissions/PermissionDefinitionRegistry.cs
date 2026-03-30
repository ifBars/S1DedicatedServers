using DedicatedServerMod.Shared.Permissions;

namespace DedicatedServerMod.Server.Permissions
{
    /// <summary>
    /// Stores registered permission definitions from the framework and addons.
    /// </summary>
    internal sealed class PermissionDefinitionRegistry
    {
        private readonly Dictionary<string, PermissionDefinition> _definitions =
            new Dictionary<string, PermissionDefinition>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Registers or replaces permission definitions.
        /// </summary>
        /// <param name="definitions">The definitions to register.</param>
        public void Register(IEnumerable<PermissionDefinition> definitions)
        {
            if (definitions == null)
            {
                return;
            }

            foreach (PermissionDefinition definition in definitions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.Node))
                {
                    continue;
                }

                string normalizedNode = PermissionNode.Normalize(definition.Node);
                _definitions[normalizedNode] = new PermissionDefinition
                {
                    Node = normalizedNode,
                    Category = definition.Category ?? string.Empty,
                    Description = definition.Description ?? string.Empty,
                    SuggestedGroups = definition.SuggestedGroups?
                        .Where(group => !string.IsNullOrWhiteSpace(group))
                        .Select(PermissionNode.NormalizeGroupName)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList() ?? new List<string>()
                };
            }
        }

        /// <summary>
        /// Gets all registered permission definitions.
        /// </summary>
        /// <returns>The registered definitions.</returns>
        public IReadOnlyList<PermissionDefinition> GetAll()
        {
            return _definitions.Values
                .OrderBy(definition => definition.Node, StringComparer.Ordinal)
                .ToList();
        }
    }
}
