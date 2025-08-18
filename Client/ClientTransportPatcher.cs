using FishNet;
using FishNet.Managing.Client;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using HarmonyLib;
using MelonLoader;
using ScheduleOne.UI.MainMenu;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace DedicatedServerMod.Client
{
    /// <summary>
    /// Handles Harmony patches for transport configuration in dedicated server mode.
    /// Manages switching between Steam networking and Tugboat transport.
    /// </summary>
    public class ClientTransportPatcher
    {
        private readonly MelonLogger.Instance logger;
        private HarmonyLib.Harmony harmony;
        private bool isExiting = false;

        public ClientTransportPatcher(MelonLogger.Instance logger)
        {
            this.logger = logger;
        }

        public void Initialize()
        {
            try
            {
                logger.Msg("Initializing ClientTransportPatcher");
                
                harmony = new HarmonyLib.Harmony("DedicatedServerMod.ClientTransportPatcher");
                ApplyPatches();
                
                logger.Msg("ClientTransportPatcher initialized");
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to initialize ClientTransportPatcher: {ex}");
            }
        }

        /// <summary>
        /// Apply all necessary Harmony patches
        /// </summary>
        private void ApplyPatches()
        {
            try
            {
                // Patch ClientManager.StartConnection to intercept connection attempts
                PatchStartConnection();
                
                // Patch Multipass.Initialize to add Tugboat transport
                PatchMultipassInitialize();
                
                // Patch ConfirmExitScreen.ConfirmExit to handle dedicated server disconnection
                PatchConfirmExit();
                
                logger.Msg("Transport patches applied successfully");
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to apply transport patches: {ex}");
            }
        }

        /// <summary>
        /// Patch ClientManager.StartConnection to handle Tugboat connections
        /// </summary>
        private void PatchStartConnection()
        {
            var clientManagerType = typeof(ClientManager);
            var startConnectionMethod = clientManagerType.GetMethod("StartConnection", 
                BindingFlags.Public | BindingFlags.Instance, null, new Type[0], null);
            
            if (startConnectionMethod != null)
            {
                var prefixMethod = typeof(ClientTransportPatcher).GetMethod(nameof(StartConnectionPrefix), 
                    BindingFlags.Static | BindingFlags.Public);
                harmony.Patch(startConnectionMethod, new HarmonyMethod(prefixMethod));
                logger.Msg("Patched ClientManager.StartConnection");
            }
            else
            {
                logger.Error("Could not find ClientManager.StartConnection method");
            }
        }

        /// <summary>
        /// Patch Multipass.Initialize to ensure Tugboat is available
        /// </summary>
        private void PatchMultipassInitialize()
        {
            var multipassType = typeof(Multipass);
            var initializeMethod = multipassType.GetMethod("Initialize");
            
            if (initializeMethod != null)
            {
                var prefixMethod = typeof(ClientTransportPatcher).GetMethod(nameof(MultipassInitializePrefix), 
                    BindingFlags.Static | BindingFlags.Public);
                harmony.Patch(initializeMethod, new HarmonyMethod(prefixMethod));
                logger.Msg("Patched Multipass.Initialize");
            }
            else
            {
                logger.Error("Could not find Multipass.Initialize method");
            }
        }

        /// <summary>
        /// Patch ConfirmExitScreen.ConfirmExit to handle dedicated server disconnection
        /// </summary>
        private void PatchConfirmExit()
        {
            var confirmExitScreenType = typeof(ConfirmExitScreen);
            var confirmExitMethod = confirmExitScreenType.GetMethod("ConfirmExit", 
                BindingFlags.Public | BindingFlags.Instance);
            
            if (confirmExitMethod != null)
            {
                var prefixMethod = typeof(ClientTransportPatcher).GetMethod(nameof(ConfirmExitPrefix), 
                    BindingFlags.Static | BindingFlags.Public);
                harmony.Patch(confirmExitMethod, new HarmonyMethod(prefixMethod));
                logger.Msg("Patched ConfirmExitScreen.ConfirmExit");
            }
            else
            {
                logger.Error("Could not find ConfirmExitScreen.ConfirmExit method");
            }
        }

        /// <summary>
        /// Harmony patch for ClientManager.StartConnection
        /// Intercepts connection attempts and uses Tugboat when in dedicated server mode
        /// </summary>
        public static bool StartConnectionPrefix(ClientManager __instance, ref bool __result)
        {
            if (!ClientConnectionManager.IsTugboatMode)
            {
                return true; // Execute original method for normal connections
            }

            try
            {
                var logger = new MelonLogger.Instance("ClientTransportPatcher");
                var (serverIP, serverPort) = ClientConnectionManager.GetTargetServer();
                
                logger.Msg($"Intercepting StartConnection for Tugboat connection to {serverIP}:{serverPort}");
                
                // Get network components
                var networkManager = InstanceFinder.NetworkManager;
                if (networkManager == null)
                {
                    logger.Error("NetworkManager not found");
                    return true;
                }

                var transportManager = networkManager.TransportManager;
                var transport = transportManager.Transport;
                var multipass = transport as Multipass;
                
                if (multipass == null)
                {
                    logger.Error("Multipass transport not found");
                    return true;
                }

                // Get or configure Tugboat component
                var tugboat = multipass.gameObject.GetComponent<Tugboat>();
                if (tugboat == null)
                {
                    logger.Error("Tugboat component not found - should have been added in Initialize patch");
                    return true;
                }

                // Configure Tugboat connection
                tugboat.SetClientAddress(serverIP);
                tugboat.SetPort((ushort)serverPort);
                SetClientTransport(multipass, tugboat);
                
                // Start Tugboat connection
                __result = tugboat.StartConnection(false);
                
                logger.Msg($"Tugboat connection started: {__result}, IP: {serverIP}, Port: {serverPort}");
                
                return false; // Skip original method
            }
            catch (Exception ex)
            {
                var logger = new MelonLogger.Instance("ClientTransportPatcher");
                logger.Error($"Error in StartConnection patch: {ex}");
                return true; // Fallback to original method
            }
        }

        /// <summary>
        /// Harmony patch for Multipass.Initialize
        /// Ensures Tugboat transport is available in the system
        /// </summary>
        public static void MultipassInitializePrefix(Multipass __instance)
        {
            try
            {
                var logger = new MelonLogger.Instance("ClientTransportPatcher");
                
                var tugboat = __instance.gameObject.GetComponent<Tugboat>();
                if (tugboat == null)
                {
                    tugboat = __instance.gameObject.AddComponent<Tugboat>();
                    logger.Msg("Added Tugboat component to Multipass");
                    
                    // Add to transports list using reflection
                    AddTugboatToTransportsList(__instance, tugboat);
                }
                else
                {
                    logger.Msg("Tugboat component already exists on Multipass");
                }
            }
            catch (Exception ex)
            {
                var logger = new MelonLogger.Instance("ClientTransportPatcher");
                logger.Error($"Error in Multipass Initialize patch: {ex}");
            }
        }

        /// <summary>
        /// Harmony patch for ConfirmExitScreen.ConfirmExit
        /// Handles disconnection from dedicated server when in Tugboat mode
        /// </summary>
        public static bool ConfirmExitPrefix()
        {
            try
            {
                var logger = new MelonLogger.Instance("ClientTransportPatcher");
                
                if (ClientConnectionManager.IsTugboatMode && !isExiting)
                {
                    logger.Msg("ConfirmExit called while in Tugboat mode - initiating save and disconnect sequence");
                    isExiting = true;
                    // Start coroutine to save player data and then disconnect
                    MelonCoroutines.Start(SaveAndDisconnectCoroutine());
                    
                    // Return false to prevent the original method from running
                    return false;
                }
                
                // Return true to allow the original method to run for non-Tugboat mode
                return true;
            }
            catch (Exception ex)
            {
                var logger = new MelonLogger.Instance("ClientTransportPatcher");
                logger.Error($"Error in ConfirmExit patch: {ex}");
                
                // Return true to allow the original method to run as fallback
                return true;
            }
        }

        /// <summary>
        /// Coroutine to save player data and then disconnect with proper timing
        /// </summary>
        private static IEnumerator SaveAndDisconnectCoroutine()
        {
            var logger = new MelonLogger.Instance("ClientTransportPatcher");
            bool saveRequested = false;
            
            // Save player data before disconnecting
            if (ScheduleOne.PlayerScripts.Player.Local != null)
            {
                try
                {
                    logger.Msg("Sending save request to server");
                    ScheduleOne.PlayerScripts.Player.Local.RequestSavePlayer();
                    saveRequested = true;
                }
                catch (Exception ex)
                {
                    logger.Error($"Error sending save request: {ex}");
                }
            }
            
            // Wait for the save request to be sent and processed (if one was sent)
            if (saveRequested)
            {
                logger.Msg("Waiting for save request to complete...");
                yield return new WaitForSeconds(2f);
            }
            
            // Stop the client connection if connected
            try
            {
                if (InstanceFinder.IsClient)
                {
                    var clientManager = InstanceFinder.ClientManager;
                    if (clientManager != null)
                    {
                        logger.Msg("Stopping client connection via ClientManager");
                        clientManager.StopConnection();
                    }
                }
                
                logger.Msg("Dedicated server disconnection completed");
                
                Application.Quit();
            }
            catch (Exception ex)
            {
                logger.Error($"Error during disconnection: {ex}");
            }
        }

        /// <summary>
        /// Add Tugboat to the internal transports list using reflection
        /// </summary>
        private static void AddTugboatToTransportsList(Multipass multipass, Tugboat tugboat)
        {
            try
            {
                var transportsField = typeof(Multipass).GetField("_transports", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (transportsField != null)
                {
                    var transports = transportsField.GetValue(multipass) as System.Collections.Generic.List<Transport>;
                    if (transports != null)
                    {
                        transports.Add(tugboat);
                        var logger = new MelonLogger.Instance("ClientTransportPatcher");
                        logger.Msg("Added Tugboat to transports list");
                    }
                }
            }
            catch (Exception ex)
            {
                var logger = new MelonLogger.Instance("ClientTransportPatcher");
                logger.Error($"Error adding Tugboat to transports list: {ex}");
            }
        }

        /// <summary>
        /// Set the client transport in Multipass using reflection
        /// </summary>
        private static void SetClientTransport(Multipass multipass, Transport transport)
        {
            try
            {
                var clientTransportField = typeof(Multipass).GetField("_clientTransport", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (clientTransportField != null)
                {
                    clientTransportField.SetValue(multipass, transport);
                    var logger = new MelonLogger.Instance("ClientTransportPatcher");
                    logger.Msg("Set Tugboat as client transport");
                }
                else
                {
                    var logger = new MelonLogger.Instance("ClientTransportPatcher");
                    logger.Warning("Could not find _clientTransport field");
                }
            }
            catch (Exception ex)
            {
                var logger = new MelonLogger.Instance("ClientTransportPatcher");
                logger.Error($"Error setting client transport: {ex}");
            }
        }

        /// <summary>
        /// Get information about available transports for debugging
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
    }
}

