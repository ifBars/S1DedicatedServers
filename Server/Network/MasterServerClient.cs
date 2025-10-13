using System;
using System.Collections;
using System.Net.Http;
using System.Text;
using MelonLoader;
using Newtonsoft.Json;
using UnityEngine;
using FishNet;

namespace DedicatedServerMod.Server.Network
{
    /// <summary>
    /// HTTP client for communicating with the central master server listing API
    /// Handles registration, heartbeat updates, and graceful unregistration
    /// </summary>
    public class MasterServerClient
    {
        private readonly MelonLogger.Instance logger;
        private readonly HttpClient httpClient;
        private bool isRegistered = false;
        private bool isRunning = false;
        private Coroutine heartbeatCoroutine;
        
        private const int HEARTBEAT_INTERVAL_SECONDS = 30;
        private const float HEARTBEAT_TIMEOUT_SECONDS = 10f;
        private const string MOD_VERSION = "1.0.0"; // Update this with your mod version
        
        public bool IsRegistered => isRegistered;
        public bool IsRunning => isRunning;

        public MasterServerClient(MelonLogger.Instance loggerInstance)
        {
            logger = loggerInstance;
            httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(HEARTBEAT_TIMEOUT_SECONDS);
        }

        /// <summary>
        /// Initialize the master server client and register if enabled
        /// </summary>
        public void Initialize()
        {
            try
            {
                var config = ServerConfig.Instance;
                
                if (!config.RegisterWithMasterServer)
                {
                    logger.Msg("Master server registration disabled in config");
                    return;
                }

                if (string.IsNullOrEmpty(config.MasterServerUrl))
                {
                    logger.Warning("Master server URL not configured, skipping registration");
                    return;
                }

                logger.Msg($"Master server client initialized. URL: {config.MasterServerUrl}");
                
                // Check if we already have an API key
                if (!string.IsNullOrEmpty(config.MasterServerApiKey))
                {
                    logger.Msg("Using existing API key from config");
                    isRegistered = true;
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to initialize master server client: {ex}");
            }
        }

        /// <summary>
        /// Register this server with the master server if not already registered
        /// </summary>
        public IEnumerator RegisterWithMasterServer()
        {
            var config = ServerConfig.Instance;
            
            if (!config.RegisterWithMasterServer || string.IsNullOrEmpty(config.MasterServerUrl))
            {
                yield break;
            }

            // Skip if already registered
            if (isRegistered && !string.IsNullOrEmpty(config.MasterServerApiKey))
            {
                logger.Msg("Already registered with master server");
                yield break;
            }

            logger.Msg("Registering with master server...");

            var request = new RegisterServerRequest
            {
                ServerName = config.ServerName,
                OwnerContact = config.MasterServerOwnerContact
            };

            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response = null;
            Exception requestException = null;

            // Send request in background thread
            var requestTask = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    return await httpClient.PostAsync($"{config.MasterServerUrl}/api/v1/servers/register", content);
                }
                catch (Exception ex)
                {
                    requestException = ex;
                    return null;
                }
            });

            // Wait for completion
            while (!requestTask.IsCompleted)
            {
                yield return null;
            }

            response = requestTask.Result;

            if (requestException != null)
            {
                logger.Error($"Failed to register with master server: {requestException.Message}");
                yield break;
            }

            if (response == null || !response.IsSuccessStatusCode)
            {
                logger.Error($"Master server registration failed with status: {response?.StatusCode}");
                yield break;
            }

            // Parse response
            var readTask = System.Threading.Tasks.Task.Run(async () => await response.Content.ReadAsStringAsync());
            while (!readTask.IsCompleted)
            {
                yield return null;
            }

            var responseJson = readTask.Result;
            var registerResponse = JsonConvert.DeserializeObject<RegisterServerResponse>(responseJson);

            if (registerResponse != null && registerResponse.Success)
            {
                logger.Msg($"Successfully registered with master server!");
                logger.Msg($"Server ID: {registerResponse.ServerId}");
                logger.Msg($"API Key: {registerResponse.ApiKey.Substring(0, Math.Min(16, registerResponse.ApiKey.Length))}...");

                // Save API key to config
                config.MasterServerApiKey = registerResponse.ApiKey;
                config.MasterServerServerId = registerResponse.ServerId;
                ServerConfig.SaveConfig();

                isRegistered = true;
            }
            else
            {
                logger.Error($"Master server registration failed: {registerResponse?.Message ?? "Unknown error"}");
            }
        }

        /// <summary>
        /// Start sending heartbeats to the master server
        /// </summary>
        public void StartHeartbeat()
        {
            if (isRunning)
            {
                logger.Warning("Heartbeat already running");
                return;
            }

            var config = ServerConfig.Instance;
            
            if (!config.RegisterWithMasterServer || string.IsNullOrEmpty(config.MasterServerUrl))
            {
                return;
            }

            if (!isRegistered || string.IsNullOrEmpty(config.MasterServerApiKey))
            {
                logger.Warning("Cannot start heartbeat - not registered with master server");
                return;
            }

            logger.Msg("Starting master server heartbeat...");
            isRunning = true;
            heartbeatCoroutine = MelonCoroutines.Start(HeartbeatCoroutine());
        }

        /// <summary>
        /// Stop sending heartbeats
        /// </summary>
        public void StopHeartbeat()
        {
            if (!isRunning)
            {
                return;
            }

            logger.Msg("Stopping master server heartbeat...");
            isRunning = false;
            
            if (heartbeatCoroutine != null)
            {
                MelonCoroutines.Stop(heartbeatCoroutine);
                heartbeatCoroutine = null;
            }
        }

        /// <summary>
        /// Coroutine that sends periodic heartbeats
        /// </summary>
        private IEnumerator HeartbeatCoroutine()
        {
            while (isRunning)
            {
                yield return SendHeartbeat();
                
                // Wait for next heartbeat interval
                float elapsed = 0f;
                while (elapsed < HEARTBEAT_INTERVAL_SECONDS && isRunning)
                {
                    yield return null;
                    elapsed += Time.deltaTime;
                }
            }
        }

        /// <summary>
        /// Send a single heartbeat to the master server
        /// </summary>
        private IEnumerator SendHeartbeat()
        {
            var config = ServerConfig.Instance;

            // Build heartbeat data
            var heartbeatData = new ServerHeartbeatData
            {
                ApiKey = config.MasterServerApiKey,
                ServerName = config.ServerName,
                ServerDescription = config.ServerDescription,
                CurrentPlayers = GetCurrentPlayerCount(),
                MaxPlayers = config.MaxPlayers,
                Port = config.ServerPort,
                PublicAddress = config.PublicServerAddress, // Can be null, server will auto-detect
                PasswordProtected = !string.IsNullOrEmpty(config.ServerPassword),
                GameVersion = Application.version,
                MapName = GetCurrentMapName(),
                ModVersion = MOD_VERSION
            };

            var json = JsonConvert.SerializeObject(heartbeatData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response = null;
            Exception requestException = null;

            // Send request in background thread
            var requestTask = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    return await httpClient.PostAsync($"{config.MasterServerUrl}/api/v1/servers/heartbeat", content);
                }
                catch (Exception ex)
                {
                    requestException = ex;
                    return null;
                }
            });

            // Wait for completion with timeout
            float elapsed = 0f;
            while (!requestTask.IsCompleted && elapsed < HEARTBEAT_TIMEOUT_SECONDS)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            if (!requestTask.IsCompleted)
            {
                logger.Warning("Heartbeat request timed out");
                yield break;
            }

            response = requestTask.Result;

            if (requestException != null)
            {
                logger.Warning($"Heartbeat request failed: {requestException.Message}");
                yield break;
            }

            if (response == null)
            {
                logger.Warning("Heartbeat response was null");
                yield break;
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.Warning($"Heartbeat failed with status: {response.StatusCode}");
                
                // If unauthorized, try to re-register
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    logger.Warning("API key invalid, attempting to re-register...");
                    isRegistered = false;
                    config.MasterServerApiKey = "";
                    ServerConfig.SaveConfig();
                    yield return RegisterWithMasterServer();
                }
                
                yield break;
            }

            if (config.VerboseLogging)
            {
                logger.Msg($"Heartbeat sent successfully ({heartbeatData.CurrentPlayers}/{heartbeatData.MaxPlayers} players)");
            }
        }

        /// <summary>
        /// Gracefully unregister from the master server
        /// </summary>
        public IEnumerator UnregisterFromMasterServer()
        {
            var config = ServerConfig.Instance;
            
            if (!isRegistered || string.IsNullOrEmpty(config.MasterServerApiKey))
            {
                yield break;
            }

            logger.Msg("Unregistering from master server...");

            var request = new { apiKey = config.MasterServerApiKey };
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response = null;

            // Send request in background thread
            var requestTask = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    var requestMessage = new HttpRequestMessage(HttpMethod.Delete, $"{config.MasterServerUrl}/api/v1/servers/unregister")
                    {
                        Content = content
                    };
                    return await httpClient.SendAsync(requestMessage);
                }
                catch
                {
                    return null;
                }
            });

            // Wait for completion
            float elapsed = 0f;
            while (!requestTask.IsCompleted && elapsed < HEARTBEAT_TIMEOUT_SECONDS)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            response = requestTask.Result;

            if (response != null && response.IsSuccessStatusCode)
            {
                logger.Msg("Successfully unregistered from master server");
            }
            else
            {
                logger.Warning("Failed to unregister from master server (server may still be listed until timeout)");
            }
        }

        /// <summary>
        /// Get the current number of connected players
        /// </summary>
        private int GetCurrentPlayerCount()
        {
            try
            {
                if (InstanceFinder.ServerManager != null)
                {
                    return InstanceFinder.ServerManager.Clients.Count;
                }
            }
            catch (Exception ex)
            {
                logger.Warning($"Failed to get player count: {ex.Message}");
            }
            
            return 0;
        }

        /// <summary>
        /// Get the current map/save name
        /// </summary>
        private string GetCurrentMapName()
        {
            try
            {
                var config = ServerConfig.Instance;
                if (!string.IsNullOrEmpty(config.SaveGamePath))
                {
                    return System.IO.Path.GetFileName(config.SaveGamePath);
                }
            }
            catch { }
            
            return "Default";
        }

        /// <summary>
        /// Cleanup resources
        /// </summary>
        public void Shutdown()
        {
            try
            {
                StopHeartbeat();
                httpClient?.Dispose();
                logger.Msg("Master server client shutdown");
            }
            catch (Exception ex)
            {
                logger.Error($"Error during master server client shutdown: {ex}");
            }
        }
    }
}

