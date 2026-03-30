using System;
#if IL2CPP
using Il2CppFishNet.Object;
using Il2CppFishNet.Object.Delegating;
using Il2CppFishNet.Serializing;
using Il2CppFishNet.Transporting;
#else
using FishNet.Object;
using FishNet.Object.Delegating;
using FishNet.Serializing;
using FishNet.Transporting;
#endif
using HarmonyLib;
using DedicatedServerMod.Utils;
#if IL2CPP
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.UI;
#else
using ScheduleOne.DevUtilities;
using ScheduleOne.UI;
#endif

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
        /// The custom message ID used for RPC registration.
        /// </summary>
        private const uint MESSAGE_ID = 105u;

        /// <summary>
        /// Initialize the messaging patches with a logger instance.
        /// </summary>
        public static void Initialize()
        {
            DebugLog.StartupDebug("Messaging patches initialized (using attribute-based patching)");
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
                    DebugLog.Debug("Client: DailySummary RPC registration delegated to CustomMessaging");
                }
                catch (Exception ex)
                {
                    DebugLog.Error($"Failed to register custom RPCs: {ex}");
                }
            }
        }

        #endregion

        #region Message Handlers
        
        // Message handlers are now in Shared.Networking.CustomMessaging
        // This patch only delegates RPC registration to the shared implementation
        
        #endregion

        #region Events

        /// <summary>
        /// Event raised when a custom message is received from the server.
        /// Delegates to Shared.Networking.CustomMessaging.ClientMessageReceived.
        /// </summary>
        public static event Action<string, string> ClientMessageReceived
        {
            add => Shared.Networking.CustomMessaging.ClientMessageReceived += value;
            remove => Shared.Networking.CustomMessaging.ClientMessageReceived -= value;
        }

        /// <summary>
        /// Event raised when a custom message is received from a client.
        /// Delegates to Shared.Networking.CustomMessaging.ServerMessageReceived.
        /// </summary>
        public static event Action<FishNet.Connection.NetworkConnection, string, string> ServerMessageReceived
        {
            add => Shared.Networking.CustomMessaging.ServerMessageReceived += value;
            remove => Shared.Networking.CustomMessaging.ServerMessageReceived -= value;
        }

        #endregion
    }
}
