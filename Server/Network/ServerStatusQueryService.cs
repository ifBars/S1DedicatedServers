using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MelonLoader;
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
        private TcpListener _listener;
        private Thread _listenerThread;
        private CancellationTokenSource _cancellation;

        public ServerStatusQueryService(MelonLogger.Instance logger, PlayerManager playerManager)
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
                    client.ReceiveTimeout = 2000;
                    client.SendTimeout = 2000;

                    using (NetworkStream stream = client.GetStream())
                    using (var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true))
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true) { AutoFlush = true })
                    {
                        string request = reader.ReadLine();
                        if (!string.Equals(request, StatusRequestCommand, StringComparison.Ordinal))
                        {
                            return;
                        }

                        ServerStatusSnapshot snapshot = BuildSnapshot();
                        writer.WriteLine(JsonConvert.SerializeObject(snapshot));
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
    }
}
