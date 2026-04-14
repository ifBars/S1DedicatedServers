using DedicatedServerMod.Server.Commands.Contracts;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Shared.Permissions;

namespace DedicatedServerMod.Server.Commands.BuiltIn.Moderation
{
    /// <summary>
    /// Returns a teleported player to their most recent saved position.
    /// </summary>
    public sealed class ReturnCommand : BaseServerCommand
    {
        /// <summary>
        /// Initializes a new return command.
        /// </summary>
        /// <param name="playerMgr">The shared player manager.</param>
        public ReturnCommand(PlayerManager playerMgr)
            : base(playerMgr)
        {
        }

        /// <inheritdoc />
        public override string CommandWord => "return";

        /// <inheritdoc />
        public override string Description => "Return yourself or another player to the last saved teleport position.";

        /// <inheritdoc />
        public override string Usage => "return [player_name_or_id]";

        /// <inheritdoc />
        public override string RequiredPermissionNode => PermissionBuiltIns.Nodes.PlayerReturn;

        /// <inheritdoc />
        public override void Execute(CommandContext context)
        {
            ConnectedPlayerInfo executor = GetPlayerExecutor(context);
            ConnectedPlayerInfo targetPlayer = ResolveTargetPlayer(context);
            if (targetPlayer == null)
            {
                context.ReplyError(context.IsConsoleExecution
                    ? "Usage: return <player_name_or_id>"
                    : "Usage: return [player_name_or_id]");
                return;
            }

            if (targetPlayer != executor && executor != null && !CanManagePlayer(executor, targetPlayer))
            {
                context.ReplyError($"Cannot return {targetPlayer.DisplayName}: insufficient privileges");
                return;
            }

            if (PlayerManager.ReturnPlayerToPreviousPosition(targetPlayer, out string errorMessage))
            {
                context.Reply($"Returned {targetPlayer.DisplayName} to their previous position.");
                return;
            }

            context.ReplyError(errorMessage);
        }

        private ConnectedPlayerInfo ResolveTargetPlayer(CommandContext context)
        {
            if (context.Arguments.Count == 0)
            {
                return GetPlayerExecutor(context);
            }

            return FindPlayerByNameOrId(context.Arguments[0]);
        }

        private static bool CanManagePlayer(ConnectedPlayerInfo executor, ConnectedPlayerInfo targetPlayer)
        {
            return DedicatedServerMod.Server.Core.ServerBootstrap.Permissions?.HasDominanceOver(
                executor?.TrustedUniqueId,
                targetPlayer?.TrustedUniqueId) == true;
        }
    }
}
