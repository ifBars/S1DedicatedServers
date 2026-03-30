using System.Collections.Generic;
using DedicatedServerMod.Server.Commands.Execution;

namespace DedicatedServerMod.Server.Commands.Contracts
{
    /// <summary>
    /// Interface for all server commands.
    /// </summary>
    public interface IServerCommand
    {
        /// <summary>
        /// The command word used to invoke this command.
        /// </summary>
        string CommandWord { get; }

        /// <summary>
        /// Description of what the command does.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Usage example for the command.
        /// </summary>
        string Usage { get; }

        /// <summary>
        /// Required permission node to execute this command.
        /// </summary>
        string RequiredPermissionNode { get; }

        /// <summary>
        /// Gets the permission nodes that should make this command visible in help and discovery surfaces.
        /// </summary>
        IReadOnlyCollection<string> GetDiscoveryPermissionNodes();

        /// <summary>
        /// Resolves the required permission node for a specific invocation.
        /// </summary>
        /// <param name="arguments">The parsed command arguments.</param>
        /// <returns>The required permission node for the invocation.</returns>
        string GetRequiredPermissionNode(IReadOnlyList<string> arguments);

        /// <summary>
        /// Execute the command with the given context.
        /// </summary>
        void Execute(CommandContext context);
    }
}
