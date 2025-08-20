using MelonLoader;
using System;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Server.Core;
using DedicatedServerMod;

namespace DedicatedServerMod.Server.Commands.Server
{
    /// <summary>
    /// Command to manually trigger a save
    /// </summary>
    public class SaveCommand : BaseServerCommand
    {
        public SaveCommand(MelonLogger.Instance logger, PlayerManager playerMgr) 
            : base(logger, playerMgr)
        {
        }

        public override string CommandWord => "save";
        public override string Description => "Manually triggers a game save";
        public override string Usage => "save";
        public override PermissionLevel RequiredPermission => PermissionLevel.Operator;

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
                    logger.Msg($"Manual save triggered by {context.Executor?.DisplayName ?? "Console"}");
                }
                else
                {
                    context.ReplyError("Persistence manager not available");
                }
            }
            catch (Exception ex)
            {
                context.ReplyError($"Failed to trigger save: {ex.Message}");
                logger.Error($"Error triggering manual save: {ex}");
            }
        }
    }
}
