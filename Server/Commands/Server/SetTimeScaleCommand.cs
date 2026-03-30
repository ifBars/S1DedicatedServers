using System.Globalization;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Shared.Permissions;
using MelonLoader;
using UnityEngine;

namespace DedicatedServerMod.Server.Commands.Server
{
    /// <summary>
    /// Command to set the authoritative server Unity time scale.
    /// </summary>
    public class SetTimeScaleCommand : BaseServerCommand
    {
        public SetTimeScaleCommand(MelonLogger.Instance logger, PlayerManager playerMgr)
            : base(logger, playerMgr)
        {
        }

        public override string CommandWord => "settimescale";

        public override string Description => "Sets the authoritative server Unity time scale";

        public override string Usage => "settimescale 1";

        public override string RequiredPermissionNode => PermissionNode.CreateConsoleCommandNode(CommandWord);

        public override void Execute(CommandContext context)
        {
            if (context.Arguments.Count == 0 || !TryParseScale(context.Arguments[0], out float scale) || scale < 0f)
            {
                context.ReplyError("Usage: settimescale <scale>");
                return;
            }

            scale = Mathf.Clamp(scale, 0f, 20f);
            Time.timeScale = scale;
            context.Reply($"Server time scale set to {scale:0.###}.");
            Logger.Msg($"Server time scale set to {scale:0.###} by {context.Executor?.DisplayName ?? "Console"}");
        }

        private static bool TryParseScale(string rawValue, out float scale)
        {
            return float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out scale) ||
                   float.TryParse(rawValue, NumberStyles.Float, CultureInfo.CurrentCulture, out scale);
        }
    }
}
