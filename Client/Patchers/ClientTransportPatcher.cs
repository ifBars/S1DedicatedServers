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
using System.Collections.Generic;
using System.Reflection;
using DedicatedServerMod.Client.Managers;
using UnityEngine;

namespace DedicatedServerMod.Client.Patchers
{
    /// <summary>
    /// Handles Harmony patches for transport configuration in dedicated server mode.
    /// Manages switching between Steam networking and Tugboat transport.
    /// </summary>
    public class ClientTransportPatcher
    {
        private readonly MelonLogger.Instance logger;
        private HarmonyLib.Harmony harmony;
        private static bool isExiting = false;

        private static readonly FieldInfo ClientTransportField =
            typeof(Multipass).GetField("_clientTransport", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo TransportsListField =
            typeof(Multipass).GetField("_transports", BindingFlags.NonPublic | BindingFlags.Instance);

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

        private void ApplyPatches()
        {
            try
            {
                PatchStartConnection();
                PatchMultipassInitialize();
                PatchConfirmExit();
                logger.Msg("Transport patches applied successfully");
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to apply transport patches: {ex}");
            }
        }

        private void PatchStartConnection()
        {
            var startConnectionMethod = typeof(ClientManager).GetMethod("StartConnection",
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

        private void PatchMultipassInitialize()
        {
            var initializeMethod = typeof(Multipass).GetMethod("Initialize");

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

        private void PatchConfirmExit()
        {
            var confirmExitMethod = typeof(ConfirmExitScreen).GetMethod("ConfirmExit",
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
        /// Safety-net prefix: if ClientManager.StartConnection() is called while in Tugboat mode,
        /// ensure the transport is configured. The primary path now uses
        /// <see cref="SetMultipassClientTransport"/> + direct Tugboat.StartConnection
        /// from ClientConnectionManager, so this prefix is a fallback only.
        /// </summary>
        public static bool StartConnectionPrefix(ClientManager __instance)
        {
            if (!ClientConnectionManager.IsTugboatMode)
                return true;

            try
            {
                var logger = new MelonLogger.Instance("ClientTransportPatcher");
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

                logger.Msg($"StartConnectionPrefix: configured Tugboat fallback to {serverIP}:{serverPort}");
                return true;
            }
            catch (Exception ex)
            {
                var logger = new MelonLogger.Instance("ClientTransportPatcher");
                logger.Error($"Error in StartConnectionPrefix: {ex}");
                return true;
            }
        }

        public static void MultipassInitializePrefix(Multipass __instance)
        {
            try
            {
                var tugboat = __instance.gameObject.GetComponent<Tugboat>();
                if (tugboat == null)
                {
                    tugboat = __instance.gameObject.AddComponent<Tugboat>();
                    AddTugboatToTransportsList(__instance, tugboat);
                    var logger = new MelonLogger.Instance("ClientTransportPatcher");
                    logger.Msg("Added Tugboat component to Multipass");
                }
            }
            catch (Exception ex)
            {
                var logger = new MelonLogger.Instance("ClientTransportPatcher");
                logger.Error($"Error in Multipass Initialize patch: {ex}");
            }
        }

        public static bool ConfirmExitPrefix()
        {
            try
            {
                if (ClientConnectionManager.IsTugboatMode && !isExiting)
                {
                    var logger = new MelonLogger.Instance("ClientTransportPatcher");
                    logger.Msg("ConfirmExit in Tugboat mode - initiating save and disconnect");
                    isExiting = true;
                    MelonCoroutines.Start(SaveAndDisconnectCoroutine());
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                var logger = new MelonLogger.Instance("ClientTransportPatcher");
                logger.Error($"Error in ConfirmExit patch: {ex}");
                return true;
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
                    logger.Error($"Error sending save request: {ex}");
                }
            }

            if (saveRequested)
            {
                logger.Msg("Waiting for save request to complete...");
                yield return new WaitForSeconds(2f);
            }

            try
            {
                if (InstanceFinder.IsClient)
                    InstanceFinder.ClientManager?.StopConnection();

                logger.Msg("Dedicated server disconnection completed");
                Application.Quit();
            }
            catch (Exception ex)
            {
                logger.Error($"Error during disconnection: {ex}");
            }
        }

        #region Public Transport Helpers

        /// <summary>
        /// Sets the client transport on a Multipass instance using cached reflection.
        /// Called by ClientConnectionManager during transport preconfiguration.
        /// </summary>
        public static bool SetMultipassClientTransport(Multipass multipass, Transport transport)
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
        public static void AddTugboatToTransportsList(Multipass multipass, Tugboat tugboat)
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

        public string GetTransportInfo()
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
