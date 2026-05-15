using System.Collections.Generic;
using DedicatedServerMod.Server.Commands.Execution;

namespace DedicatedServerMod.Server.Commands.Contracts
{
    /// <summary>
    /// Defines a command that can be invoked through DedicatedServerMod's server command pipeline.
    /// </summary>
    /// <remarks>
    /// Implementations are shared by console transports and in-game command surfaces. Command code
    /// should validate whether <see cref="CommandContext.Executor"/> is available before using
    /// player-only behavior.
    /// </remarks>
    public interface IServerCommand
    {
        /// <summary>
        /// Gets the command word used to invoke this command.
        /// </summary>
        string CommandWord { get; }

        /// <summary>
        /// Gets a short user-facing description of what the command does.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the usage string shown in help and validation errors.
        /// </summary>
        string Usage { get; }

        /// <summary>
        /// Gets the default permission node required to execute this command.
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
        /// Executes the command with the given context.
        /// </summary>
        /// <param name="context">The invocation context, including arguments, executor, permissions, and reply sinks.</param>
        void Execute(CommandContext context);
    }
}
