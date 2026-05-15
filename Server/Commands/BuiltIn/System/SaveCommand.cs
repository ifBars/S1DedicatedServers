using DedicatedServerMod.Server.Commands.Contracts;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Server.Core;
using DedicatedServerMod.Utils;

namespace DedicatedServerMod.Server.Commands.BuiltIn.System
{
    /// <summary>
    /// Triggers an immediate save through the server persistence manager.
    /// </summary>
    /// <remarks>
    /// This command is guarded by the dedicated <c>server.save</c> permission node. The command
    /// fails cleanly when server bootstrap or persistence initialization has not completed.
    /// </remarks>
    public class SaveCommand(PlayerManager playerMgr) : BaseServerCommand(playerMgr)
    {
        public override string CommandWord => "save";
        public override string Description => "Manually triggers a game save";
        public override string Usage => "save";
        public override string RequiredPermissionNode => Shared.Permissions.PermissionBuiltIns.Nodes.ServerSave;

        public override void Execute(CommandContext context)
        {
            try
            {
                if (!ServerBootstrap.IsInitialized)
                {
                    context.ReplyError("Server not fully initialized");
                    return;
                }

                // Trigger save through persistence manager
                if (ServerBootstrap.Persistence != null)
                {
                    ServerBootstrap.Persistence.TriggerManualSave($"manual_save_by_{context.Executor?.DisplayName ?? "console"}");
                    context.Reply("Manual save triggered successfully");
                }
                else
                {
                    context.ReplyError("Persistence manager not available");
                }
            }
            catch (Exception ex)
            {
                context.ReplyError($"Failed to trigger save: {ex.Message}");
                DebugLog.Error("Error triggering manual save", ex);
            }
        }
    }
}
