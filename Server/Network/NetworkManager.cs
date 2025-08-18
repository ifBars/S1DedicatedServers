using FishNet;
using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using MelonLoader;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using DedicatedServerMod;

namespace DedicatedServerMod.Server.Network
{
    /// <summary>
    /// Manages network transport, server connections, and network lifecycle.
    /// Handles switching between Steam networking and Tugboat for dedicated servers.
    /// </summary>
    public class NetworkManager
    {
        private readonly MelonLogger.Instance logger;
        private DateTime _serverStartTime;
        private bool _isServerRunning = false;
        private bool _hooksRegistered = false;

        public NetworkManager(MelonLogger.Instance loggerInstance)
        {
            logger = loggerInstance;
        }

        /// <summary>
        /// Gets whether the server is currently running
        /// </summary>
        public bool IsServerRunning => _isServerRunning;

        /// <summary>
        /// Gets the server uptime
        /// </summary>
        public TimeSpan Uptime => _isServerRunning ? DateTime.Now - _serverStartTime : TimeSpan.Zero;

        /// <summary>
        /// Initialize the network manager
        /// </summary>
        public void Initialize()
        {
            try
            {
                MelonCoroutines.Start(WaitAndRegisterHooks());
                logger.Msg("Network manager initialized");
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to initialize network manager: {ex}");
                throw;
            }
        }

        private bool TryRegisterHooks()
        {
            if (_hooksRegistered) return true;
            var serverManager = InstanceFinder.ServerManager;
            if (serverManager == null) return false;
            serverManager.OnServerConnectionState -= OnServerConnectionState; // ensure no duplicates
            serverManager.OnServerConnectionState += OnServerConnectionState;
            _hooksRegistered = true;
            logger.Msg("Network event hooks established");
            return true;
        }

        private IEnumerator WaitAndRegisterHooks()
        {
            // Wait until FishNet NetworkManager and ServerManager are ready, then hook
            while (InstanceFinder.NetworkManager == null || InstanceFinder.ServerManager == null)
                yield return null;
            TryRegisterHooks();
        }

        /// <summary>
        /// Handle server connection state changes
        /// </summary>
        private void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            logger.Msg($"Server connection state: {args.ConnectionState}");

            switch (args.ConnectionState)
            {
                case LocalConnectionState.Started:
                    _isServerRunning = true;
                    _serverStartTime = DateTime.Now;
                    logger.Msg($"=== DEDICATED SERVER ONLINE ===");
                    logger.Msg($"Server Name: {ServerConfig.Instance.ServerName}");
                    logger.Msg($"Port: {ServerConfig.Instance.ServerPort}");
                    logger.Msg($"Max Players: {ServerConfig.Instance.MaxPlayers}");
                    break;

                case LocalConnectionState.Stopped:
                    _isServerRunning = false;
                    logger.Msg($"=== DEDICATED SERVER OFFLINE ===");
                    break;
            }
        }

        /// <summary>
        /// Start the dedicated server asynchronously
        /// </summary>
        public IEnumerator StartServerAsync()
        {
            logger.Msg("Starting dedicated server...");

            try
            {
                // Ensure transport is properly configured
                if (!SetupTugboatTransport())
                {
                    logger.Error("Failed to setup Tugboat transport");
                    yield break;
                }

                // Start the server
                var networkManager = InstanceFinder.NetworkManager;
                if (networkManager != null && networkManager.ServerManager != null)
                {
                    networkManager.ServerManager.StartConnection();
                    logger.Msg("Server start initiated");
                }
                else
                {
                    logger.Error("NetworkManager or ServerManager not available");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error starting server: {ex}");
            }
        }

        /// <summary>
        /// Setup Tugboat transport for dedicated server
        /// </summary>
        private bool SetupTugboatTransport()
        {
            try
            {
                var networkManager = InstanceFinder.NetworkManager;
                if (networkManager == null)
                {
                    logger.Error("NetworkManager not available");
                    return false;
                }

                var transportManager = networkManager.TransportManager;
                var transport = transportManager.Transport;
                var multipass = transport as Multipass;

                if (multipass == null)
                {
                    logger.Error("Multipass transport not found");
                    return false;
                }

                // Get or add Tugboat component
                var tugboat = multipass.gameObject.GetComponent<Tugboat>();
                if (tugboat == null)
                {
                    tugboat = multipass.gameObject.AddComponent<Tugboat>();
                    if (tugboat == null)
                    {
                        logger.Error("Failed to add Tugboat component");
                        return false;
                    }
                    logger.Msg("Added Tugboat transport component");
                }

                // Configure Tugboat for server
                tugboat.SetPort((ushort)ServerConfig.Instance.ServerPort);

                // Set as server transport
                if (!SetServerTransport(multipass, tugboat))
                {
                    logger.Error("Failed to set Tugboat as server transport");
                    return false;
                }

                logger.Msg($"Tugboat transport configured for server on port {ServerConfig.Instance.ServerPort}");
                return true;
            }
            catch (Exception ex)
            {
                logger.Error($"Error setting up Tugboat transport: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Set the server transport using reflection
        /// </summary>
        private bool SetServerTransport(Multipass multipass, Transport transport)
        {
            try
            {
                // Try to set server transport using reflection
                var serverTransportField = typeof(Multipass).GetField("_clientTransport",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (serverTransportField != null)
                {
                    serverTransportField.SetValue(multipass, transport);
                    logger.Msg("Set server transport using reflection");
                    return true;
                }

                // Try alternative method
                var methods = typeof(Multipass).GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                foreach (var method in methods)
                {
                    if (method.Name.Contains("SetClientTransport") && method.GetParameters().Length == 1)
                    {
                        var paramType = method.GetParameters()[0].ParameterType;
                        if (paramType.IsAssignableFrom(typeof(Transport)))
                        {
                            method.Invoke(multipass, new object[] { transport });
                            logger.Msg("Set server transport using method reflection");
                            return true;
                        }
                    }
                }

                logger.Warning("Could not find method to set server transport");
                return false;
            }
            catch (Exception ex)
            {
                logger.Error($"Error setting server transport: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Stop the dedicated server
        /// </summary>
        public void StopServer(string reason = "Server stop requested")
        {
            try
            {
                logger.Msg($"Stopping server: {reason}");

                var networkManager = InstanceFinder.NetworkManager;
                if (networkManager != null && networkManager.ServerManager != null)
                {
                    networkManager.ServerManager.StopConnection(true);
                    logger.Msg("Server stop initiated");
                }
                else
                {
                    logger.Warning("NetworkManager or ServerManager not available for stop");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error stopping server: {ex}");
            }
        }

        /// <summary>
        /// Get network transport information for debugging
        /// </summary>
        public string GetTransportInfo()
        {
            try
            {
                var networkManager = InstanceFinder.NetworkManager;
                if (networkManager == null)
                    return "NetworkManager not available";

                var transportManager = networkManager.TransportManager;
                var transport = transportManager.Transport;
                var multipass = transport as Multipass;

                if (multipass == null)
                    return "Multipass transport not found";

                var components = multipass.gameObject.GetComponents<Transport>();
                var info = $"Available transports on Multipass: {components.Length}\n";

                foreach (var comp in components)
                {
                    info += $"- {comp.GetType().Name}\n";
                }

                return info;
            }
            catch (Exception ex)
            {
                return $"Error getting transport info: {ex.Message}";
            }
        }

        /// <summary>
        /// Restore default Steam transport for normal multiplayer
        /// </summary>
        public bool RestoreDefaultTransport()
        {
            try
            {
                var networkManager = InstanceFinder.NetworkManager;
                if (networkManager == null)
                {
                    logger.Warning("NetworkManager not available");
                    return false;
                }

                var transportManager = networkManager.TransportManager;
                var transport = transportManager.Transport;
                var multipass = transport as Multipass;

                if (multipass == null)
                {
                    logger.Warning("Multipass transport not found");
                    return false;
                }

                // Look for FishySteamworks transport
                var steamTransport = multipass.gameObject.GetComponent<FishySteamworks.FishySteamworks>();
                if (steamTransport != null)
                {
                    SetServerTransport(multipass, steamTransport);
                    logger.Msg("Restored default Steam transport");
                    return true;
                }

                logger.Warning("Steam transport not found");
                return false;
            }
            catch (Exception ex)
            {
                logger.Error($"Error restoring default transport: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Get network statistics
        /// </summary>
        public NetworkStats GetNetworkStats()
        {
            return new NetworkStats
            {
                IsServerRunning = _isServerRunning,
                Uptime = Uptime,
                ServerPort = ServerConfig.Instance.ServerPort,
                TransportInfo = GetTransportInfo()
            };
        }

        /// <summary>
        /// Shutdown the network manager
        /// </summary>
        public void Shutdown()
        {
            try
            {
                if (_isServerRunning)
                {
                    StopServer("Server shutdown");
                }

                // Remove event hooks
                if (InstanceFinder.ServerManager != null)
                {
                    InstanceFinder.ServerManager.OnServerConnectionState -= OnServerConnectionState;
                }
                _hooksRegistered = false;

                logger.Msg("Network manager shutdown");
            }
            catch (Exception ex)
            {
                logger.Error($"Error during network manager shutdown: {ex}");
            }
        }
    }

    /// <summary>
    /// Network statistics information
    /// </summary>
    public class NetworkStats
    {
        public bool IsServerRunning { get; set; }
        public TimeSpan Uptime { get; set; }
        public int ServerPort { get; set; }
        public string TransportInfo { get; set; }

        public override string ToString()
        {
            return $"Network Status: {(IsServerRunning ? "Running" : "Stopped")} | " +
                   $"Port: {ServerPort} | " +
                   $"Uptime: {Uptime:hh\\:mm\\:ss}";
        }
    }
}
