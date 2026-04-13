using DedicatedServerMod.Server.Commands.Contracts;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Shared.Permissions;

namespace DedicatedServerMod.Server.Commands.BuiltIn.Moderation
{
    /// <summary>
    /// Toggles or sets a player's vanished state.
    /// </summary>
    public sealed class VanishCommand : BaseServerCommand
    {
        /// <summary>
        /// Initializes a new vanish command.
        /// </summary>
        /// <param name="playerMgr">The shared player manager.</param>
        public VanishCommand(PlayerManager playerMgr)
            : base(playerMgr)
        {
        }

        /// <inheritdoc />
        public override string CommandWord => "vanish";

        /// <inheritdoc />
        public override string Description => "Toggle or set vanish mode for yourself or another player.";

        /// <inheritdoc />
        public override string Usage => "vanish [player_name_or_id] [on|off|toggle]";

        /// <inheritdoc />
        public override string RequiredPermissionNode => PermissionBuiltIns.Nodes.PlayerVanish;

        /// <inheritdoc />
        public override void Execute(CommandContext context)
        {
            if (!TryResolveInvocation(context, out ConnectedPlayerInfo targetPlayer, out bool desiredState, out string errorMessage))
            {
                context.ReplyError(errorMessage);
                return;
            }

            if (targetPlayer != context.Executor && context.Executor != null && !CanManagePlayer(context.Executor, targetPlayer))
            {
                context.ReplyError($"Cannot change vanish state for {targetPlayer.DisplayName}: insufficient privileges");
                return;
            }

            bool currentState = PlayerManager.IsPlayerVanished(targetPlayer);
            if (currentState == desiredState)
            {
                context.Reply($"{targetPlayer.DisplayName} is already {(desiredState ? "vanished" : "visible")}.");
                return;
            }

            if (PlayerManager.SetPlayerVanished(targetPlayer, desiredState, out errorMessage))
            {
                context.Reply($"{targetPlayer.DisplayName} is now {(desiredState ? "vanished" : "visible")}.");
                return;
            }

            context.ReplyError(errorMessage);
        }

        private bool TryResolveInvocation(
            CommandContext context,
            out ConnectedPlayerInfo targetPlayer,
            out bool desiredState,
            out string errorMessage)
        {
            targetPlayer = null;
            desiredState = false;
            errorMessage = string.Empty;

            if (context.Arguments.Count == 0)
            {
                if (context.Executor == null)
                {
                    errorMessage = $"Usage: {Usage}";
                    return false;
                }

                targetPlayer = context.Executor;
                desiredState = !PlayerManager.IsPlayerVanished(targetPlayer);
                return true;
            }

            if (context.Arguments.Count == 1)
            {
                if (TryParseDesiredState(context.Arguments[0], out bool? parsedState))
                {
                    if (context.Executor == null)
                    {
                        errorMessage = "Console usage requires a target player when setting vanish state.";
                        return false;
                    }

                    targetPlayer = context.Executor;
                    desiredState = parsedState ?? !PlayerManager.IsPlayerVanished(targetPlayer);
                    return true;
                }

                targetPlayer = FindPlayerByNameOrId(context.Arguments[0]);
                if (targetPlayer == null)
                {
                    errorMessage = $"Player not found: {context.Arguments[0]}";
                    return false;
                }

                desiredState = !PlayerManager.IsPlayerVanished(targetPlayer);
                return true;
            }

            if (context.Arguments.Count == 2)
            {
                targetPlayer = FindPlayerByNameOrId(context.Arguments[0]);
                if (targetPlayer == null)
                {
                    errorMessage = $"Player not found: {context.Arguments[0]}";
                    return false;
                }

                if (!TryParseDesiredState(context.Arguments[1], out bool? parsedState))
                {
                    errorMessage = $"Unknown vanish state '{context.Arguments[1]}'. Use on, off, or toggle.";
                    return false;
                }

                desiredState = parsedState ?? !PlayerManager.IsPlayerVanished(targetPlayer);
                return true;
            }

            errorMessage = $"Usage: {Usage}";
            return false;
        }

        private static bool TryParseDesiredState(string value, out bool? desiredState)
        {
            desiredState = null;

            switch (value?.Trim().ToLowerInvariant())
            {
                case "on":
                case "enable":
                case "enabled":
                case "true":
                    desiredState = true;
                    return true;
                case "off":
                case "disable":
                case "disabled":
                case "false":
                    desiredState = false;
                    return true;
                case "toggle":
                    desiredState = null;
                    return true;
                default:
                    return false;
            }
        }

        private static bool CanManagePlayer(ConnectedPlayerInfo executor, ConnectedPlayerInfo targetPlayer)
        {
            return DedicatedServerMod.Server.Core.ServerBootstrap.Permissions?.HasDominanceOver(
                executor?.TrustedUniqueId,
                targetPlayer?.TrustedUniqueId) == true;
        }
    }
}
