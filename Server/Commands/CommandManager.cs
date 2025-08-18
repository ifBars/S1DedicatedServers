using MelonLoader;
using System;
using System.Collections.Generic;
using System.Reflection;
using ScheduleOne;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Server.Commands.Admin;
using DedicatedServerMod.Server.Commands.Server;
using DedicatedServerMod.Shared;
using Console = ScheduleOne.Console;

namespace DedicatedServerMod.Server.Commands
{
    /// <summary>
    /// Manages server commands including registration, execution, and permissions.
    /// </summary>
    public class CommandManager
    {
        private readonly MelonLogger.Instance logger;
        private readonly PlayerManager playerManager;
        private readonly Dictionary<string, IServerCommand> serverCommands;

        public CommandManager(MelonLogger.Instance loggerInstance, PlayerManager playerMgr)
        {
            logger = loggerInstance;
            playerManager = playerMgr;
            serverCommands = new Dictionary<string, IServerCommand>();
        }

        /// <summary>
        /// Initialize the command manager and register all commands
        /// </summary>
        public void Initialize()
        {
            try
            {
                RegisterServerCommands();
                IntegrateWithGameConsole();
                logger.Msg("Command manager initialized");
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to initialize command manager: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Register all server commands
        /// </summary>
        private void RegisterServerCommands()
        {
            // Admin commands
            RegisterCommand(new OpCommand(logger, playerManager));
            RegisterCommand(new DeopCommand(logger, playerManager));
            RegisterCommand(new AdminCommand(logger, playerManager));
            RegisterCommand(new DeadminCommand(logger, playerManager));
            RegisterCommand(new ListOpsCommand(logger, playerManager));
            RegisterCommand(new ListAdminsCommand(logger, playerManager));
            RegisterCommand(new KickCommand(logger, playerManager));
            RegisterCommand(new BanCommand(logger, playerManager));
            RegisterCommand(new UnbanCommand(logger, playerManager));
            RegisterCommand(new ListPlayersCommand(logger, playerManager));

            // Server management commands
            RegisterCommand(new ServerInfoCommand(logger, playerManager));
            RegisterCommand(new ReloadConfigCommand(logger, playerManager));
            RegisterCommand(new SaveCommand(logger, playerManager));
            RegisterCommand(new ShutdownCommand(logger, playerManager));

            logger.Msg($"Registered {serverCommands.Count} server commands");
        }

        /// <summary>
        /// Register a server command
        /// </summary>
        private void RegisterCommand(IServerCommand command)
        {
            try
            {
                if (serverCommands.ContainsKey(command.CommandWord))
                {
                    logger.Warning($"Command '{command.CommandWord}' already registered, skipping");
                    return;
                }

                serverCommands[command.CommandWord] = command;
                logger.Msg($"Registered command: {command.CommandWord}");
            }
            catch (Exception ex)
            {
                logger.Error($"Error registering command '{command?.CommandWord}': {ex}");
            }
        }

        /// <summary>
        /// Integrate with the game's console system
        /// </summary>
        private void IntegrateWithGameConsole()
        {
            try
            {
                // Get the game's console commands dictionary using reflection
                var commandsField = typeof(Console).GetField("commands",
                    BindingFlags.NonPublic | BindingFlags.Static);

                if (commandsField?.GetValue(null) is Dictionary<string, Console.ConsoleCommand> gameCommands)
                {
                                    // Ensure base game commands are available
                if (!gameCommands.ContainsKey("settime") || !gameCommands.ContainsKey("give"))
                {
                    CustomMessaging.InitializeConsoleCommands(gameCommands);
                    logger.Msg("Initialized base game console commands");
                }

                    // Register our server commands as console commands
                    foreach (var serverCommand in serverCommands.Values)
                    {
                        if (!gameCommands.ContainsKey(serverCommand.CommandWord))
                        {
                            var consoleCommandAdapter = new ConsoleCommandAdapter(serverCommand);
                            gameCommands[serverCommand.CommandWord] = consoleCommandAdapter;
                        }
                    }

                    logger.Msg("Integrated server commands with game console");
                }
                else
                {
                    logger.Warning("Could not access game console commands dictionary");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error integrating with game console: {ex}");
            }
        }

        /// <summary>
        /// Execute a server command
        /// </summary>
        public bool ExecuteCommand(string commandWord, List<string> args, ConnectedPlayerInfo executor = null)
        {
            try
            {
                if (!serverCommands.TryGetValue(commandWord.ToLower(), out var command))
                {
                    return false; // Command not found
                }

                // Check permissions
                if (!CanExecuteCommand(executor, command))
                {
                    logger.Warning($"Player {executor?.DisplayName ?? "Console"} lacks permission for command '{commandWord}'");
                    return false;
                }

                // Execute the command
                var context = new CommandContext
                {
                    Executor = executor,
                    Arguments = args,
                    Logger = logger,
                    PlayerManager = playerManager
                };

                command.Execute(context);
                return true;
            }
            catch (Exception ex)
            {
                logger.Error($"Error executing command '{commandWord}': {ex}");
                return false;
            }
        }

        /// <summary>
        /// Check if a player can execute a command
        /// </summary>
        private bool CanExecuteCommand(ConnectedPlayerInfo player, IServerCommand command)
        {
            // Console (null player) can execute any command
            if (player == null)
                return true;

            // Check permission level
            return playerManager.Permissions.CanExecuteCommand(player, command.RequiredPermission);
        }

        /// <summary>
        /// Get all available commands for a player
        /// </summary>
        public List<IServerCommand> GetAvailableCommands(ConnectedPlayerInfo player)
        {
            var availableCommands = new List<IServerCommand>();

            foreach (var command in serverCommands.Values)
            {
                if (CanExecuteCommand(player, command))
                {
                    availableCommands.Add(command);
                }
            }

            return availableCommands;
        }

        /// <summary>
        /// Get command by name
        /// </summary>
        public IServerCommand GetCommand(string commandWord)
        {
            serverCommands.TryGetValue(commandWord.ToLower(), out var command);
            return command;
        }

        /// <summary>
        /// Get all registered commands
        /// </summary>
        public Dictionary<string, IServerCommand> GetAllCommands()
        {
            return new Dictionary<string, IServerCommand>(serverCommands);
        }

        /// <summary>
        /// Shutdown the command manager
        /// </summary>
        public void Shutdown()
        {
            serverCommands.Clear();
            logger.Msg("Command manager shutdown");
        }
    }

    /// <summary>
    /// Adapter to integrate server commands with the game's console system
    /// </summary>
    public class ConsoleCommandAdapter : Console.ConsoleCommand
    {
        private readonly IServerCommand serverCommand;

        public ConsoleCommandAdapter(IServerCommand command)
        {
            serverCommand = command;
        }

        public override string CommandWord => serverCommand.CommandWord;
        public override string CommandDescription => serverCommand.Description;
        public override string ExampleUsage => serverCommand.Usage;

        public override void Execute(List<string> args)
        {
            try
            {
                // Create a context for console execution (no player executor)
                var context = new CommandContext
                {
                    Executor = null, // Console execution
                    Arguments = args,
                    Logger = null, // Will be handled by the command
                    PlayerManager = null // Will be provided by command manager
                };

                serverCommand.Execute(context);
            }
            catch (Exception ex)
            {
                Console.LogError($"Error executing command '{CommandWord}': {ex.Message}");
            }
        }
    }
}
