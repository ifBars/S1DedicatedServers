using System.Globalization;
using DedicatedServerMod.Server.Commands.Contracts;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Shared.Permissions;
using UnityEngine;

namespace DedicatedServerMod.Server.Commands.BuiltIn.Gameplay
{
    /// <summary>
    /// Sets Unity's global time scale for the dedicated server process.
    /// </summary>
    /// <remarks>
    /// This is an operator diagnostic and recovery command, not the normal way to tune game time.
    /// Persistent game-time tuning should use the gameplay configuration instead.
    /// </remarks>
    public class SetTimeScaleCommand(PlayerManager playerMgr) : BaseServerCommand(playerMgr)
    {
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
        }

        private static bool TryParseScale(string rawValue, out float scale)
        {
            return float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out scale) ||
                   float.TryParse(rawValue, NumberStyles.Float, CultureInfo.CurrentCulture, out scale);
        }
    }
}
