using System;
using System.Globalization;
using System.Text;
using DedicatedServerMod.Shared.Networking;
using FishNet;
using FishNet.Transporting;
using MelonLoader;
using Newtonsoft.Json;
using Steamworks;
using DSConstants = DedicatedServerMod.Utils.Constants;

namespace DedicatedServerMod.Client.Managers
{
    /// <summary>
    /// Handles client-side authentication handshake and Steam ticket submission.
    /// </summary>
    public sealed class ClientAuthManager
    {
        private readonly MelonLogger.Instance _logger;

        private HAuthTicket _activeAuthTicket;
        private bool _isAuthenticated;
        private bool _isHandshakeStarted;
        private bool _isConnectionStateHooked;

        /// <summary>
        /// Initializes a new client auth manager.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        public ClientAuthManager(MelonLogger.Instance logger)
        {
            _logger = logger;
            _activeAuthTicket = HAuthTicket.Invalid;
        }

        /// <summary>
        /// Gets whether authentication has completed successfully.
        /// </summary>
        public bool IsAuthenticated => _isAuthenticated;

        /// <summary>
        /// Initializes message subscriptions for auth handshake.
        /// </summary>
        public void Initialize()
        {
            CustomMessaging.ClientMessageReceived += OnClientMessageReceived;
            TryHookConnectionState();
            _logger.Msg("Client auth manager initialized");
        }

        /// <summary>
        /// Starts the auth handshake by sending auth_hello to server.
        /// </summary>
        public void BeginHandshake()
        {
            if (!InstanceFinder.IsClient || InstanceFinder.IsServer)
            {
                return;
            }

            if (_isHandshakeStarted)
            {
                return;
            }

            _isAuthenticated = false;
            _isHandshakeStarted = true;

            var hello = new AuthHelloMessage
            {
                Version = DSConstants.ModVersion
            };

            string payload = JsonConvert.SerializeObject(hello);
            CustomMessaging.SendToServer(DSConstants.Messages.AuthHello, payload);
            _logger.Msg("Authentication hello sent to server");
        }

        /// <summary>
        /// Updates runtime state for auth manager.
        /// </summary>
        public void Update()
        {
            TryHookConnectionState();
        }

        /// <summary>
        /// Clears auth state for disconnect scenarios.
        /// </summary>
        public void OnDisconnected()
        {
            _isAuthenticated = false;
            _isHandshakeStarted = false;
            CancelActiveTicket();
        }

        /// <summary>
        /// Shuts down auth manager resources.
        /// </summary>
        public void Shutdown()
        {
            CustomMessaging.ClientMessageReceived -= OnClientMessageReceived;

            if (_isConnectionStateHooked && InstanceFinder.ClientManager != null)
            {
                InstanceFinder.ClientManager.OnClientConnectionState -= OnClientConnectionState;
                _isConnectionStateHooked = false;
            }

            OnDisconnected();
        }

        private void OnClientMessageReceived(string command, string data)
        {
            if (string.Equals(command, DSConstants.Messages.AuthChallenge, StringComparison.Ordinal))
            {
                HandleAuthChallenge(data);
                return;
            }

            if (string.Equals(command, DSConstants.Messages.AuthResult, StringComparison.Ordinal))
            {
                HandleAuthResult(data);
            }
        }

        private void HandleAuthChallenge(string data)
        {
            AuthChallengeMessage challenge;
            try
            {
                challenge = JsonConvert.DeserializeObject<AuthChallengeMessage>(data ?? string.Empty);
            }
            catch (JsonException ex)
            {
                _logger.Warning($"Failed to parse auth challenge: {ex.Message}");
                return;
            }

            if (challenge == null)
            {
                _logger.Warning("Received null auth challenge payload");
                return;
            }

            if (!string.Equals(challenge.Provider, "steam_game_server", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Warning($"Unsupported auth provider challenge: {challenge.Provider}");
                return;
            }

            if (!TryCreateAuthSessionTicket(out string steamId, out string ticketHex))
            {
                _logger.Warning("Failed to create Steam auth ticket for challenge");
                return;
            }

            var ticketMessage = new AuthTicketMessage
            {
                Provider = "steam_game_server",
                SteamId = steamId,
                TicketHex = ticketHex,
                Nonce = challenge.Nonce ?? string.Empty
            };

            string payload = JsonConvert.SerializeObject(ticketMessage);
            CustomMessaging.SendToServer(DSConstants.Messages.AuthTicket, payload);
            _logger.Msg("Authentication ticket submitted to server");
        }

        private void HandleAuthResult(string data)
        {
            AuthResultMessage result;
            try
            {
                result = JsonConvert.DeserializeObject<AuthResultMessage>(data ?? string.Empty);
            }
            catch (JsonException ex)
            {
                _logger.Warning($"Failed to parse auth result: {ex.Message}");
                return;
            }

            if (result == null)
            {
                return;
            }

            _isAuthenticated = result.Success;
            _isHandshakeStarted = false;

            if (result.Success)
            {
                _logger.Msg($"Authentication succeeded: {result.Message}");
                CustomMessaging.SendToServer(DSConstants.Messages.RequestServerData);
            }
            else
            {
                _logger.Warning($"Authentication failed: {result.Message}");
            }
        }

        private bool TryCreateAuthSessionTicket(out string steamId, out string ticketHex)
        {
            steamId = string.Empty;
            ticketHex = string.Empty;

            try
            {
                steamId = SteamUser.GetSteamID().m_SteamID.ToString(CultureInfo.InvariantCulture);

                byte[] ticketBuffer = new byte[1024];
                uint ticketSize = 0;
                SteamNetworkingIdentity identity = default;

                CancelActiveTicket();

                _activeAuthTicket = SteamUser.GetAuthSessionTicket(ticketBuffer, ticketBuffer.Length, out ticketSize, ref identity);
                if (_activeAuthTicket == HAuthTicket.Invalid || ticketSize == 0)
                {
                    _activeAuthTicket = HAuthTicket.Invalid;
                    return false;
                }

                ticketHex = BytesToHex(ticketBuffer, (int)ticketSize);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Warning($"Error creating auth session ticket: {ex.Message}");
                return false;
            }
        }

        private void CancelActiveTicket()
        {
            if (_activeAuthTicket == HAuthTicket.Invalid)
            {
                return;
            }

            try
            {
                SteamUser.CancelAuthTicket(_activeAuthTicket);
            }
            catch (Exception ex)
            {
                _logger.Warning($"Failed to cancel auth ticket: {ex.Message}");
            }
            finally
            {
                _activeAuthTicket = HAuthTicket.Invalid;
            }
        }

        private void TryHookConnectionState()
        {
            if (_isConnectionStateHooked || InstanceFinder.ClientManager == null)
            {
                return;
            }

            InstanceFinder.ClientManager.OnClientConnectionState += OnClientConnectionState;
            _isConnectionStateHooked = true;
        }

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                OnDisconnected();
            }
        }

        private static string BytesToHex(byte[] bytes, int length)
        {
            var builder = new StringBuilder(length * 2);
            for (int i = 0; i < length; i++)
            {
                builder.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }
}
