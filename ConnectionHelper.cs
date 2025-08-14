using FishNet;
using MelonLoader;
using ScheduleOne.Persistence;
using ScheduleOne.DevUtilities;
using System;
using System.Collections;
using UnityEngine;

namespace DedicatedServerMod
{
    /// <summary>
    /// Helper class for managing dedicated server connections.
    /// </summary>
    public static class ConnectionHelper
    {
        private static MelonLogger.Instance logger = new MelonLogger.Instance("ConnectionHelper");

        /// <summary>
        /// Connection state tracking
        /// </summary>
        public static bool IsConnecting { get; private set; }
        public static bool IsConnectedToDedicatedServer { get; private set; }
        public static string LastConnectionError { get; private set; }

        /// <summary>
        /// Initiates connection to a dedicated server using the patterns
        /// </summary>
        public static void ConnectToDedicatedServer(string serverIP, int port)
        {
            if (IsConnecting)
            {
                logger.Warning("Connection already in progress");
                return;
            }

            try
            {
                logger.Msg($"Initiating connection to {serverIP}:{port}");
                IsConnecting = true;
                IsConnectedToDedicatedServer = false;
                LastConnectionError = null;

                MelonCoroutines.Start(ConnectionCoroutine(serverIP, port));
            }
            catch (Exception ex)
            {
                logger.Error($"Error initiating connection: {ex}");
                IsConnecting = false;
                LastConnectionError = ex.Message;
            }
        }

        private static IEnumerator ConnectionCoroutine(string serverIP, int port)
        {
            // Step 1: Prepare transport
            logger.Msg("Step 1: Preparing Tugboat transport");
            
            if (!TransportManager.EnsureTugboatAvailable())
            {
                HandleConnectionError("Failed to setup Tugboat transport");
                yield break;
            }

            if (!TransportManager.ConfigureTugboatConnection(serverIP, port))
            {
                HandleConnectionError("Failed to configure Tugboat connection");
                yield break;
            }

            yield return new WaitForSeconds(0.1f);

            // Step 2: Prepare LoadManager
            logger.Msg("Step 2: Preparing LoadManager for dedicated server join");
            
            var loadManager = Singleton<LoadManager>.Instance;
            if (loadManager != null)
            {
                // Set to null to force "join existing server" mode
                loadManager.ActiveSaveInfo = null;
                loadManager.IsLoading = true;
                loadManager.LoadedGameFolderPath = string.Empty;
                loadManager.LoadStatus = LoadManager.ELoadStatus.LoadingScene;
                
                logger.Msg("LoadManager prepared for server join");
            }
            else
            {
                logger.Warning("LoadManager not available");
            }

            yield return new WaitForSeconds(0.1f);

            // Step 3: Start client connection
            logger.Msg("Step 3: Starting client connection");
            
            var clientManager = InstanceFinder.ClientManager;
            if (clientManager == null)
            {
                HandleConnectionError("ClientManager not available");
                yield break;
            }

            // This will trigger our patched StartConnection method
            bool connectionStarted = clientManager.StartConnection();
            
            if (!connectionStarted)
            {
                HandleConnectionError("Failed to start client connection");
                yield break;
            }

            logger.Msg("Client connection initiated");

            // Step 4: Wait for connection establishment
            logger.Msg("Step 4: Waiting for connection establishment");
            
            float timeout = 15f; // Generous timeout for testing
            float elapsed = 0f;
            
            while (elapsed < timeout && !InstanceFinder.IsClient)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
                
                // Log progress every few seconds
                if (elapsed % 2f < 0.1f)
                {
                    logger.Msg($"Waiting for connection... {elapsed:F1}s");
                }
            }

            if (!InstanceFinder.IsClient)
            {
                HandleConnectionError($"Connection timeout after {timeout}s");
                yield break;
            }

            logger.Msg($"Successfully connected to dedicated server in {elapsed:F1}s!");
            
            // Step 5: Wait for scene and player
            logger.Msg("Step 5: Waiting for scene and player setup");
            
            // Wait for main scene
            while (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Main" && elapsed < timeout + 10f)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Main")
            {
                logger.Msg("Main scene loaded");
                
                // Wait for player spawn (with additional timeout)
                while (ScheduleOne.PlayerScripts.Player.Local == null && elapsed < timeout + 20f)
                {
                    yield return new WaitForSeconds(0.1f);
                    elapsed += 0.1f;
                }

                if (ScheduleOne.PlayerScripts.Player.Local != null)
                {
                    logger.Msg("Player spawned successfully!");
                    IsConnectedToDedicatedServer = true;
                    
                    // Final LoadManager cleanup
                    if (loadManager != null)
                    {
                        loadManager.LoadStatus = LoadManager.ELoadStatus.None;
                        loadManager.IsLoading = false;
                        loadManager.IsGameLoaded = true;
                    }
                }
                else
                {
                    logger.Warning("Player spawn timeout");
                }
            }
            else
            {
                logger.Warning("Scene load timeout or incorrect scene");
            }

            IsConnecting = false;
            logger.Msg("Connection process completed");
        }

        /// <summary>
        /// Handles connection errors and performs cleanup
        /// </summary>
        private static void HandleConnectionError(string errorMessage)
        {
            logger.Error($"Connection failed: {errorMessage}");
            LastConnectionError = errorMessage;
            IsConnecting = false;
            IsConnectedToDedicatedServer = false;
            
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
                    loadManager.LoadStatus = LoadManager.ELoadStatus.None;
                }
            }
            catch (Exception cleanupEx)
            {
                logger.Error($"Error during cleanup: {cleanupEx}");
            }
        }

        /// <summary>
        /// Disconnects from dedicated server and returns to menu.
        /// </summary>
        public static void DisconnectFromDedicatedServer()
        {
            try
            {
                logger.Msg("Disconnecting from dedicated server");
                
                if (InstanceFinder.IsClient)
                {
                    InstanceFinder.ClientManager?.StopConnection();
                }
                
                if (InstanceFinder.IsServer)
                {
                    InstanceFinder.ServerManager?.StopConnection(true);
                }

                IsConnectedToDedicatedServer = false;
                
                // Restore default transport for normal multiplayer
                TransportManager.RestoreDefaultTransport();
                
                logger.Msg("Disconnected from dedicated server");
            }
            catch (Exception ex)
            {
                logger.Error($"Error disconnecting: {ex}");
            }
        }

        /// <summary>
        /// Gets current connection status information.
        /// </summary>
        public static string GetConnectionStatus()
        {
            try
            {
                var status = "=== Connection Status ===\n";
                status += $"Is Connecting: {IsConnecting}\n";
                status += $"Connected to Dedicated Server: {IsConnectedToDedicatedServer}\n";
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

                status += "\n" + TransportManager.GetTransportInfo();
                
                return status;
            }
            catch (Exception ex)
            {
                return $"Error getting status: {ex.Message}";
            }
        }
    }
}
