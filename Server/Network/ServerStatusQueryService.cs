using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using MelonLoader;
using DedicatedServerMod.API;
using Newtonsoft.Json;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Shared;
using DedicatedServerMod.Shared.Configuration;

namespace DedicatedServerMod.Server.Network
{
    /// <summary>
    /// Hosts a lightweight TCP status endpoint for server browser metadata queries.
    /// </summary>
    public sealed class ServerStatusQueryService
    {
        private const string StatusRequestCommand = "DS_STATUS";

        private readonly MelonLogger.Instance _logger;
        private readonly PlayerManager _playerManager;
        private readonly List<StatusQueryRegistrationEntry> _registrations = new List<StatusQueryRegistrationEntry>();
        private TcpListener _listener;
        private Thread _listenerThread;
        private CancellationTokenSource _cancellation;
        private long _nextRegistrationOrder;

        internal ServerStatusQueryService(MelonLogger.Instance logger, PlayerManager playerManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _playerManager = playerManager ?? throw new ArgumentNullException(nameof(playerManager));
        }

        public void Start()
        {
            if (_listener != null)
            {
                return;
            }

            int port = ServerConfig.Instance.ServerPort;
            _cancellation = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();

            _listenerThread = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "DedicatedServerStatusQuery"
            };
            _listenerThread.Start();

            _logger.Msg($"Status query endpoint listening on TCP {port}");
        }

        public void Shutdown()
        {
            try
            {
                _cancellation?.Cancel();
                _listener?.Stop();
            }
            catch (Exception ex)
            {
                _logger.Warning($"Status query shutdown warning: {ex.Message}");
            }
            finally
            {
                _listener = null;
                _listenerThread = null;
                _cancellation?.Dispose();
                _cancellation = null;
            }
        }

        /// <summary>
        /// Registers a handler for custom commands on the dedicated server status-query endpoint.
        /// </summary>
        /// <param name="registrationId">Stable identifier for this handler registration.</param>
        /// <param name="configure">Fluent handler builder.</param>
        /// <returns>A disposable registration handle.</returns>
        public ServerStatusQueryRegistration RegisterHandler(string registrationId, Action<ServerStatusQueryHandlerBuilder> configure)
        {
            if (string.IsNullOrWhiteSpace(registrationId))
                throw new ArgumentException("Registration id cannot be empty.", nameof(registrationId));
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            ServerStatusQueryHandlerBuilder builder = new ServerStatusQueryHandlerBuilder(registrationId);
            configure(builder);

            StatusQueryRegistrationEntry entry = new StatusQueryRegistrationEntry(
                Guid.NewGuid(),
                builder.RegistrationId,
                builder.Priority,
                _nextRegistrationOrder++,
                builder.HandlerCallback);

            lock (_registrations)
            {
                _registrations.RemoveAll(existing =>
                    string.Equals(existing.RegistrationId, builder.RegistrationId, StringComparison.OrdinalIgnoreCase));
                _registrations.Add(entry);
            }

            return new ServerStatusQueryRegistration(this, entry.Token, entry.RegistrationId);
        }

        internal void UnregisterHandler(Guid token)
        {
            lock (_registrations)
            {
                _registrations.RemoveAll(entry => entry.Token == token);
            }
        }

        private void ListenLoop()
        {
            while (_cancellation != null && !_cancellation.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(_ => HandleClient(client));
                }
                catch (SocketException)
                {
                    if (_cancellation == null || _cancellation.IsCancellationRequested)
                    {
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warning($"Status query accept error: {ex.Message}");
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            using (client)
            {
                try
                {
                    client.ReceiveTimeout = 15000;
                    client.SendTimeout = 15000;

                    using (NetworkStream stream = client.GetStream())
                    using (var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true))
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true) { AutoFlush = true })
                    {
                        string request = reader.ReadLine();
                        if (string.Equals(request, StatusRequestCommand, StringComparison.Ordinal))
                        {
                            ServerStatusSnapshot snapshot = BuildSnapshot();
                            writer.WriteLine(JsonConvert.SerializeObject(snapshot));
                            return;
                        }

                        if (TryWriteExtendedResponse(request, writer))
                        {
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warning($"Status query client error: {ex.Message}");
                }
            }
        }

        private ServerStatusSnapshot BuildSnapshot()
        {
            ServerConfig config = ServerConfig.Instance;
            int currentPlayers = _playerManager.GetVisiblePlayerCount();

            return new ServerStatusSnapshot
            {
                ServerName = config.ServerName,
                ServerDescription = config.ServerDescription,
                CurrentPlayers = currentPlayers,
                MaxPlayers = config.MaxPlayers
            };
        }

        private bool TryWriteExtendedResponse(string request, StreamWriter writer)
        {
            if (string.IsNullOrWhiteSpace(request))
            {
                return false;
            }

            foreach (StatusQueryRegistrationEntry entry in GetRegistrationSnapshot())
            {
                try
                {
                    ServerStatusQueryContext context = new ServerStatusQueryContext(request);
                    entry.HandlerCallback?.Invoke(context);
                    if (!context.IsHandled)
                    {
                        continue;
                    }

                    writer.WriteLine(context.ResponseLine ?? string.Empty);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.Warning($"Status query extension error from '{entry.RegistrationId}': {ex.Message}");
                }
            }

            return false;
        }

        private List<StatusQueryRegistrationEntry> GetRegistrationSnapshot()
        {
            lock (_registrations)
            {
                return _registrations
                    .OrderByDescending(entry => entry.Priority)
                    .ThenBy(entry => entry.Order)
                    .ToList();
            }
        }

        private sealed class StatusQueryRegistrationEntry
        {
            public StatusQueryRegistrationEntry(Guid token, string registrationId, int priority, long order, Action<ServerStatusQueryContext> handlerCallback)
            {
                Token = token;
                RegistrationId = registrationId;
                Priority = priority;
                Order = order;
                HandlerCallback = handlerCallback;
            }

            public Guid Token { get; }

            public string RegistrationId { get; }

            public int Priority { get; }

            public long Order { get; }

            public Action<ServerStatusQueryContext> HandlerCallback { get; }
        }
    }
}
