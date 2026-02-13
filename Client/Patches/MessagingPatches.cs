using System;
using DedicatedServerMod.Client.Managers;
using FishNet.Object;
using FishNet.Object.Delegating;
using FishNet.Serializing;
using FishNet.Transporting;
using HarmonyLib;
using MelonLoader;
using ScheduleOne.DevUtilities;
using ScheduleOne.UI;
using UnityEngine;

namespace DedicatedServerMod.Client.Patches
{
    /// <summary>
    /// Harmony patches for the custom messaging system.
    /// Registers custom RPC handlers on the DailySummary NetworkBehaviour.
    /// </summary>
    /// <remarks>
    /// This class handles the registration of custom RPC methods for
    /// bidirectional communication between server and client mods.
    /// </remarks>
    internal static class MessagingPatches
    {
        /// <summary>
        /// The logger instance for this patch class.
        /// </summary>
        private static MelonLogger.Instance _logger;

        /// <summary>
        /// The custom message ID used for RPC registration.
        /// </summary>
        private const uint MESSAGE_ID = 105u;

        /// <summary>
        /// Initialize the messaging patches with a logger instance.
        /// </summary>
        /// <param name="logger">The logger instance to use</param>
        public static void Initialize(MelonLogger.Instance logger)
        {
            _logger = logger;
            _logger.Msg("Messaging patches initialized (using attribute-based patching)");
        }

        #region DailySummary.Awake Patch

        /// <summary>
        /// Harmony postfix patch for DailySummary.Awake.
        /// Registers custom RPC handlers for the messaging system.
        /// </summary>
        /// <remarks>
        /// This patch registers custom message handlers after DailySummary initializes,
        /// allowing the mod to send and receive custom messages between client and server.
        /// Uses the shared CustomMessaging.DailySummaryAwakePostfix to avoid duplication.
        /// </remarks>
        [HarmonyPatch(typeof(DailySummary), "Awake")]
        private static class DailySummary_Awake_Postfix
        {
            private static void Postfix(DailySummary __instance)
            {
                try
                {
                    Shared.Networking.CustomMessaging.DailySummaryAwakePostfix(__instance);
                    _logger?.Msg("Client: DailySummary RPC registration delegated to CustomMessaging");
                    
                    // Delay sending messages slightly to ensure DailySummary.Instance is set
                    MelonCoroutines.Start(SendDelayedClientMessages());
                }
                catch (Exception ex)
                {
                    _logger?.Error($"Failed to register custom RPCs: {ex}");
                }
            }
        }

        /// <summary>
        /// Sends client_ready and request_server_data messages after a short delay
        /// to ensure DailySummary.Instance is properly initialized.
        /// </summary>
        private static System.Collections.IEnumerator SendDelayedClientMessages()
        {
            // Wait for DailySummary to be spawned (up to 5 seconds)
            float timeout = 5f;
            float elapsed = 0f;
            
            while (elapsed < timeout)
            {
                var ds = DailySummary.Instance;
                if (ds != null)
                {
                    var nb = ds as NetworkBehaviour;
                    if (nb != null && nb.IsSpawned)
                    {
                        _logger?.Msg("DailySummary is spawned, sending client messages");
                        break;
                    }
                }
                
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }
            
            if (elapsed >= timeout)
            {
                _logger?.Warning("Timeout waiting for DailySummary to spawn");
            }
            
            try
            {
                // Send "client ready" message to server so it knows we can receive authentication challenges
                SendClientReadyMessage();
                
                // Now that RPCs are registered, request server data if connected
                RequestServerDataIfConnected();
            }
            catch (Exception ex)
            {
                _logger?.Error($"Error sending delayed client messages: {ex}");
            }
        }

        #endregion

        #region Message Handlers
        
        // Message handlers are now in Shared.Networking.CustomMessaging
        // This patch only delegates RPC registration to the shared implementation
        
        /// <summary>
        /// Sends a "client ready" message to the server indicating RPCs are registered
        /// and the client is ready to receive authentication challenges.
        /// </summary>
        private static void SendClientReadyMessage()
        {
            try
            {
                if (FishNet.InstanceFinder.IsClient && !FishNet.InstanceFinder.IsServer)
                {
                    _logger?.Msg("Sending client_ready message to server");
                    Shared.Networking.CustomMessaging.SendToServer("client_ready");
                }
            }
            catch (Exception ex)
            {
                _logger?.Warning($"Failed to send client_ready message: {ex.Message}");
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Event raised when a custom message is received from the server.
        /// Delegates to Shared.Networking.CustomMessaging.ClientMessageReceived.
        /// </summary>
        public static event System.Action<string, string> ClientMessageReceived
        {
            add => Shared.Networking.CustomMessaging.ClientMessageReceived += value;
            remove => Shared.Networking.CustomMessaging.ClientMessageReceived -= value;
        }

        /// <summary>
        /// Event raised when a custom message is received from a client.
        /// Delegates to Shared.Networking.CustomMessaging.ServerMessageReceived.
        /// </summary>
        public static event System.Action<FishNet.Connection.NetworkConnection, string, string> ServerMessageReceived
        {
            add => Shared.Networking.CustomMessaging.ServerMessageReceived += value;
            remove => Shared.Networking.CustomMessaging.ServerMessageReceived -= value;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Requests server data if connected to a server.
        /// Called after RPC registration to ensure DailySummary instance is ready.
        /// </summary>
        private static void RequestServerDataIfConnected()
        {
            try
            {
                // Check if we're connected as a client
                if (FishNet.InstanceFinder.IsClient && !FishNet.InstanceFinder.IsServer)
                {
                    _logger?.Msg("Client connected - requesting initial server data");
                    Shared.Networking.CustomMessaging.SendToServer("request_server_data");
                }
            }
            catch (Exception ex)
            {
                _logger?.Warning($"Failed to request server data after RPC registration: {ex.Message}");
            }
        }

        #endregion
    }
}
