using MelonLoader;
using System.Collections.Generic;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Shared.Permissions;

namespace DedicatedServerMod.Server.Commands.Admin
{
    /// <summary>
    /// Lists groups and assigns or removes them from users.
    /// </summary>
    public sealed class GroupCommand : BaseServerCommand
    {
        /// <summary>
        /// Initializes a new group management command.
        /// </summary>
        public GroupCommand(MelonLogger.Instance logger, PlayerManager playerMgr)
            : base(logger, playerMgr)
        {
        }

        /// <inheritdoc />
        public override string CommandWord => "group";

        /// <inheritdoc />
        public override string Description => "Lists, assigns, or removes permission groups";

        /// <inheritdoc />
        public override string Usage => "group <list|assign|unassign> ...";

        /// <inheritdoc />
        public override string RequiredPermissionNode => PermissionBuiltIns.Nodes.PermissionsGroupList;

        /// <inheritdoc />
        public override IReadOnlyCollection<string> GetDiscoveryPermissionNodes()
        {
            return new[]
            {
                PermissionBuiltIns.Nodes.PermissionsGroupList,
                PermissionBuiltIns.Nodes.PermissionsGroupAssign,
                PermissionBuiltIns.Nodes.PermissionsGroupUnassign
            };
        }

        /// <inheritdoc />
        public override string GetRequiredPermissionNode(IReadOnlyList<string> arguments)
        {
            if (arguments == null || arguments.Count == 0)
            {
                return RequiredPermissionNode;
            }

            switch (arguments[0]?.Trim().ToLowerInvariant())
            {
                case "assign":
                    return PermissionBuiltIns.Nodes.PermissionsGroupAssign;
                case "unassign":
                    return PermissionBuiltIns.Nodes.PermissionsGroupUnassign;
                case "list":
                default:
                    return PermissionBuiltIns.Nodes.PermissionsGroupList;
            }
        }

        /// <inheritdoc />
        public override void Execute(CommandContext context)
        {
            if (!ValidateArguments(context, 1))
            {
                return;
            }

            string action = context.Arguments[0].Trim().ToLowerInvariant();
            switch (action)
            {
                case "list":
                    context.Reply(string.Join(", ", context.Permissions?.GetGroupNames() ?? System.Array.Empty<string>()));
                    break;
                case "assign":
                    ExecuteAssign(context);
                    break;
                case "unassign":
                    ExecuteUnassign(context);
                    break;
                default:
                    context.ReplyError($"Unknown group action '{action}'. Usage: {Usage}");
                    break;
            }
        }

        private void ExecuteAssign(CommandContext context)
        {
            if (context.Executor != null && context.Permissions?.HasPermission(context.Executor.TrustedUniqueId, PermissionBuiltIns.Nodes.PermissionsGroupAssign) != true)
            {
                context.ReplyError("You do not have permission to assign groups.");
                return;
            }

            if (context.Arguments.Count < 3)
            {
                context.ReplyError("Usage: group assign <player_or_steamid> <group>");
                return;
            }

            string targetId = ResolveTargetId(context.Arguments[1]);
            string groupName = context.Arguments[2];
            if (context.Permissions?.AssignGroup(context.Executor?.TrustedUniqueId, targetId, groupName, "group assign") == true)
            {
                context.Reply($"Assigned group '{groupName}' to '{targetId}'.");
                return;
            }

            context.ReplyError($"Failed to assign group '{groupName}' to '{targetId}'.");
        }

        private void ExecuteUnassign(CommandContext context)
        {
            if (context.Executor != null && context.Permissions?.HasPermission(context.Executor.TrustedUniqueId, PermissionBuiltIns.Nodes.PermissionsGroupUnassign) != true)
            {
                context.ReplyError("You do not have permission to remove groups.");
                return;
            }

            if (context.Arguments.Count < 3)
            {
                context.ReplyError("Usage: group unassign <player_or_steamid> <group>");
                return;
            }

            string targetId = ResolveTargetId(context.Arguments[1]);
            string groupName = context.Arguments[2];
            if (context.Permissions?.UnassignGroup(context.Executor?.TrustedUniqueId, targetId, groupName, "group unassign") == true)
            {
                context.Reply($"Removed group '{groupName}' from '{targetId}'.");
                return;
            }

            context.ReplyError($"Failed to remove group '{groupName}' from '{targetId}'.");
        }

        private string ResolveTargetId(string identifier)
        {
            ConnectedPlayerInfo player = FindPlayerByNameOrId(identifier);
            return player?.TrustedUniqueId ?? identifier;
        }
    }
}
