using FishNet;
using MelonLoader;
using ScheduleOne.Persistence;
using System;
using System.Collections;
using ScheduleOne.DevUtilities;
using DedicatedServerMod.API;
using DedicatedServerMod.Shared.Networking;
using UnityEngine;

namespace DedicatedServerMod.Client.Managers
{
    /// <summary>
    /// Manages dedicated server connections for the client mod.
    /// Handles connection establishment, state tracking, and coordination with other systems.
    /// </summary>
    public class ClientConnectionManager
    {
        private readonly MelonLogger.Instance logger;
        
        // Connection configuration
        private static string _targetServerIP = "localhost";
        private static int _targetServerPort = 38465;
        private static bool _isTugboatMode = false;
        
        // Connection state
        public bool IsConnecting { get; private set; }
        public bool IsConnectedToDedicatedServer { get; private set; }
        public string LastConnectionError { get; private set; }

        public ClientConnectionManager(MelonLogger.Instance logger)
        {
            this.logger = logger;
        }

        public void Initialize()
        {
            try
            {
                logger.Msg("Initializing ClientConnectionManager");
                
                // Parse command line arguments for server configuration
                ParseCommandLineArguments();
                
                logger.Msg("ClientConnectionManager initialized");
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to initialize ClientConnectionManager: {ex}");
            }
        }

        /// <summary>
        /// Parse command line arguments for server IP and port
        /// </summary>
        private void ParseCommandLineArguments()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--server-ip" && i + 1 < args.Length)
                {
                    _targetServerIP = args[i + 1];
                    logger.Msg($"Target server IP set to: {_targetServerIP}");
                }
                if (args[i] == "--server-port" && i + 1 < args.Length)
                {
                    if (int.TryParse(args[i + 1], out int port))
                    {
                        _targetServerPort = port;
                        logger.Msg($"Target server port set to: {_targetServerPort}");
                    }
                }
            }
        }

        /// <summary>
        /// Start connection to dedicated server
        /// </summary>
        public void StartDedicatedConnection()
        {
            if (IsConnecting)
            {
                logger.Warning("Connection already in progress");
                return;
            }

            try
            {
                logger.Msg($"Starting dedicated server connection to {_targetServerIP}:{_targetServerPort}");
                logger.Msg("This will bypass the frozen intro sequence and handle character creation manually");
                
                IsConnecting = true;
                _isTugboatMode = true;
                LastConnectionError = null;

                // Prepare LoadManager for join mode
                PrepareLoadManager();

                // Start connection coroutine
                StartCoroutine(ConnectToDedicatedServerCoroutine());
            }
            catch (Exception ex)
            {
                logger.Error($"Error starting dedicated connection: {ex}");
                HandleConnectionError(ex.Message);
            }
        }

        /// <summary>
        /// Prepare LoadManager for dedicated server join
        /// </summary>
        private void PrepareLoadManager()
        {
            var loadManager = Singleton<LoadManager>.Instance;
            if (loadManager != null)
            {
                loadManager.ActiveSaveInfo = null;
                loadManager.IsLoading = true;
                logger.Msg("Set LoadManager.ActiveSaveInfo = null (join mode)");
            }
        }

        /// <summary>
        /// Main connection coroutine
        /// </summary>
        private IEnumerator ConnectToDedicatedServerCoroutine()
        {
            logger.Msg("Starting connection coroutine");
            
            // Get ClientManager
            var clientManager = InstanceFinder.ClientManager;
            if (clientManager == null)
            {
                HandleConnectionError("ClientManager not found");
                yield break;
            }

            // Start connection (will be intercepted by transport patches)
            logger.Msg("Calling ClientManager.StartConnection (will be intercepted by transport patches)");
            bool result = clientManager.StartConnection();
            
            logger.Msg($"StartConnection returned: {result}");
            
            if (!result)
            {
                HandleConnectionError("StartConnection failed");
                yield break;
            }

            // Wait for connection establishment
            yield return StartCoroutine(WaitForConnectionEstablishment());
        }

        /// <summary>
        /// Wait for connection to be established with timeout
        /// </summary>
        private IEnumerator WaitForConnectionEstablishment()
        {
            float timeout = 15f; // Increased timeout for dedicated server connections
            float elapsed = 0f;
            
            while (elapsed < timeout && !InstanceFinder.IsClient)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (InstanceFinder.IsClient)
            {
                logger.Msg("Successfully connected to dedicated server!");
                logger.Msg($"Client connection established in {elapsed:F1}s");
                
                IsConnectedToDedicatedServer = true;
                IsConnecting = false;
                
                // Note: Server data request is sent from MessagingPatches after RPC registration

                // Wait for player to spawn and initialize
                yield return new WaitForSeconds(2f);
                
                if (ScheduleOne.PlayerScripts.Player.Local != null)
                {
                    logger.Msg("Local player found - connection setup complete");
                    try { ModManager.NotifyConnectedToServer(); } catch {}
                }
                else
                {
                    logger.Warning("Local player not found after connection");
                }
            }
            else
            {
                HandleConnectionError($"Connection timeout after {timeout}s");
            }
        }

        /// <summary>
        /// Handle connection errors and cleanup
        /// </summary>
        private void HandleConnectionError(string errorMessage)
        {
            logger.Error($"Connection failed: {errorMessage}");
            LastConnectionError = errorMessage;
            IsConnecting = false;
            IsConnectedToDedicatedServer = false;
            _isTugboatMode = false;
            
            // Cleanup on failure
            try
            {
                if (InstanceFinder.IsClient)
                {
                    InstanceFinder.ClientManager?.StopConnection();
                }
                
                var loadManager = Singleton<LoadManager>.Instance;
                if (loadManager != null)
                {
                    loadManager.IsLoading = false;
                }
            }
            catch (Exception cleanupEx)
            {
                logger.Error($"Error during cleanup: {cleanupEx}");
            }
        }

        /// <summary>
        /// Disconnect from dedicated server
        /// </summary>
        public void DisconnectFromDedicatedServer()
        {
            try
            {
                logger.Msg("Disconnecting from dedicated server");
                
                // Stop connection first
                if (InstanceFinder.IsClient)
                {
                    InstanceFinder.ClientManager?.StopConnection();
                }

                // Reset all connection state
                IsConnectedToDedicatedServer = false;
                IsConnecting = false; // Also reset connecting state
                _isTugboatMode = false;
                LastConnectionError = null; // Clear error
                
                // Reset LoadManager
                var loadManager = Singleton<LoadManager>.Instance;
                if (loadManager != null)
                {
                    loadManager.IsLoading = false;
                    loadManager.ActiveSaveInfo = null;
                }
                
                logger.Msg("Disconnected from dedicated server - all state reset");
                try { ModManager.NotifyDisconnectedFromServer(); } catch {}
            }
            catch (Exception ex)
            {
                logger.Error($"Error disconnecting: {ex}");
                // Force reset state even on error
                IsConnectedToDedicatedServer = false;
                IsConnecting = false;
                _isTugboatMode = false;
            }
        }

        /// <summary>
        /// Get connection status information
        /// </summary>
        public string GetConnectionStatus()
        {
            try
            {
                var status = "=== Connection Status ===\n";
                status += $"Is Connecting: {IsConnecting}\n";
                status += $"Connected to Dedicated Server: {IsConnectedToDedicatedServer}\n";
                status += $"Tugboat Mode: {_isTugboatMode}\n";
                status += $"Target Server: {_targetServerIP}:{_targetServerPort}\n";
                status += $"FishNet Is Client: {InstanceFinder.IsClient}\n";
                status += $"FishNet Is Server: {InstanceFinder.IsServer}\n";
                status += $"Current Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}\n";
                status += $"Player Local: {(ScheduleOne.PlayerScripts.Player.Local != null ? "Spawned" : "Not Spawned")}\n";
                
                if (!string.IsNullOrEmpty(LastConnectionError))
                {
                    status += $"Last Error: {LastConnectionError}\n";
                }

                var loadManager = Singleton<LoadManager>.Instance;
                if (loadManager != null)
                {
                    status += $"LoadManager Status: {loadManager.LoadStatus}\n";
                    status += $"Is Loading: {loadManager.IsLoading}\n";
                    status += $"Is Game Loaded: {loadManager.IsGameLoaded}\n";
                }
                
                return status;
            }
            catch (Exception ex)
            {
                return $"Error getting status: {ex.Message}";
            }
        }

        /// <summary>
        /// Check if currently in Tugboat mode
        /// </summary>
        public static bool IsTugboatMode => _isTugboatMode;

        /// <summary>
        /// Get target server configuration
        /// </summary>
        public static (string ip, int port) GetTargetServer() => (_targetServerIP, _targetServerPort);

        /// <summary>
        /// Set the target server IP and port for the next connection
        /// </summary>
        public void SetTargetServer(string ip, int port)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ip))
                {
                    throw new ArgumentException("IP cannot be empty");
                }
                if (port <= 0 || port > 65535)
                {
                    throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535");
                }

                _targetServerIP = ip.Trim();
                _targetServerPort = port;
                logger.Msg($"Target server updated to {_targetServerIP}:{_targetServerPort}");
            }
            catch (Exception ex)
            {
                logger.Error($"Error setting target server: {ex.Message}");
            }
        }

        /// <summary>
        /// Helper method for coroutine starting
        /// </summary>
        private object StartCoroutine(IEnumerator coroutine)
        {
            return MelonCoroutines.Start(coroutine);
        }
    }
}
