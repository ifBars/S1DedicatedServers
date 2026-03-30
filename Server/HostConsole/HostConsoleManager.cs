using DedicatedServerMod.Server.Commands;
using DedicatedServerMod.Shared.Configuration;
using MelonLoader;

namespace DedicatedServerMod.Server.HostConsole
{
    /// <summary>
    /// Owns server host console transports such as TCP and stdio.
    /// </summary>
    public sealed class HostConsoleManager : IDisposable
    {
        private readonly CommandManager _commandManager;
        private readonly MelonLogger.Instance _logger;
        private readonly List<IHostConsoleTransport> _transports;

        /// <summary>
        /// Initializes a new host console manager.
        /// </summary>
        public HostConsoleManager(CommandManager commandManager, MelonLogger.Instance logger)
        {
            _commandManager = commandManager ?? throw new ArgumentNullException(nameof(commandManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _transports = new List<IHostConsoleTransport>();
        }

        /// <summary>
        /// Starts all configured host console transports.
        /// </summary>
        public void Start()
        {
            ServerConfig config = ServerConfig.Instance;
            TryStartTcpTransport(config);
            TryStartStdioTransport(config);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            for (int i = _transports.Count - 1; i >= 0; i--)
            {
                try
                {
                    _transports[i].Dispose();
                }
                catch (Exception ex)
                {
                    _logger.Warning($"Host console transport '{_transports[i].Name}' failed to dispose cleanly: {ex.Message}");
                }
            }

            _transports.Clear();
        }

        private void TryStartTcpTransport(ServerConfig config)
        {
            if (!config.TcpConsoleEnabled)
            {
                return;
            }

            try
            {
                bool hasPassword = config.TcpConsoleRequirePassword && !string.IsNullOrWhiteSpace(config.TcpConsolePassword);
                TcpHostConsoleTransport transport = new TcpHostConsoleTransport(
                    config.TcpConsoleBindAddress,
                    config.TcpConsolePort,
                    config.TcpConsoleMaxConnections,
                    hasPassword ? config.TcpConsolePassword : null,
                    _commandManager,
                    _logger);

                transport.Start();
                _transports.Add(transport);
                _logger.Msg($"Host console transport started: {transport.Name} ({config.TcpConsoleBindAddress}:{config.TcpConsolePort})");
            }
            catch (Exception ex)
            {
                _logger.Warning($"TCP host console failed to start: {ex.Message}");
            }
        }

        private void TryStartStdioTransport(ServerConfig config)
        {
            if (!ShouldStartStdioTransport(config.StdioConsoleMode))
            {
                return;
            }

            try
            {
                StdioHostConsoleTransport transport = new StdioHostConsoleTransport(_commandManager, _logger);
                transport.Start();
                _transports.Add(transport);
                _logger.Msg($"Host console transport started: {transport.Name}");
            }
            catch (Exception ex)
            {
                _logger.Warning($"stdio host console failed to start: {ex.Message}");
            }
        }

        private bool ShouldStartStdioTransport(StdioConsoleMode mode)
        {
            switch (mode)
            {
                case StdioConsoleMode.Disabled:
                    return false;
                case StdioConsoleMode.Enabled:
                    return true;
                case StdioConsoleMode.Auto:
                    return IsInputRedirected();
                default:
                    return IsInputRedirected();
            }
        }

        private bool IsInputRedirected()
        {
            try
            {
                return Console.IsInputRedirected;
            }
            catch (Exception ex)
            {
                _logger.Warning($"Unable to determine stdin redirection state: {ex.Message}");
                return false;
            }
        }
    }
}
