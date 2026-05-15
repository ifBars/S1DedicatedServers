using System;
using System.Collections.Generic;
using System.Linq;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Player;

namespace DedicatedServerMod.Server.Commands.Contracts
{
    /// <summary>
    /// Provides shared helpers for built-in and addon-facing server command implementations.
    /// </summary>
    /// <remarks>
    /// The base class centralizes player lookup, argument validation, and executor identity helpers.
    /// Derived commands are still responsible for validating console-versus-player semantics and any
    /// target-specific dominance or permission rules.
    /// </remarks>
    public abstract class BaseServerCommand : IServerCommand
    {
        /// <summary>
        /// Gets the player manager shared by commands.
        /// </summary>
        protected readonly PlayerManager PlayerManager;

        /// <summary>
        /// Initializes a new server command base instance.
        /// </summary>
        /// <param name="playerMgr">The player manager used for lookup and moderation helpers.</param>
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
        /// Finds a connected player by Steam ID, display name, or FishNet client ID.
        /// </summary>
        /// <param name="identifier">The player identifier supplied by the command caller.</param>
        /// <returns>The matching connected player, or <see langword="null"/> when no match exists.</returns>
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
        /// Validates that the required number of arguments are provided.
        /// </summary>
        /// <param name="context">The active command context.</param>
        /// <param name="requiredCount">The minimum number of required arguments.</param>
        /// <param name="usageMessage">Optional usage message to show when validation fails.</param>
        /// <returns><see langword="true"/> when enough arguments were supplied; otherwise <see langword="false"/>.</returns>
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
        /// Gets the executing player when the command was run by an in-game player.
        /// </summary>
        /// <param name="context">The active command context.</param>
        /// <returns>The player executor, or <see langword="null"/> for console execution.</returns>
        protected ConnectedPlayerInfo GetPlayerExecutor(CommandContext context)
        {
            return context != null && !context.IsConsoleExecution
                ? context.Executor
                : null;
        }

        /// <summary>
        /// Gets the trusted unique identifier for the executor, or <see langword="null"/> for console execution.
        /// </summary>
        /// <param name="context">The active command context.</param>
        /// <returns>The executor's trusted unique identifier, or <see langword="null"/> for console execution.</returns>
        protected string GetExecutorTrustedUniqueId(CommandContext context)
        {
            return GetPlayerExecutor(context)?.TrustedUniqueId;
        }
    }
}
