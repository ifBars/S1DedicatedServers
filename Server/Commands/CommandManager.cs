using DedicatedServerMod.Server.Commands.BuiltIn.Gameplay;
using DedicatedServerMod.Server.Commands.BuiltIn.Moderation;
using DedicatedServerMod.Server.Commands.BuiltIn.Permissions;
using DedicatedServerMod.Server.Commands.BuiltIn.System;
using DedicatedServerMod.Server.Commands.Contracts;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Commands.Output;
using DedicatedServerMod.Server.Network;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Server.Permissions;
using DedicatedServerMod.Shared.ConsoleSupport;
using DedicatedServerMod.Shared.Networking;
using DedicatedServerMod.Utils;

namespace DedicatedServerMod.Server.Commands
{
    /// <summary>
    /// Manages server commands including registration, execution, and permissions.
    /// </summary>
    public class CommandManager
    {
        private readonly PlayerManager playerManager;
        private readonly NetworkManager networkManager;
        private readonly ServerPermissionService permissionService;
        private readonly Dictionary<string, IServerCommand> serverCommands;

        /// <summary>
        /// Initializes a new command manager.
        /// </summary>
        public CommandManager(PlayerManager playerMgr, NetworkManager networkMgr)
        {
            playerManager = playerMgr;
            networkManager = networkMgr;
            permissionService = Core.ServerBootstrap.Permissions;
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
                DebugLog.StartupDebug("Command manager initialized");
            }
            catch (Exception ex)
            {
                DebugLog.Error("Failed to initialize command manager", ex);
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
                DebugLog.Warning($"Player {executor?.DisplayName ?? "Console"} lacks permission for command '{commandLine.CommandWord}'");
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
                    PlayerManager = playerManager,
                    Permissions = permissionService,
                    Output = output
                };

                command.Execute(context);
                return new CommandExecutionResult(CommandExecutionStatus.Success, commandLine.CommandWord, string.Empty);
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error executing command '{commandLine.CommandWord}'", ex);
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
        }

        private void RegisterServerCommands()
        {
            RegisterCommand(new ReloadPermissionsCommand(playerManager));
            RegisterCommand(new PermissionCommand(playerManager));
            RegisterCommand(new GroupCommand(playerManager));
            RegisterCommand(new OpCommand(playerManager));
            RegisterCommand(new DeopCommand(playerManager));
            RegisterCommand(new AdminCommand(playerManager));
            RegisterCommand(new DeadminCommand(playerManager));
            RegisterCommand(new ListOpsCommand(playerManager));
            RegisterCommand(new ListAdminsCommand(playerManager));
            RegisterCommand(new KickCommand(playerManager));
            RegisterCommand(new BanCommand(playerManager));
            RegisterCommand(new UnbanCommand(playerManager));
            RegisterCommand(new ListPlayersCommand(playerManager));
            RegisterCommand(new HelpCommand(playerManager, this));
            RegisterCommand(new ServerInfoCommand(playerManager, networkManager));
            RegisterCommand(new ReloadConfigCommand(playerManager));
            RegisterCommand(new SaveCommand(playerManager));
            RegisterCommand(new SetTimeCommand(playerManager));
            RegisterCommand(new SetTimeScaleCommand(playerManager));
            RegisterCommand(new ShutdownCommand(playerManager));

            DebugLog.StartupDebug($"Registered {serverCommands.Count} server commands");
        }

        private void RegisterCommand(IServerCommand command)
        {
            try
            {
                if (serverCommands.ContainsKey(command.CommandWord))
                {
                    DebugLog.Warning($"Command '{command.CommandWord}' already registered, skipping");
                    return;
                }

                serverCommands[command.CommandWord] = command;
                DebugLog.StartupDebug($"Registered command: {command.CommandWord}");
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error registering command '{command?.CommandWord}'", ex);
            }
        }

        private void IntegrateWithGameConsole()
        {
            try
            {
                if (GameConsoleAccess.TryGetCommandDictionary(out var gameCommands))
                {
                    if (!gameCommands.ContainsKey("settime") || !gameCommands.ContainsKey("give"))
                    {
                        CustomMessaging.InitializeConsoleCommands();
                        DebugLog.StartupDebug("Initialized base game console commands");
                    }

                    foreach (IServerCommand serverCommand in serverCommands.Values)
                    {
                        if (!gameCommands.ContainsKey(serverCommand.CommandWord))
                        {
                            gameCommands[serverCommand.CommandWord] = new ConsoleCommandAdapter(this, serverCommand);
                        }
                    }
                }
                else
                {
                    DebugLog.Warning("Could not access game console commands dictionary");
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error("Error integrating with game console", ex);
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
}
