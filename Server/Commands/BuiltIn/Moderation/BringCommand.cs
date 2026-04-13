using DedicatedServerMod.Server.Commands.Contracts;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Shared.Permissions;

namespace DedicatedServerMod.Server.Commands.BuiltIn.Moderation
{
    /// <summary>
    /// Teleports a player to the executing staff member or a specified destination player.
    /// </summary>
    public sealed class BringCommand : BaseServerCommand
    {
        /// <summary>
        /// Initializes a new bring command.
        /// </summary>
        /// <param name="playerMgr">The shared player manager.</param>
        public BringCommand(PlayerManager playerMgr)
            : base(playerMgr)
        {
        }

        /// <inheritdoc />
        public override string CommandWord => "bring";

        /// <inheritdoc />
        public override string Description => "Teleport a player to your position or another player's position.";

        /// <inheritdoc />
        public override string Usage => "bring <player_name_or_id> [destination_player_name_or_id]";

        /// <inheritdoc />
        public override string RequiredPermissionNode => PermissionBuiltIns.Nodes.PlayerBring;

        /// <inheritdoc />
        public override void Execute(CommandContext context)
        {
            if (!ValidateArguments(context, 1))
            {
                return;
            }

            ConnectedPlayerInfo targetPlayer = FindPlayerByNameOrId(context.Arguments[0]);
            if (targetPlayer == null)
            {
                context.ReplyError($"Player not found: {context.Arguments[0]}");
                return;
            }

            ConnectedPlayerInfo destinationPlayer = ResolveDestinationPlayer(context);
            if (destinationPlayer == null)
            {
                context.ReplyError(context.IsConsoleExecution
                    ? "Console usage requires a destination player: bring <player_name_or_id> <destination_player_name_or_id>"
                    : "Destination player not found.");
                return;
            }

            if (targetPlayer != context.Executor && context.Executor != null && !CanManagePlayer(context.Executor, targetPlayer))
            {
                context.ReplyError($"Cannot bring {targetPlayer.DisplayName}: insufficient privileges");
                return;
            }

            if (ReferenceEquals(targetPlayer, destinationPlayer))
            {
                context.ReplyError("Target player is already the destination anchor.");
                return;
            }

            if (PlayerManager.BringPlayer(targetPlayer, destinationPlayer, out string errorMessage))
            {
                context.Reply(
                    destinationPlayer == context.Executor
                        ? $"Brought {targetPlayer.DisplayName} to your position."
                        : $"Brought {targetPlayer.DisplayName} to {destinationPlayer.DisplayName}.");
                return;
            }

            context.ReplyError(errorMessage);
        }

        private ConnectedPlayerInfo ResolveDestinationPlayer(CommandContext context)
        {
            if (context.Arguments.Count > 1)
            {
                return FindPlayerByNameOrId(context.Arguments[1]);
            }

            return context.Executor;
        }

        private static bool CanManagePlayer(ConnectedPlayerInfo executor, ConnectedPlayerInfo targetPlayer)
        {
            return DedicatedServerMod.Server.Core.ServerBootstrap.Permissions?.HasDominanceOver(
                executor?.TrustedUniqueId,
                targetPlayer?.TrustedUniqueId) == true;
        }
    }
}
