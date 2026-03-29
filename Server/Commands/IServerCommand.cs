using System.Collections.Generic;
using DedicatedServerMod.Server.Player;
using MelonLoader;

namespace DedicatedServerMod.Server.Commands
{
    /// <summary>
    /// Interface for all server commands.
    /// </summary>
    public interface IServerCommand
    {
        /// <summary>
        /// The command word used to invoke this command.
        /// </summary>
        string CommandWord { get; }

        /// <summary>
        /// Description of what the command does.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Usage example for the command.
        /// </summary>
        string Usage { get; }

        /// <summary>
        /// Required permission level to execute this command.
        /// </summary>
        PermissionLevel RequiredPermission { get; }

        /// <summary>
        /// Execute the command with the given context.
        /// </summary>
        void Execute(CommandContext context);
    }

    /// <summary>
    /// Context information for command execution.
    /// </summary>
    public class CommandContext
    {
        /// <summary>
        /// The player executing the command (null for console).
        /// </summary>
        public ConnectedPlayerInfo Executor { get; set; }

        /// <summary>
        /// Command arguments.
        /// </summary>
        public List<string> Arguments { get; set; }

        /// <summary>
        /// Logger instance.
        /// </summary>
        public MelonLogger.Instance Logger { get; set; }

        /// <summary>
        /// Player manager.
        /// </summary>
        public PlayerManager PlayerManager { get; set; }

        /// <summary>
        /// Optional transport-specific output sink.
        /// </summary>
        public ICommandOutput Output { get; set; }

        /// <summary>
        /// Whether the command is being executed from console.
        /// </summary>
        public bool IsConsoleExecution => Executor == null;

        /// <summary>
        /// Send a message back to the executor.
        /// </summary>
        public void Reply(string message)
        {
            if (Output != null)
            {
                Output.WriteInfo(message);
                return;
            }

            if (IsConsoleExecution)
            {
                Logger?.Msg($"[COMMAND] {message}");
            }
            else
            {
                Logger?.Msg($"[COMMAND -> {Executor.DisplayName}] {message}");
            }
        }

        /// <summary>
        /// Send a warning message back to the executor.
        /// </summary>
        public void ReplyWarning(string message)
        {
            if (Output != null)
            {
                Output.WriteWarning(message);
                return;
            }

            if (IsConsoleExecution)
            {
                Logger?.Warning($"[COMMAND] {message}");
            }
            else
            {
                Logger?.Warning($"[COMMAND -> {Executor.DisplayName}] {message}");
            }
        }

        /// <summary>
        /// Send an error message back to the executor.
        /// </summary>
        public void ReplyError(string message)
        {
            if (Output != null)
            {
                Output.WriteError(message);
                return;
            }

            if (IsConsoleExecution)
            {
                Logger?.Error($"[COMMAND] {message}");
            }
            else
            {
                Logger?.Error($"[COMMAND -> {Executor.DisplayName}] {message}");
            }
        }
    }

    /// <summary>
    /// Base class for server commands with common functionality.
    /// </summary>
    public abstract class BaseServerCommand : IServerCommand
    {
        /// <summary>
        /// The logger instance shared by commands.
        /// </summary>
        protected readonly MelonLogger.Instance Logger;

        /// <summary>
        /// The player manager shared by commands.
        /// </summary>
        protected readonly PlayerManager PlayerManager;

        /// <summary>
        /// Initializes a new server command base instance.
        /// </summary>
        protected BaseServerCommand(MelonLogger.Instance loggerInstance, PlayerManager playerMgr)
        {
            Logger = loggerInstance;
            PlayerManager = playerMgr;
        }

        /// <inheritdoc />
        public abstract string CommandWord { get; }

        /// <inheritdoc />
        public abstract string Description { get; }

        /// <inheritdoc />
        public abstract string Usage { get; }

        /// <inheritdoc />
        public abstract PermissionLevel RequiredPermission { get; }

        /// <inheritdoc />
        public abstract void Execute(CommandContext context);

        /// <summary>
        /// Find a player by name or identifier.
        /// </summary>
        protected ConnectedPlayerInfo FindPlayerByNameOrId(string identifier)
        {
            ConnectedPlayerInfo player = PlayerManager.GetPlayerBySteamId(identifier);
            if (player != null)
            {
                return player;
            }

            player = PlayerManager.GetPlayerByName(identifier);
            if (player != null)
            {
                return player;
            }

            if (int.TryParse(identifier, out int clientId))
            {
                return PlayerManager.GetConnectedPlayers().Find(p => p.ClientId == clientId);
            }

            return null;
        }

        /// <summary>
        /// Validate that the required number of arguments are provided.
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
