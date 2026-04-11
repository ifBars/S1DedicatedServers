using MelonLoader;
using DedicatedServerMod.Shared.Networking;

[assembly: MelonInfo(typeof(ExampleClientMod), "ExampleClientMod", "1.0.0", "Example Author")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace DedicatedServerMod.Examples.Client
{
    /// <summary>
    /// Example client mod demonstrating the DedicatedServerMod client API.
    /// This mod shows how to use client-side mod lifecycle hooks and messaging.
    /// </summary>
    /// <remarks>
    /// To use this example:
    /// 1. Copy this file to your mod project
    /// 2. Add reference to DedicatedServerMod.dll
    /// 3. Implement your mod logic
    /// 4. Build and place in Schedule I/Mods/
    /// </remarks>
    public class ExampleClientMod : API.ClientMelonModBase
    {
        /// <summary>
        /// Called when the client mod is initializing.
        /// Use this to attach event-first client hooks.
        /// </summary>
        public override void OnClientInitialize()
        {
            LoggerInstance.Msg("ExampleClientMod initializing...");

            API.ModManager.ClientConnectedToServer += HandleConnectedToServer;
            API.ModManager.ClientDisconnectedFromServer += HandleDisconnectedFromServer;
            API.ModManager.ClientPlayerReady += HandleClientPlayerReady;
            API.ModManager.ClientCustomMessageReceived += HandleCustomMessage;

            LoggerInstance.Msg("ExampleClientMod initialized successfully");
        }

        /// <summary>
        /// Called when the client is shutting down.
        /// </summary>
        public override void OnClientShutdown()
        {
            API.ModManager.ClientCustomMessageReceived -= HandleCustomMessage;
            API.ModManager.ClientPlayerReady -= HandleClientPlayerReady;
            API.ModManager.ClientDisconnectedFromServer -= HandleDisconnectedFromServer;
            API.ModManager.ClientConnectedToServer -= HandleConnectedToServer;

            LoggerInstance.Msg("ExampleClientMod: Shutting down...");
        }

        /// <summary>
        /// Handles the event-first dedicated-server connection notification.
        /// </summary>
        private void HandleConnectedToServer()
        {
            LoggerInstance.Msg("ExampleClientMod: Connected to server!");

            // Example: Request server data or send identification.
            // CustomMessaging.SendToServer("request_info");
        }

        /// <summary>
        /// Handles the event-first local-player-ready notification.
        /// </summary>
        private void HandleClientPlayerReady()
        {
            LoggerInstance.Msg("ExampleClientMod: Player is ready!");

            // Example: Show custom UI or enable features.
            // ShowCustomHUD();
        }

        /// <summary>
        /// Handles the event-first dedicated-server disconnect notification.
        /// </summary>
        private void HandleDisconnectedFromServer()
        {
            LoggerInstance.Msg("ExampleClientMod: Disconnected from server");

            // Example: Clean up UI or state.
            // HideCustomHUD();
        }

        /// <summary>
        /// Handles forwarded custom messages from the server.
        /// </summary>
        /// <param name="messageType">Type of the message.</param>
        /// <param name="data">Message data, typically JSON.</param>
        private void HandleCustomMessage(string messageType, byte[] data)
        {
            LoggerInstance.Msg($"Received message from server: {messageType}");

            // Example: Handle specific message types here.
        }
    }
}
