using FishNet;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using MelonLoader;
using System;
using System.Reflection;

namespace DedicatedServerMod
{
    /// <summary>
    /// Manages transport switching between Steam networking and Tugboat
    /// for dedicated server connections.
    /// </summary>
    public static class TransportManager
    {
        private static MelonLogger.Instance logger = new MelonLogger.Instance("TransportManager");

        /// <summary>
        /// Ensures Tugboat transport is available in the Multipass system.
        /// </summary>
        public static bool EnsureTugboatAvailable()
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

                // Check if Tugboat is already available
                var tugboat = multipass.gameObject.GetComponent<Tugboat>();
                if (tugboat != null)
                {
                    logger.Msg("Tugboat already available");
                    return true;
                }

                // Add Tugboat component
                tugboat = multipass.gameObject.AddComponent<Tugboat>();
                if (tugboat == null)
                {
                    logger.Error("Failed to add Tugboat component");
                    return false;
                }

                logger.Msg("Successfully added Tugboat transport");
                return true;
            }
            catch (Exception ex)
            {
                logger.Error($"Error ensuring Tugboat availability: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Configures Tugboat transport for connection to a specific server.
        /// </summary>
        public static bool ConfigureTugboatConnection(string serverIP, int port)
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

                var tugboat = multipass.gameObject.GetComponent<Tugboat>();
                if (tugboat == null)
                {
                    logger.Error("Tugboat component not found - call EnsureTugboatAvailable first");
                    return false;
                }

                // Configure Tugboat connection parameters
                tugboat.SetClientAddress(serverIP);
                tugboat.SetPort((ushort)port);

                // Set as active client transport using reflection
                SetClientTransport(multipass, tugboat);

                logger.Msg($"Configured Tugboat for {serverIP}:{port}");
                return true;
            }
            catch (Exception ex)
            {
                logger.Error($"Error configuring Tugboat connection: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Uses reflection to set the client transport in Multipass.
        /// This avoids direct field access and maintains compatibility.
        /// </summary>
        private static void SetClientTransport(Multipass multipass, Transport transport)
        {
            try
            {
                // Access private field using reflection (safe alternative to Harmony field access)
                var clientTransportField = typeof(Multipass).GetField("_clientTransport", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (clientTransportField != null)
                {
                    clientTransportField.SetValue(multipass, transport);
                    logger.Msg("Successfully set client transport to Tugboat");
                }
                else
                {
                    // Try alternative method if field name changed
                    logger.Warning("Could not find _clientTransport field, trying alternative approach");
                    TryAlternativeTransportSet(multipass, transport);
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error setting client transport: {ex}");
            }
        }

        /// <summary>
        /// Alternative method to set transport if reflection fails.
        /// </summary>
        private static void TryAlternativeTransportSet(Multipass multipass, Transport transport)
        {
            try
            {
                // Look for any method that might set the client transport
                var methods = typeof(Multipass).GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                
                foreach (var method in methods)
                {
                    if (method.Name.Contains("SetClientTransport") && method.GetParameters().Length == 1)
                    {
                        var paramType = method.GetParameters()[0].ParameterType;
                        if (paramType.IsAssignableFrom(typeof(Transport)))
                        {
                            method.Invoke(multipass, new object[] { transport });
                            logger.Msg("Set client transport using alternative method");
                            return;
                        }
                    }
                }
                
                logger.Warning("No alternative method found for setting client transport");
            }
            catch (Exception ex)
            {
                logger.Error($"Error in alternative transport set: {ex}");
            }
        }

        /// <summary>
        /// Restores default Steam transport for normal multiplayer.
        /// This allows switching back to standard game networking.
        /// </summary>
        public static bool RestoreDefaultTransport()
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

                // Look for FishySteamworks transport (standard game transport)
                var steamTransport = multipass.gameObject.GetComponent<FishySteamworks.FishySteamworks>();
                if (steamTransport != null)
                {
                    SetClientTransport(multipass, steamTransport);
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
        /// Gets information about available transports for debugging.
        /// </summary>
        public static string GetTransportInfo()
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
    }
}
