using System.Collections.Generic;
using MelonLoader;
using DedicatedServerMod.Server.Player;

namespace DedicatedServerMod.Server.Commands
{
    /// <summary>
    /// Interface for all server commands
    /// </summary>
    public interface IServerCommand
    {
        /// <summary>
        /// The command word used to invoke this command
        /// </summary>
        string CommandWord { get; }

        /// <summary>
        /// Description of what the command does
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Usage example for the command
        /// </summary>
        string Usage { get; }

        /// <summary>
        /// Required permission level to execute this command
        /// </summary>
        PermissionLevel RequiredPermission { get; }

        /// <summary>
        /// Execute the command with the given context
        /// </summary>
        void Execute(CommandContext context);
    }

    /// <summary>
    /// Context information for command execution
    /// </summary>
    public class CommandContext
    {
        /// <summary>
        /// The player executing the command (null for console)
        /// </summary>
        public ConnectedPlayerInfo Executor { get; set; }

        /// <summary>
        /// Command arguments
        /// </summary>
        public List<string> Arguments { get; set; }

        /// <summary>
        /// Logger instance
        /// </summary>
        public MelonLogger.Instance Logger { get; set; }

        /// <summary>
        /// Player manager
        /// </summary>
        public PlayerManager PlayerManager { get; set; }

        /// <summary>
        /// Optional sinks for console output redirection (e.g., TCP console)
        /// </summary>
        public System.Action<string> OutputInfo { get; set; }
        public System.Action<string> OutputWarning { get; set; }
        public System.Action<string> OutputError { get; set; }

        /// <summary>
        /// Whether the command is being executed from console
        /// </summary>
        public bool IsConsoleExecution => Executor == null;

        /// <summary>
        /// Send a message back to the executor
        /// </summary>
        public void Reply(string message)
        {
            if (IsConsoleExecution)
            {
                OutputInfo?.Invoke(message);
                Logger?.Msg($"[COMMAND] {message}");
                ScheduleOne.Console.Log(message);
            }
            else
            {
                Logger?.Msg($"[COMMAND -> {Executor.DisplayName}] {message}");
            }
        }

        /// <summary>
        /// Send a warning message back to the executor
        /// </summary>
        public void ReplyWarning(string message)
        {
            if (IsConsoleExecution)
            {
                OutputWarning?.Invoke(message);
                Logger?.Warning($"[COMMAND] {message}");
                ScheduleOne.Console.LogWarning(message);
            }
            else
            {
                Logger?.Warning($"[COMMAND -> {Executor.DisplayName}] {message}");
            }
        }

        /// <summary>
        /// Send an error message back to the executor
        /// </summary>
        public void ReplyError(string message)
        {
            if (IsConsoleExecution)
            {
                OutputError?.Invoke(message);
                Logger?.Error($"[COMMAND] {message}");
                ScheduleOne.Console.LogError(message);
            }
            else
            {
                Logger?.Error($"[COMMAND -> {Executor.DisplayName}] {message}");
            }
        }
    }

    /// <summary>
    /// Base class for server commands with common functionality
    /// </summary>
    public abstract class BaseServerCommand : IServerCommand
    {
        protected readonly MelonLogger.Instance logger;
        protected readonly PlayerManager playerManager;

        protected BaseServerCommand(MelonLogger.Instance loggerInstance, PlayerManager playerMgr)
        {
            logger = loggerInstance;
            playerManager = playerMgr;
        }

        public abstract string CommandWord { get; }
        public abstract string Description { get; }
        public abstract string Usage { get; }
        public abstract PermissionLevel RequiredPermission { get; }

        public abstract void Execute(CommandContext context);

        /// <summary>
        /// Find a player by name or identifier
        /// </summary>
        protected ConnectedPlayerInfo FindPlayerByNameOrId(string identifier)
        {
            // Try exact match first
            var player = playerManager.GetPlayerBySteamId(identifier);
            if (player != null) return player;

            // Try by name
            player = playerManager.GetPlayerByName(identifier);
            if (player != null) return player;

            // Try by client ID
            if (int.TryParse(identifier, out int clientId))
            {
                return playerManager.GetConnectedPlayers().Find(p => p.ClientId == clientId);
            }

            return null;
        }

        /// <summary>
        /// Validate that the required number of arguments are provided
        /// </summary>
        protected bool ValidateArguments(CommandContext context, int requiredCount, string usageMessage = null)
        {
            if (context.Arguments.Count < requiredCount)
            {
                context.ReplyError($"Usage: {usageMessage ?? Usage}");
                return false;
            }
            return true;
        }
    }
}
