using System.Collections;
using System.Net;
using System.Net.Http;
using System.Text;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Shared;
using DedicatedServerMod.Shared.Configuration;
using DedicatedServerMod.Utils;
using MelonLoader;
using Newtonsoft.Json;
using UnityEngine;

namespace DedicatedServerMod.Server.Network
{
    internal sealed class PublicServerListingClient : IDisposable
    {
        private const int HEARTBEAT_INTERVAL_SECONDS = 60;

        private readonly HttpClient _httpClient;
        private readonly MelonLogger.Instance _logger;
        private readonly PlayerManager _playerManager;
        private object _heartbeatCoroutine;
        private bool _isRunning;

        internal PublicServerListingClient(MelonLogger.Instance logger, PlayerManager playerManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _playerManager = playerManager ?? throw new ArgumentNullException(nameof(playerManager));
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
        }

        internal void Start()
        {
            if (_isRunning || !ServerConfig.Instance.PublicListingEnabled)
            {
                return;
            }

            if (!TryGetServiceUri(out _))
            {
                DebugLog.Warning("Public listing is enabled, but publicListingServiceUrl is not a valid HTTP or HTTPS endpoint.");
                return;
            }

            _isRunning = true;
            _heartbeatCoroutine = MelonCoroutines.Start(RunHeartbeatLoop());
            DebugLog.Info("Public server listing enabled; starting background registration and heartbeat.");
        }

        internal void Shutdown()
        {
            if (!_isRunning)
            {
                return;
            }

            _isRunning = false;
            if (_heartbeatCoroutine != null)
            {
                MelonCoroutines.Stop(_heartbeatCoroutine);
                _heartbeatCoroutine = null;
            }

            TryRemovePresence();
        }

        public void Dispose()
        {
            Shutdown();
            _httpClient.Dispose();
        }

        private IEnumerator RunHeartbeatLoop()
        {
            while (_isRunning)
            {
                Task<bool> heartbeatTask = EnsureRegisteredAndSendHeartbeatAsync();
                while (_isRunning && !heartbeatTask.IsCompleted)
                {
                    yield return null;
                }

                if (heartbeatTask.IsFaulted)
                {
                    DebugLog.Warning($"Public listing heartbeat failed: {heartbeatTask.Exception?.GetBaseException().Message}");
                }

                if (_isRunning)
                {
                    yield return new WaitForSecondsRealtime(HEARTBEAT_INTERVAL_SECONDS);
                }
            }
        }

        private async Task<bool> EnsureRegisteredAndSendHeartbeatAsync()
        {
            ServerConfig config = ServerConfig.Instance;
            if (string.IsNullOrWhiteSpace(config.PublicListingId) || string.IsNullOrWhiteSpace(config.PublicListingSecret))
            {
                if (!await RegisterAsync(config).ConfigureAwait(false))
                {
                    return false;
                }
            }

            return await SendHeartbeatAsync(config).ConfigureAwait(false);
        }

        private async Task<bool> RegisterAsync(ServerConfig config)
        {
            if (!TryGetServiceUri(out Uri serviceUri))
            {
                return false;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(serviceUri, "/api/v2/listings"))
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
            using HttpResponseMessage response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                DebugLog.Warning($"Public listing registration returned HTTP {(int)response.StatusCode}.");
                return false;
            }

            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            PublicListingRegistrationResponse registration = JsonConvert.DeserializeObject<PublicListingRegistrationResponse>(json);
            if (registration == null || !registration.Success || string.IsNullOrWhiteSpace(registration.ListingId) || string.IsNullOrWhiteSpace(registration.Secret))
            {
                DebugLog.Warning("Public listing registration returned an invalid response.");
                return false;
            }

            config.PublicListingId = registration.ListingId;
            config.PublicListingSecret = registration.Secret;
            ServerConfig.SaveConfig();
            DebugLog.Info($"Public listing registration created identity {registration.ListingId}.");
            return true;
        }

        private async Task<bool> SendHeartbeatAsync(ServerConfig config)
        {
            if (!TryGetServiceUri(out Uri serviceUri))
            {
                return false;
            }

            var heartbeat = new PublicListingHeartbeatRequest
            {
                ServerName = config.ServerName,
                ServerDescription = config.ServerDescription,
                CurrentPlayers = _playerManager.GetVisiblePlayerCount(),
                MaxPlayers = config.MaxPlayers,
                Port = config.ServerPort,
                PasswordProtected = !string.IsNullOrEmpty(config.ServerPassword),
                GameVersion = Application.version,
                ModVersion = API.Version.ModVersion
            };

            using var request = new HttpRequestMessage(
                HttpMethod.Put,
                new Uri(serviceUri, $"/api/v2/listings/{Uri.EscapeDataString(config.PublicListingId)}/heartbeat"));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.PublicListingSecret);
            request.Content = new StringContent(JsonConvert.SerializeObject(heartbeat), Encoding.UTF8, "application/json");

            using HttpResponseMessage response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                DebugLog.Warning("Public listing credentials were rejected; a new identity will be registered on the next heartbeat.");
                config.PublicListingId = string.Empty;
                config.PublicListingSecret = string.Empty;
                ServerConfig.SaveConfig();
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                DebugLog.Warning($"Public listing heartbeat returned HTTP {(int)response.StatusCode}.");
                return false;
            }

            DebugLog.Verbose("Public listing heartbeat accepted.");
            return true;
        }

        private void TryRemovePresence()
        {
            ServerConfig config = ServerConfig.Instance;
            if (string.IsNullOrWhiteSpace(config.PublicListingId) || string.IsNullOrWhiteSpace(config.PublicListingSecret) || !TryGetServiceUri(out Uri serviceUri))
            {
                return;
            }

            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Delete,
                    new Uri(serviceUri, $"/api/v2/listings/{Uri.EscapeDataString(config.PublicListingId)}/presence"));
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.PublicListingSecret);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                using HttpResponseMessage response = _httpClient.SendAsync(request, timeout.Token).ConfigureAwait(false).GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    DebugLog.Verbose($"Public listing presence removal returned HTTP {(int)response.StatusCode}; TTL expiry will remove it.");
                }
            }
            catch (Exception ex)
            {
                DebugLog.Verbose($"Public listing presence removal did not complete: {ex.Message}. TTL expiry will remove it.");
            }
        }

        private static bool TryGetServiceUri(out Uri serviceUri)
        {
            string value = ServerConfig.Instance.PublicListingServiceUrl?.Trim().TrimEnd('/');
            if (!Uri.TryCreate(value, UriKind.Absolute, out serviceUri))
            {
                return false;
            }

            return string.Equals(serviceUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                   (string.Equals(serviceUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && serviceUri.IsLoopback);
        }
    }
}
