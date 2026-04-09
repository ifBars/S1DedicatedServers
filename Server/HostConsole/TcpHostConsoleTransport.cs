using System.Net;
using System.Net.Sockets;
using System.Text;
using DedicatedServerMod.Server.Commands;
using DedicatedServerMod.Server.Commands.Output;
using DedicatedServerMod.Utils;
using MelonLoader;

namespace DedicatedServerMod.Server.HostConsole
{
    /// <summary>
    /// Lightweight TCP line-based host console transport compatible with telnet.
    /// </summary>
    internal sealed class TcpHostConsoleTransport : IHostConsoleTransport
    {
        private readonly string _bindAddress;
        private readonly int _port;
        private readonly int _maxConnections;
        private readonly string _passwordOrNull;
        private readonly CommandManager _commandManager;
        private readonly MelonLogger.Instance _logger;
        private TcpListener _listener;
        private Thread _acceptThread;
        private int _activeClientCount;
        private volatile bool _isRunning;

        /// <summary>
        /// Initializes a new TCP host console transport instance.
        /// </summary>
        public TcpHostConsoleTransport(string bindAddress, int port, int maxConnections, string passwordOrNull, CommandManager commandManager, MelonLogger.Instance logger)
        {
            _bindAddress = string.IsNullOrWhiteSpace(bindAddress) ? Constants.DefaultTcpConsoleBindAddress : bindAddress;
            _port = port <= 0 ? Constants.DefaultTcpConsolePort : port;
            _maxConnections = maxConnections > 0 ? maxConnections : Constants.DefaultTcpConsoleMaxConnections;
            _passwordOrNull = string.IsNullOrWhiteSpace(passwordOrNull) ? null : passwordOrNull;
            _commandManager = commandManager ?? throw new ArgumentNullException(nameof(commandManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public string Name => "tcp";

        /// <inheritdoc />
        public void Start()
        {
            if (_isRunning)
            {
                return;
            }

            if (!IsLoopbackBindAddress(_bindAddress))
            {
                _logger.Warning("TCP console is bound to a non-loopback address. Keep this interface LAN-only and prefer loopback unless you are deliberately securing access another way.");
                if (_passwordOrNull == null)
                {
                    _logger.Warning("TCP console is exposed beyond loopback without a password requirement. This is not recommended.");
                }
            }

            _listener = new TcpListener(IPAddress.Parse(_bindAddress), _port);
            _listener.Start();
            _isRunning = true;
            _acceptThread = new Thread(AcceptLoop)
            {
                IsBackground = true,
                Name = "HostConsole-tcp-accept"
            };
            _acceptThread.Start();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            try
            {
                _isRunning = false;
                _listener?.Stop();
            }
            catch
            {
            }
        }

        private void AcceptLoop()
        {
            try
            {
                while (_isRunning)
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    ConfigureClient(client);

                    int currentClientCount = Interlocked.Increment(ref _activeClientCount);
                    if (currentClientCount > _maxConnections)
                    {
                        Interlocked.Decrement(ref _activeClientCount);
                        _logger.Warning($"TCP console rejected a connection because the max client limit of {_maxConnections} was reached.");
                        RejectClient(client, $"TCP console connection limit reached ({_maxConnections}).\r\n");
                        continue;
                    }

                    Thread clientThread = new Thread(() => HandleClient(client))
                    {
                        IsBackground = true,
                        Name = "HostConsole-tcp-client"
                    };
                    clientThread.Start();
                }
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                if (_isRunning)
                {
                    _logger.Warning($"TCP console accept loop error: {ex.Message}");
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            using (client)
            {
                try
                {
                    NetworkStream stream = client.GetStream();
                    Encoding encoding = Encoding.UTF8;

                    void WriteRaw(string value)
                    {
                        byte[] data = encoding.GetBytes(value);
                        stream.Write(data, 0, data.Length);
                    }

                    void WriteLine(string value)
                    {
                        string line = value ?? string.Empty;
                        if (!line.EndsWith("\r\n", StringComparison.Ordinal))
                        {
                            line += "\r\n";
                        }

                        WriteRaw(line);
                    }

                    WriteLine("ScheduleOne Dedicated Server Console");
                    WriteLine($"Type 'help' for commands. Connected at {DateTime.Now}.");
                    if (_passwordOrNull != null)
                    {
                        WriteRaw("Password: ");
                        string password = ReadLine(stream, encoding);
                        if (!string.Equals(password ?? string.Empty, _passwordOrNull, StringComparison.Ordinal))
                        {
                            WriteLine("Authentication failed.");
                            return;
                        }

                        WriteLine("Authenticated.");
                    }

                    ICommandReplyChannel output = CommandReplyChannelFactory.CreateTcp(WriteLine);
                    while (_isRunning && client.Connected)
                    {
                        WriteRaw("> ");
                        string line = ReadLine(stream, encoding);
                        if (line == null)
                        {
                            break;
                        }

                        string trimmedLine = line.Trim();
                        if (trimmedLine.Length == 0)
                        {
                            WriteLine(string.Empty);
                            continue;
                        }

                        if (trimmedLine.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                            trimmedLine.Equals("quit", StringComparison.OrdinalIgnoreCase))
                        {
                            WriteLine("Goodbye.");
                            break;
                        }

                        _commandManager.ExecuteConsoleLine(trimmedLine, output);
                    }
                }
                catch (IOException)
                {
                }
                finally
                {
                    Interlocked.Decrement(ref _activeClientCount);
                }
            }
        }

        private static bool IsLoopbackBindAddress(string candidate)
        {
            string normalizedAddress = string.IsNullOrWhiteSpace(candidate) ? Constants.DefaultTcpConsoleBindAddress : candidate.Trim();
            if (string.Equals(normalizedAddress, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!IPAddress.TryParse(normalizedAddress, out IPAddress address))
            {
                return false;
            }

            return IPAddress.IsLoopback(address);
        }

        private static void ConfigureClient(TcpClient client)
        {
            client.SendTimeout = Constants.TcpConsoleSocketTimeoutMs;
            client.NoDelay = true;

            NetworkStream stream = client.GetStream();
            stream.WriteTimeout = Constants.TcpConsoleSocketTimeoutMs;
        }

        private static void RejectClient(TcpClient client, string message)
        {
            using (client)
            {
                try
                {
                    NetworkStream stream = client.GetStream();
                    byte[] data = Encoding.UTF8.GetBytes(message);
                    stream.Write(data, 0, data.Length);
                }
                catch
                {
                }
            }
        }

        private static string ReadLine(NetworkStream stream, Encoding encoding)
        {
            StringBuilder builder = new StringBuilder();
            byte[] buffer = new byte[1];
            while (true)
            {
                int read;
                try
                {
                    read = stream.Read(buffer, 0, 1);
                }
                catch
                {
                    return null;
                }

                if (read <= 0)
                {
                    return null;
                }

                char current = encoding.GetChars(buffer, 0, read)[0];
                if (current == '\n')
                {
                    break;
                }

                if (current == '\r')
                {
                    continue;
                }

                builder.Append(current);
            }

            return builder.ToString();
        }
    }
}
