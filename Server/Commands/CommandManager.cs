using System;
using System.Collections.Generic;
using System.Reflection;
using DedicatedServerMod.Server.Commands.Admin;
using DedicatedServerMod.Server.Commands.Server;
using DedicatedServerMod.Server.Network;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Server.Permissions;
using DedicatedServerMod.Shared;
using DedicatedServerMod.Shared.ConsoleSupport;
using DedicatedServerMod.Shared.Networking;
using MelonLoader;
#if IL2CPP
using Il2CppScheduleOne;
using Console = Il2CppScheduleOne.Console;
#else
using ScheduleOne;
using Console = ScheduleOne.Console;
#endif

namespace DedicatedServerMod.Server.Commands
{
    /// <summary>
    /// Manages server commands including registration, execution, and permissions.
    /// </summary>
    public class CommandManager
    {
        private readonly MelonLogger.Instance logger;
        private readonly PlayerManager playerManager;
        private readonly NetworkManager networkManager;
        private readonly ServerPermissionService permissionService;
        private readonly Dictionary<string, IServerCommand> serverCommands;

        /// <summary>
        /// Initializes a new command manager.
        /// </summary>
        public CommandManager(MelonLogger.Instance loggerInstance, PlayerManager playerMgr, NetworkManager networkMgr)
        {
            logger = loggerInstance;
            playerManager = playerMgr;
            networkManager = networkMgr;
            permissionService = DedicatedServerMod.Server.Core.ServerBootstrap.Permissions;
            serverCommands = new Dictionary<string, IServerCommand>();
        }

        /// <summary>
        /// Initialize the command manager and register all commands.
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
        /// Execute a raw console line through the shared parser and command pipeline.
        /// </summary>
        public CommandExecutionResult ExecuteConsoleLine(string rawLine, ICommandOutput output = null, ConnectedPlayerInfo executor = null)
        {
            CommandLineParseResult parseResult = CommandLineParser.TryParse(rawLine);
            if (parseResult.IsEmpty)
            {
                return new CommandExecutionResult(CommandExecutionStatus.Empty, string.Empty, string.Empty);
            }

            if (!parseResult.Success)
            {
                CommandExecutionResult result = new CommandExecutionResult(CommandExecutionStatus.ParseError, string.Empty, parseResult.ErrorMessage);
                output?.WriteError(result.Message);
                return result;
            }

            return ExecuteConsoleLine(parseResult.CommandLine, output, executor);
        }

        /// <summary>
        /// Execute an already parsed command line.
        /// </summary>
        public CommandExecutionResult ExecuteConsoleLine(ParsedCommandLine commandLine, ICommandOutput output = null, ConnectedPlayerInfo executor = null)
        {
            if (commandLine == null)
            {
                throw new ArgumentNullException(nameof(commandLine));
            }

            if (!serverCommands.TryGetValue(commandLine.CommandWord, out IServerCommand command))
            {
                CommandExecutionResult result = new CommandExecutionResult(
                    CommandExecutionStatus.UnknownCommand,
                    commandLine.CommandWord,
                    $"Unknown command: {commandLine.CommandWord}");
                output?.WriteError(result.Message);
                return result;
            }

            if (!CanExecuteCommand(executor, command, commandLine.Arguments))
            {
                logger.Warning($"Player {executor?.DisplayName ?? "Console"} lacks permission for command '{commandLine.CommandWord}'");
                CommandExecutionResult result = new CommandExecutionResult(
                    CommandExecutionStatus.Unauthorized,
                    commandLine.CommandWord,
                    $"Unauthorized command: {commandLine.CommandWord}");
                output?.WriteError(result.Message);
                return result;
            }

            try
            {
                CommandContext context = new CommandContext
                {
                    Executor = executor,
                    Arguments = new List<string>(commandLine.Arguments),
                    Logger = logger,
                    PlayerManager = playerManager,
                    Permissions = permissionService,
                    Output = output
                };

                command.Execute(context);
                return new CommandExecutionResult(CommandExecutionStatus.Success, commandLine.CommandWord, string.Empty);
            }
            catch (Exception ex)
            {
                logger.Error($"Error executing command '{commandLine.CommandWord}': {ex}");
                CommandExecutionResult result = new CommandExecutionResult(
                    CommandExecutionStatus.ExecutionFailed,
                    commandLine.CommandWord,
                    $"Error executing command '{commandLine.CommandWord}': {ex.Message}",
                    ex);
                output?.WriteError(result.Message);
                return result;
            }
        }

        /// <summary>
        /// Execute a server command by command word and arguments.
        /// </summary>
        public bool ExecuteCommand(string commandWord, List<string> args, ConnectedPlayerInfo executor = null)
        {
            ParsedCommandLine commandLine = new ParsedCommandLine(
                commandWord?.ToLowerInvariant() ?? string.Empty,
                args ?? new List<string>());

            return ExecuteConsoleLine(commandLine, output: null, executor).Succeeded;
        }

        /// <summary>
        /// Execute a server command with TCP-style output callbacks.
        /// </summary>
        public bool ExecuteCommand(string commandWord, List<string> args, Action<string> outputInfo, Action<string> outputWarning, Action<string> outputError)
        {
            ParsedCommandLine commandLine = new ParsedCommandLine(
                commandWord?.ToLowerInvariant() ?? string.Empty,
                args ?? new List<string>());

            ICommandOutput output = new DelegateCommandOutput(outputInfo, outputWarning, outputError);
            return ExecuteConsoleLine(commandLine, output).Succeeded;
        }

        /// <summary>
        /// Get all available commands for a player.
        /// </summary>
        public List<IServerCommand> GetAvailableCommands(ConnectedPlayerInfo player)
        {
            List<IServerCommand> availableCommands = new List<IServerCommand>();
            foreach (IServerCommand command in serverCommands.Values)
            {
                if (CanDiscoverCommand(player, command))
                {
                    availableCommands.Add(command);
                }
            }

            return availableCommands;
        }

        /// <summary>
        /// Get command by name.
        /// </summary>
        public IServerCommand GetCommand(string commandWord)
        {
            serverCommands.TryGetValue(commandWord.ToLowerInvariant(), out IServerCommand command);
            return command;
        }

        /// <summary>
        /// Get all registered commands.
        /// </summary>
        public Dictionary<string, IServerCommand> GetAllCommands()
        {
            return new Dictionary<string, IServerCommand>(serverCommands);
        }

        /// <summary>
        /// Shutdown the command manager.
        /// </summary>
        public void Shutdown()
        {
            serverCommands.Clear();
            logger.Msg("Command manager shutdown");
        }

        private void RegisterServerCommands()
        {
            RegisterCommand(new ReloadPermissionsCommand(logger, playerManager));
            RegisterCommand(new PermissionCommand(logger, playerManager));
            RegisterCommand(new GroupCommand(logger, playerManager));
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
            RegisterCommand(new HelpCommand(logger, playerManager, this));
            RegisterCommand(new ServerInfoCommand(logger, playerManager, networkManager));
            RegisterCommand(new ReloadConfigCommand(logger, playerManager));
            RegisterCommand(new SaveCommand(logger, playerManager));
            RegisterCommand(new ShutdownCommand(logger, playerManager));

            logger.Msg($"Registered {serverCommands.Count} server commands");
        }

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

        private void IntegrateWithGameConsole()
        {
            try
            {
                FieldInfo commandsField = typeof(Console).GetField("commands", BindingFlags.NonPublic | BindingFlags.Static);

                if (commandsField?.GetValue(null) is Dictionary<string, Console.ConsoleCommand> gameCommands)
                {
                    if (!gameCommands.ContainsKey("settime") || !gameCommands.ContainsKey("give"))
                    {
                        CustomMessaging.InitializeConsoleCommands(gameCommands);
                        logger.Msg("Initialized base game console commands");
                    }

                    foreach (IServerCommand serverCommand in serverCommands.Values)
                    {
                        if (!gameCommands.ContainsKey(serverCommand.CommandWord))
                        {
                            gameCommands[serverCommand.CommandWord] = new ConsoleCommandAdapter(this, serverCommand);
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

        private bool CanDiscoverCommand(ConnectedPlayerInfo player, IServerCommand command)
        {
            if (player == null)
            {
                return true;
            }

            IReadOnlyCollection<string> discoveryNodes = command.GetDiscoveryPermissionNodes();
            if (discoveryNodes == null || discoveryNodes.Count == 0)
            {
                return true;
            }

            foreach (string discoveryNode in discoveryNodes)
            {
                if (string.IsNullOrWhiteSpace(discoveryNode) || playerManager.Permissions.CanExecuteCommand(player, discoveryNode))
                {
                    return true;
                }
            }

            return false;
        }

        private bool CanExecuteCommand(ConnectedPlayerInfo player, IServerCommand command, IReadOnlyList<string> arguments)
        {
            if (player == null)
            {
                return true;
            }

            string requiredPermissionNode = command.GetRequiredPermissionNode(arguments ?? Array.Empty<string>());
            if (string.IsNullOrWhiteSpace(requiredPermissionNode))
            {
                return true;
            }

            return playerManager.Permissions.CanExecuteCommand(player, requiredPermissionNode);
        }
    }

    /// <summary>
    /// Adapter to integrate server commands with the game's console system.
    /// </summary>
    public class ConsoleCommandAdapter : Console.ConsoleCommand
    {
        private readonly CommandManager _commandManager;
        private readonly IServerCommand _serverCommand;

        /// <summary>
        /// Initializes a new game console adapter.
        /// </summary>
        public ConsoleCommandAdapter(CommandManager commandManager, IServerCommand serverCommand)
        {
            _commandManager = commandManager ?? throw new ArgumentNullException(nameof(commandManager));
            _serverCommand = serverCommand ?? throw new ArgumentNullException(nameof(serverCommand));
        }

        /// <inheritdoc />
        public override string CommandWord => _serverCommand.CommandWord;

        /// <inheritdoc />
        public override string CommandDescription => _serverCommand.Description;

        /// <inheritdoc />
        public override string ExampleUsage => _serverCommand.Usage;

        private void ExecuteCore(List<string> args)
        {
            try
            {
                ParsedCommandLine commandLine = new ParsedCommandLine(CommandWord.ToLowerInvariant(), args ?? new List<string>());
                _commandManager.ExecuteConsoleLine(commandLine, new GameConsoleCommandOutput());
            }
            catch (Exception ex)
            {
                Console.LogError($"Error executing command '{CommandWord}': {ex.Message}");
            }
        }

#if IL2CPP
        /// <inheritdoc />
        public override void Execute(Il2CppSystem.Collections.Generic.List<string> args)
        {
            List<string> managedArgs = new List<string>();
            if (args != null)
            {
                for (int i = 0; i < args.Count; i++)
                {
                    managedArgs.Add(args[i]);
                }
            }

            ExecuteCore(managedArgs);
        }
#else
        /// <inheritdoc />
        public override void Execute(List<string> args)
        {
            ExecuteCore(args ?? new List<string>());
        }
#endif
    }
}
