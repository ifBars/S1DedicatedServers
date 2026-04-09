using System.Collections.Generic;
using DedicatedServerMod.Server.Commands.Contracts;
using DedicatedServerMod.Server.Commands.Output;
using DedicatedServerMod.Shared.ConsoleSupport;
#if IL2CPP
using Console = Il2CppScheduleOne.Console;
#else
using Console = ScheduleOne.Console;
#endif

namespace DedicatedServerMod.Server.Commands
{
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
                _commandManager.ExecuteConsoleLine(commandLine, CommandReplyChannelFactory.CreateGameConsole());
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
