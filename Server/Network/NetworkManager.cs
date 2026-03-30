#if IL2CPP
using Il2CppFishNet;
using Il2CppFishNet.Managing;
using Il2CppFishNet.Transporting;
using Il2CppFishNet.Transporting.Multipass;
using Il2CppFishNet.Transporting.Tugboat;
#else
using FishNet;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
#endif
using MelonLoader;
using System.Collections;
using System.Reflection;
using DedicatedServerMod.Shared.Configuration;
using DedicatedServerMod.Utils;

namespace DedicatedServerMod.Server.Network
{
    /// <summary>
    /// Manages network transport, server connections, and network lifecycle.
    /// Handles switching between Steam networking and Tugboat for dedicated servers.
    /// </summary>
    public sealed class NetworkManager
    {
        private readonly MelonLogger.Instance logger;
        private DateTime serverStartTime;
        private bool isServerRunning = false;
        private bool hooksRegistered = false;

        internal NetworkManager(MelonLogger.Instance loggerInstance)
        {
            logger = loggerInstance;
        }

        /// <summary>
        /// Gets whether the server is currently running
        /// </summary>
        public bool IsServerRunning => isServerRunning;

        /// <summary>
        /// Gets the server uptime
        /// </summary>
        public TimeSpan Uptime => isServerRunning ? DateTime.Now - serverStartTime : TimeSpan.Zero;

        /// <summary>
        /// Initialize the network manager
        /// </summary>
        internal void Initialize()
        {
            try
            {
                MelonCoroutines.Start(WaitAndRegisterHooks());
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to initialize network manager: {ex}");
                throw;
            }
        }

        private bool TryRegisterHooks()
        {
            if (hooksRegistered) return true;
            var serverManager = InstanceFinder.ServerManager;
            if (serverManager == null) return false;
#if MONO
            serverManager.OnServerConnectionState -= OnServerConnectionState;
            serverManager.OnServerConnectionState += OnServerConnectionState;
#else
                DebugLog.ServerNetworkDebug("Skipping direct OnServerConnectionState hook on IL2CPP runtime");
#endif
                hooksRegistered = true;
            DebugLog.ServerNetworkDebug("Network event hooks established");
            return true;
        }

        private IEnumerator WaitAndRegisterHooks()
        {
            while (InstanceFinder.NetworkManager == null || InstanceFinder.ServerManager == null)
                yield return null;
            TryRegisterHooks();
        }

        /// <summary>
        /// Handle server connection state changes
        /// </summary>
        private void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            DebugLog.ServerNetworkDebug($"Server connection state: {args.ConnectionState} (transport index {args.TransportIndex})");

            switch (args.ConnectionState)
            {
                case LocalConnectionState.Starting:
                    if (isServerRunning)
                    {
                        DebugLog.ServerNetworkDebug($"Additional server transport entered Starting after server was already online (transport index {args.TransportIndex})");
                    }
                    break;

                case LocalConnectionState.Started:
                    if (isServerRunning)
                    {
                        DebugLog.ServerNetworkDebug($"Additional server transport reported Started after server was already online (transport index {args.TransportIndex})");
                        break;
                    }

                    isServerRunning = true;
                    serverStartTime = DateTime.Now;
                    logger.Msg($"=== DEDICATED SERVER ONLINE ===");
                    logger.Msg($"Server Name: {ServerConfig.Instance.ServerName}");
                    logger.Msg($"Port: {ServerConfig.Instance.ServerPort}");
                    logger.Msg($"Max Players: {ServerConfig.Instance.MaxPlayers}");
                    break;

                case LocalConnectionState.Stopped:
                    if (!isServerRunning)
                    {
                        DebugLog.ServerNetworkDebug($"Server transport reported Stopped while server was already offline (transport index {args.TransportIndex})");
                        break;
                    }

                    isServerRunning = false;
                    logger.Msg($"=== DEDICATED SERVER OFFLINE ===");
                    break;
            }
        }

        /// <summary>
        /// Set the server transport using reflection
        /// </summary>
        private bool SetServerTransport(Multipass multipass, Transport transport)
        {
            try
            {
                // Prefer method if available
                var methods = typeof(Multipass).GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                foreach (var method in methods)
                {
                    if (method.Name.Contains("SetServerTransport") && method.GetParameters().Length == 1)
                    {
                        var paramType = method.GetParameters()[0].ParameterType;
                        if (paramType.IsAssignableFrom(typeof(Transport)))
                        {
                            method.Invoke(multipass, new object[] { transport });
                            DebugLog.ServerNetworkDebug("Set server transport using Multipass.SetServerTransport");
                            return true;
                        }
                    }
                }

                // Fallback to field
                var serverTransportField = typeof(Multipass).GetField("_serverTransport",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (serverTransportField != null)
                {
                    serverTransportField.SetValue(multipass, transport);
                    DebugLog.ServerNetworkDebug("Set server transport via _serverTransport field");
                    return true;
                }

                logger.Warning("Could not set server transport (no method/field found)");
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
        /// Get network statistics
        /// </summary>
        public NetworkStats GetNetworkStats()
        {
            return new NetworkStats
            {
                IsServerRunning = isServerRunning,
                Uptime = Uptime,
                ServerPort = ServerConfig.Instance.ServerPort,
                TransportInfo = GetTransportInfo()
            };
        }

        /// <summary>
        /// Shutdown the network manager
        /// </summary>
        internal void Shutdown()
        {
            try
            {
                if (isServerRunning)
                {
                    StopServer("Server shutdown");
                }

                // Remove event hooks
                if (InstanceFinder.ServerManager != null)
                {
#if MONO
                    InstanceFinder.ServerManager.OnServerConnectionState -= OnServerConnectionState;
#endif
                }
                hooksRegistered = false;

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
