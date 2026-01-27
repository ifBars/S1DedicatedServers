using System;
using MelonLoader;

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
    public class ExampleClientMod : API.ClientModBase
    {
        /// <summary>
        /// Called when the client mod is initializing.
        /// Use this for general initialization.
        /// </summary>
        protected override void OnClientInitialize()
        {
            LoggerInstance.Msg("ExampleClientMod initializing...");

            // Example: Subscribe to custom messages from server
            // API.ModManager.NotifyConnectedToServer += OnConnectedToServer;

            LoggerInstance.Msg("ExampleClientMod initialized successfully");
        }

        /// <summary>
        /// Called when the client connects to a dedicated server.
        /// </summary>
        protected override void OnConnectedToServer()
        {
            LoggerInstance.Msg("ExampleClientMod: Connected to server!");

            // Example: Request server data or send identification
            // S1DS.Client.Messaging.SendToServer("request_info");
        }

        /// <summary>
        /// Called when the local player is spawned and systems are ready.
        /// This is the best place for player-specific initialization.
        /// </summary>
        protected override void OnClientPlayerReady()
        {
            LoggerInstance.Msg("ExampleClientMod: Player is ready!");

            // Example: Show custom UI or enable features
            // ShowCustomHUD();
        }

        /// <summary>
        /// Called when the client disconnects from a server.
        /// </summary>
        protected override void OnDisconnectedFromServer()
        {
            LoggerInstance.Msg("ExampleClientMod: Disconnected from server");

            // Example: Clean up UI or state
            // HideCustomHUD();
        }

        /// <summary>
        /// Called when the client is shutting down.
        /// </summary>
        protected override void OnClientShutdown()
        {
            LoggerInstance.Msg("ExampleClientMod: Shutting down...");

            // Example: Perform cleanup
            // CleanupResources();
        }

        /// <summary>
        /// Called when the client receives a custom message from the server.
        /// Return true if this mod handled the message, false otherwise.
        /// </summary>
        /// <param name="messageType">Type of the message</param>
        /// <param name="data">Message data (typically JSON)</param>
        /// <returns>True if handled, false to pass to other mods</returns>
        protected override bool OnCustomMessage(string messageType, byte[] data)
        {
            LoggerInstance.Msg($"Received message from server: {messageType}");

            // Example: Handle specific message types
            // if (messageType == "welcome")
            // {
            //     var welcomeMessage = System.Text.Encoding.UTF8.GetString(data);
            //     ShowWelcomeToast(welcomeMessage);
            //     return true;
            // }

            // if (messageType == "server_info")
            // {
            //     var info = Newtonsoft.Json.JsonConvert.DeserializeObject<ServerInfo>(data);
            //     UpdateServerInfoDisplay(info);
            //     return true;
            // }

            return false; // Not handled by this mod
        }
    }
}
