using System.Net;
using System.Net.Sockets;
using System.Text;
using MelonLoader;
using DedicatedServerMod.Server.CustomClothing;
using Newtonsoft.Json;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Shared;
using DedicatedServerMod.Shared.CustomClothing;
using DedicatedServerMod.Shared.Configuration;

namespace DedicatedServerMod.Server.Network
{
    /// <summary>
    /// Hosts a lightweight TCP status endpoint for server browser metadata queries.
    /// </summary>
    public sealed class ServerStatusQueryService
    {
        private const string StatusRequestCommand = "DS_STATUS";
        private const string ClothingManifestCommand = "DS_CLOTHING_MANIFEST";
        private const string ClothingAssetCommand = "DS_CLOTHING_ASSET";

        private readonly MelonLogger.Instance _logger;
        private readonly PlayerManager _playerManager;
        private readonly ServerCustomClothingManager _customClothingManager;
        private TcpListener _listener;
        private Thread _listenerThread;
        private CancellationTokenSource _cancellation;

        internal ServerStatusQueryService(MelonLogger.Instance logger, PlayerManager playerManager, ServerCustomClothingManager customClothingManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _playerManager = playerManager ?? throw new ArgumentNullException(nameof(playerManager));
            _customClothingManager = customClothingManager;
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

                        if (string.Equals(request, ClothingManifestCommand, StringComparison.Ordinal))
                        {
                            WriteManifestResponse(writer);
                            return;
                        }

                        if (!string.IsNullOrWhiteSpace(request) && request.StartsWith(ClothingAssetCommand + " ", StringComparison.Ordinal))
                        {
                            string itemId = request.Substring(ClothingAssetCommand.Length + 1).Trim();
                            WriteAssetResponse(writer, itemId);
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

        private void WriteManifestResponse(StreamWriter writer)
        {
            if (_customClothingManager == null)
            {
                writer.WriteLine(JsonConvert.SerializeObject(new CustomClothingManifestResponse
                {
                    Success = true,
                    Manifest = new CustomClothingManifest()
                }));
                return;
            }

            writer.WriteLine(JsonConvert.SerializeObject(new CustomClothingManifestResponse
            {
                Success = true,
                Manifest = _customClothingManager.GetManifest()
            }));
        }

        private void WriteAssetResponse(StreamWriter writer, string itemId)
        {
            if (_customClothingManager != null && _customClothingManager.TryGetAssetPayload(itemId, out CustomClothingAssetPayload payload))
            {
                writer.WriteLine(JsonConvert.SerializeObject(new CustomClothingAssetResponse
                {
                    Success = true,
                    Asset = payload
                }));
                return;
            }

            writer.WriteLine(JsonConvert.SerializeObject(new CustomClothingAssetResponse
            {
                Success = false,
                Error = string.IsNullOrWhiteSpace(itemId)
                    ? "Custom clothing asset request did not specify an item id."
                    : $"Custom clothing asset '{itemId}' was not found."
            }));
        }
    }
}
