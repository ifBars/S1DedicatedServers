using DedicatedServerMod.Server.Commands.Contracts;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Shared.Permissions;

namespace DedicatedServerMod.Server.Commands.BuiltIn.Permissions
{
    /// <summary>
    /// Manages direct node grants, denies, revocations, and inspection.
    /// </summary>
    public sealed class PermissionCommand : BaseServerCommand
    {
        /// <summary>
        /// Initializes a new permission management command.
        /// </summary>
        public PermissionCommand(PlayerManager playerMgr)
            : base(playerMgr)
        {
        }

        /// <inheritdoc />
        public override string CommandWord => "perm";

        /// <inheritdoc />
        public override string Description => "Inspects or mutates direct permission nodes";

        /// <inheritdoc />
        public override string Usage => "perm <info|grant|deny|revoke|tempgrant> ...";

        /// <inheritdoc />
        public override string RequiredPermissionNode => PermissionBuiltIns.Nodes.PermissionsInfo;

        /// <inheritdoc />
        public override IReadOnlyCollection<string> GetDiscoveryPermissionNodes()
        {
            return new[]
            {
                PermissionBuiltIns.Nodes.PermissionsInfo,
                PermissionBuiltIns.Nodes.PermissionsGrant,
                PermissionBuiltIns.Nodes.PermissionsDeny,
                PermissionBuiltIns.Nodes.PermissionsRevoke,
                PermissionBuiltIns.Nodes.PermissionsTempGrant
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
                case "grant":
                    return PermissionBuiltIns.Nodes.PermissionsGrant;
                case "deny":
                    return PermissionBuiltIns.Nodes.PermissionsDeny;
                case "revoke":
                    return PermissionBuiltIns.Nodes.PermissionsRevoke;
                case "tempgrant":
                    return PermissionBuiltIns.Nodes.PermissionsTempGrant;
                case "info":
                default:
                    return PermissionBuiltIns.Nodes.PermissionsInfo;
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
                case "info":
                    ExecuteInfo(context);
                    break;
                case "grant":
                    ExecuteGrant(context, isDeny: false);
                    break;
                case "deny":
                    ExecuteGrant(context, isDeny: true);
                    break;
                case "revoke":
                    ExecuteRevoke(context);
                    break;
                case "tempgrant":
                    ExecuteTemporaryGrant(context);
                    break;
                default:
                    context.ReplyError($"Unknown perm action '{action}'. Usage: {Usage}");
                    break;
            }
        }

        private void ExecuteInfo(CommandContext context)
        {
            if (context.Arguments.Count == 1)
            {
                DedicatedServerMod.Shared.Permissions.PermissionSummary summary = context.Permissions?.GetSummary();
                context.Reply(summary == null
                    ? "Permissions unavailable."
                    : $"Groups={summary.TotalGroups}, Users={summary.TotalUsers}, Bans={summary.TotalBans}, Operators={summary.TotalOperators}, Administrators={summary.TotalAdministrators}");
                return;
            }

            string targetId = ResolveTargetId(context.Arguments[1]);
            PermissionUserRecord record = context.Permissions?.GetUserRecord(targetId);
            if (record == null)
            {
                context.ReplyWarning($"No direct permission record exists for '{targetId}'.");
            }

            string effectiveGroups = string.Join(", ", context.Permissions?.GetEffectiveGroups(targetId) ?? Array.Empty<string>());
            string directGroups = string.Join(", ", record?.Groups ?? Enumerable.Empty<string>());
            string directAllow = string.Join(", ", record?.Allow ?? Enumerable.Empty<string>());
            string directDeny = string.Join(", ", record?.Deny ?? Enumerable.Empty<string>());

            context.Reply($"Subject: {targetId}");
            context.Reply($"Effective groups: {(string.IsNullOrWhiteSpace(effectiveGroups) ? "(none)" : effectiveGroups)}");
            context.Reply($"Direct groups: {(string.IsNullOrWhiteSpace(directGroups) ? "(none)" : directGroups)}");
            context.Reply($"Direct allow: {(string.IsNullOrWhiteSpace(directAllow) ? "(none)" : directAllow)}");
            context.Reply($"Direct deny: {(string.IsNullOrWhiteSpace(directDeny) ? "(none)" : directDeny)}");
        }

        private void ExecuteGrant(CommandContext context, bool isDeny)
        {
            string requiredNode = isDeny ? PermissionBuiltIns.Nodes.PermissionsDeny : PermissionBuiltIns.Nodes.PermissionsGrant;
            if (context.Executor != null && context.Permissions?.HasPermission(context.Executor.TrustedUniqueId, requiredNode) != true)
            {
                context.ReplyError("You do not have permission to modify direct nodes.");
                return;
            }

            if (context.Arguments.Count < 3)
            {
                context.ReplyError($"Usage: perm {(isDeny ? "deny" : "grant")} <player_or_steamid> <node>");
                return;
            }

            string targetId = ResolveTargetId(context.Arguments[1]);
            string node = context.Arguments[2];
            bool changed = isDeny
                ? context.Permissions?.DenyNode(context.Executor?.TrustedUniqueId, targetId, node, "perm deny") == true
                : context.Permissions?.GrantNode(context.Executor?.TrustedUniqueId, targetId, node, "perm grant") == true;

            if (changed)
            {
                context.Reply($"{(isDeny ? "Denied" : "Granted")} '{node}' for '{targetId}'.");
                return;
            }

            context.ReplyError($"Failed to {(isDeny ? "deny" : "grant")} '{node}' for '{targetId}'.");
        }

        private void ExecuteRevoke(CommandContext context)
        {
            if (context.Executor != null && context.Permissions?.HasPermission(context.Executor.TrustedUniqueId, PermissionBuiltIns.Nodes.PermissionsRevoke) != true)
            {
                context.ReplyError("You do not have permission to revoke direct nodes.");
                return;
            }

            if (context.Arguments.Count < 3)
            {
                context.ReplyError("Usage: perm revoke <player_or_steamid> <node>");
                return;
            }

            string targetId = ResolveTargetId(context.Arguments[1]);
            string node = context.Arguments[2];
            if (context.Permissions?.RevokeNode(context.Executor?.TrustedUniqueId, targetId, node, "perm revoke") == true)
            {
                context.Reply($"Revoked '{node}' for '{targetId}'.");
                return;
            }

            context.ReplyError($"Failed to revoke '{node}' for '{targetId}'.");
        }

        private void ExecuteTemporaryGrant(CommandContext context)
        {
            if (context.Executor != null && context.Permissions?.HasPermission(context.Executor.TrustedUniqueId, PermissionBuiltIns.Nodes.PermissionsTempGrant) != true)
            {
                context.ReplyError("You do not have permission to grant temporary nodes.");
                return;
            }

            if (context.Arguments.Count < 4)
            {
                context.ReplyError("Usage: perm tempgrant <player_or_steamid> <node> <minutes> [reason]");
                return;
            }

            string targetId = ResolveTargetId(context.Arguments[1]);
            string node = context.Arguments[2];
            if (!double.TryParse(context.Arguments[3], out double minutes) || minutes <= 0)
            {
                context.ReplyError("Temporary grant duration must be a positive number of minutes.");
                return;
            }

            string reason = context.Arguments.Count > 4
                ? string.Join(" ", context.Arguments.Skip(4))
                : "Temporary permission grant";

            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(minutes);
            if (context.Permissions?.GrantTemporaryNode(context.Executor?.TrustedUniqueId, targetId, node, expiresAtUtc, reason) == true)
            {
                context.Reply($"Temporarily granted '{node}' to '{targetId}' until {expiresAtUtc:O}.");
                return;
            }

            context.ReplyError($"Failed to temporarily grant '{node}' to '{targetId}'.");
        }

        private string ResolveTargetId(string identifier)
        {
            ConnectedPlayerInfo player = FindPlayerByNameOrId(identifier);
            return player?.TrustedUniqueId ?? identifier;
        }
    }
}
