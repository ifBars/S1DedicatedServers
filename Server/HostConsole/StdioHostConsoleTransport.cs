using DedicatedServerMod.Server.Commands;
using DedicatedServerMod.Server.Commands.Output;
using MelonLoader;

namespace DedicatedServerMod.Server.HostConsole
{
    /// <summary>
    /// Host console transport backed by process stdin/stdout.
    /// </summary>
    internal sealed class StdioHostConsoleTransport : IHostConsoleTransport
    {
        private readonly CommandManager _commandManager;
        private readonly MelonLogger.Instance _logger;
        private readonly ICommandReplyChannel _output;
        private volatile bool _isRunning;
        private Thread _readThread;

        /// <summary>
        /// Initializes a new stdio transport.
        /// </summary>
        public StdioHostConsoleTransport(CommandManager commandManager, MelonLogger.Instance logger)
        {
            _commandManager = commandManager ?? throw new ArgumentNullException(nameof(commandManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _output = CommandReplyChannelFactory.CreateStdio();
        }

        /// <inheritdoc />
        public string Name => "stdio";

        /// <inheritdoc />
        public void Start()
        {
            if (_isRunning)
            {
                return;
            }

            _isRunning = true;
            _readThread = new Thread(ReadLoop)
            {
                IsBackground = true,
                Name = "HostConsole-stdio"
            };
            _readThread.Start();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _isRunning = false;
        }

        private void ReadLoop()
        {
            try
            {
                while (_isRunning)
                {
                    string line = Console.In.ReadLine();
                    if (line == null)
                    {
                        _logger.Msg("stdio host console detached (EOF received).");
                        break;
                    }

                    _commandManager.ExecuteConsoleLine(line, _output);
                }
            }
            catch (Exception ex)
            {
                if (_isRunning)
                {
                    _logger.Warning($"stdio host console read loop stopped unexpectedly: {ex.Message}");
                }
            }
            finally
            {
                _isRunning = false;
            }
        }
    }
}
