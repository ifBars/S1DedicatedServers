using System;
using System.Collections.Generic;
using System.Linq;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Player;

namespace DedicatedServerMod.Server.Commands.Contracts
{
    /// <summary>
    /// Base class for server commands with common functionality.
    /// </summary>
    public abstract class BaseServerCommand : IServerCommand
    {
        /// <summary>
        /// The player manager shared by commands.
        /// </summary>
        protected readonly PlayerManager PlayerManager;

        /// <summary>
        /// Initializes a new server command base instance.
        /// </summary>
        protected BaseServerCommand(PlayerManager playerMgr)
        {
            PlayerManager = playerMgr;
        }

        /// <inheritdoc />
        public abstract string CommandWord { get; }

        /// <inheritdoc />
        public abstract string Description { get; }

        /// <inheritdoc />
        public abstract string Usage { get; }

        /// <inheritdoc />
        public abstract string RequiredPermissionNode { get; }

        /// <inheritdoc />
        public virtual IReadOnlyCollection<string> GetDiscoveryPermissionNodes()
        {
            return string.IsNullOrWhiteSpace(RequiredPermissionNode)
                ? Array.Empty<string>()
                : new[] { RequiredPermissionNode };
        }

        /// <inheritdoc />
        public virtual string GetRequiredPermissionNode(IReadOnlyList<string> arguments)
        {
            return RequiredPermissionNode;
        }

        /// <inheritdoc />
        public abstract void Execute(CommandContext context);

        /// <summary>
        /// Find a player by name or identifier.
        /// </summary>
        protected ConnectedPlayerInfo FindPlayerByNameOrId(string identifier)
        {
            ConnectedPlayerInfo player = PlayerManager.GetPlayerBySteamId(identifier);
            if (player != null)
            {
                return player;
            }

            player = PlayerManager.GetPlayerByName(identifier);
            if (player != null)
            {
                return player;
            }

            if (int.TryParse(identifier, out int clientId))
            {
                return PlayerManager.GetConnectedPlayers().FirstOrDefault(player => player.ClientId == clientId);
            }

            return null;
        }

        /// <summary>
        /// Validate that the required number of arguments are provided.
        /// </summary>
        protected bool ValidateArguments(CommandContext context, int requiredCount, string usageMessage = null)
        {
            if (context.Arguments.Count < requiredCount)
            {
                context.ReplyError($"Usage: {usageMessage ?? Usage}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get the executing player when the command was run by an in-game player.
        /// </summary>
        protected ConnectedPlayerInfo GetPlayerExecutor(CommandContext context)
        {
            return context != null && !context.IsConsoleExecution
                ? context.Executor
                : null;
        }

        /// <summary>
        /// Get the trusted unique identifier for the executor, or <see langword="null"/> for console execution.
        /// </summary>
        protected string GetExecutorTrustedUniqueId(CommandContext context)
        {
            return GetPlayerExecutor(context)?.TrustedUniqueId;
        }
    }
}
