#if IL2CPP
using Il2CppFishNet;
using Il2CppFishNet.Managing.Client;
using Il2CppFishNet.Transporting;
using Il2CppFishNet.Transporting.Multipass;
using Il2CppFishNet.Transporting.Tugboat;
#else
using FishNet;
using FishNet.Managing.Client;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
#endif
using HarmonyLib;
using MelonLoader;
#if IL2CPP
using Il2CppScheduleOne.UI.MainMenu;
#else
using ScheduleOne.UI.MainMenu;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DedicatedServerMod.Client.Managers;
using DedicatedServerMod.Utils;
using UnityEngine;

namespace DedicatedServerMod.Client.Patchers
{
    /// <summary>
    /// Handles Harmony patches for transport configuration in dedicated server mode.
    /// Manages switching between Steam networking and Tugboat transport.
    /// </summary>
    internal class ClientTransportPatcher
    {
        private static bool isExiting = false;

        private static readonly FieldInfo ClientTransportField =
            typeof(Multipass).GetField("_clientTransport", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo TransportsListField =
            typeof(Multipass).GetField("_transports", BindingFlags.NonPublic | BindingFlags.Instance);

        [HarmonyPatch]
        private static class StartConnectionPatch
        {
            private static MethodBase TargetMethod()
            {
                return typeof(ClientManager).GetMethod(
                    "StartConnection",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null);
            }

            /// <summary>
            /// Safety-net prefix: if ClientManager.StartConnection() is called while in Tugboat mode,
            /// ensure the transport is configured. The primary path now uses
            /// <see cref="SetMultipassClientTransport"/> + direct Tugboat.StartConnection
            /// from ClientConnectionManager, so this prefix is a fallback only.
            /// </summary>
            [HarmonyPrefix]
            private static bool Prefix(ClientManager __instance)
            {
                if (!ClientConnectionManager.IsTugboatMode)
                    return true;

                try
                {
                    var (serverIP, serverPort) = ClientConnectionManager.GetTargetServer();

                    var multipass = InstanceFinder.NetworkManager?.TransportManager?.Transport as Multipass;
                    if (multipass == null)
                        return true;

                    var tugboat = multipass.gameObject.GetComponent<Tugboat>();
                    if (tugboat == null)
                        return true;

                    tugboat.SetClientAddress(serverIP);
                    tugboat.SetPort((ushort)serverPort);
                    SetMultipassClientTransport(multipass, tugboat);

                    DebugLog.ServerNetworkDebug($"StartConnectionPrefix: configured Tugboat fallback to {serverIP}:{serverPort}");
                    return true;
                }
                catch (Exception ex)
                {
                    DebugLog.Error("Error in StartConnectionPrefix", ex);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(Multipass), "Initialize")]
        private static class MultipassInitializePatch
        {
            [HarmonyPrefix]
            private static void Prefix(Multipass __instance)
            {
                try
                {
                    var tugboat = __instance.gameObject.GetComponent<Tugboat>();
                    if (tugboat == null)
                    {
                        tugboat = __instance.gameObject.AddComponent<Tugboat>();
                        AddTugboatToTransportsList(__instance, tugboat);
                    }
                }
                catch (Exception ex)
                {
                    DebugLog.Error("Error in Multipass Initialize patch", ex);
                }
            }
        }

        [HarmonyPatch(typeof(ConfirmExitScreen), nameof(ConfirmExitScreen.ConfirmExit))]
        private static class ConfirmExitPatch
        {
            [HarmonyPrefix]
            private static bool Prefix()
            {
                try
                {
                    if (ClientConnectionManager.IsTugboatMode && !isExiting)
                    {
                        isExiting = true;
                        MelonCoroutines.Start(SaveAndDisconnectCoroutine());
                        return false;
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    DebugLog.Error("Error in ConfirmExit patch", ex);
                    return true;
                }
            }
        }

        private static IEnumerator SaveAndDisconnectCoroutine()
        {
            var logger = new MelonLogger.Instance("ClientTransportPatcher");
            bool saveRequested = false;

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
                    DebugLog.Error("Error sending save request", ex);
                }
            }

            if (saveRequested)
            {
                yield return new WaitForSeconds(2f);
            }

            try
            {
                Core.ClientBootstrap.Instance?.ConnectionManager?.DisconnectFromDedicatedServer();
            }
            catch (Exception ex)
            {
                DebugLog.Error("Error during disconnection", ex);
            }
            finally
            {
                isExiting = false;
            }
        }

        #region Public Transport Helpers

        /// <summary>
        /// Sets the client transport on a Multipass instance using cached reflection.
        /// Called by ClientConnectionManager during transport preconfiguration.
        /// </summary>
        internal static bool SetMultipassClientTransport(Multipass multipass, Transport transport)
        {
            if (ClientTransportField != null)
            {
                ClientTransportField.SetValue(multipass, transport);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Adds a Tugboat transport to the Multipass internal transports list.
        /// </summary>
        internal static void AddTugboatToTransportsList(Multipass multipass, Tugboat tugboat)
        {
            try
            {
                if (TransportsListField != null)
                {
                    var transports = TransportsListField.GetValue(multipass) as List<Transport>;
                    if (transports != null && !transports.Contains(tugboat))
                        transports.Add(tugboat);
                }
            }
            catch (Exception ex)
            {
                var logger = new MelonLogger.Instance("ClientTransportPatcher");
                logger.Error($"Error adding Tugboat to transports list: {ex}");
            }
        }

        #endregion

        internal string GetTransportInfo()
        {
            try
            {
                var multipass = InstanceFinder.NetworkManager?.TransportManager?.Transport as Multipass;
                if (multipass == null)
                    return "Multipass transport not found";

                var components = multipass.gameObject.GetComponents<Transport>();
                var info = $"Available transports on Multipass: {components.Length}\n";
                foreach (var comp in components)
                    info += $"- {comp.GetType().Name}\n";
                return info;
            }
            catch (Exception ex)
            {
                return $"Error getting transport info: {ex.Message}";
            }
        }
    }
}
