using System;
using DedicatedServerMod.API.Metadata;
using DedicatedServerMod.Server.Player;
using MelonLoader;

[assembly: MelonInfo(typeof(ExampleServerMod), "ExampleServerMod", "1.0.0", "Example Author")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace DedicatedServerMod.Examples.Server
{
    /// <summary>
    /// Example server mod demonstrating the DedicatedServerMod API.
    /// This mod shows how to use the server mod lifecycle hooks and messaging.
    /// </summary>
    /// <remarks>
    /// To use this example:
    /// 1. Copy this file to your mod project
    /// 2. Add reference to DedicatedServerMod.dll
    /// 3. Implement your mod logic
    /// 4. Build and place in Schedule I/Mods/
    /// </remarks>
    public class ExampleServerMod : API.ServerMelonModBase
    {
        /// <summary>
        /// Called when the server is starting up.
        /// Use this for initialization that should happen before players connect.
        /// </summary>
        public override void OnServerInitialize()
        {
            LoggerInstance.Msg("ExampleServerMod initializing...");

            // Example: Register custom commands
            // CommandManager.Instance.RegisterCommand(new MyCustomCommand());

            // Example: Subscribe to event-first lifecycle hooks.
            API.ModManager.ServerPlayerConnected += HandlePlayerConnected;
            API.ModManager.ServerPlayerDisconnected += HandlePlayerDisconnected;
            API.ModManager.ServerCustomMessageReceived += HandleCustomMessageReceived;

            LoggerInstance.Msg("ExampleServerMod initialized successfully");
        }

        /// <summary>
        /// Called when the server is fully started and ready to accept connections.
        /// </summary>
        public override void OnServerStarted()
        {
            LoggerInstance.Msg("ExampleServerMod: Server is now running!");

            // Example: Broadcast a welcome message
            // Shared.Networking.CustomMessaging.BroadcastToClients("welcome", "Welcome to my modded server!");
        }

        /// <summary>
        /// Called before the server saves data.
        /// Use this to save your mod's custom data.
        /// </summary>
        public override void OnBeforeSave()
        {
            LoggerInstance.Msg("ExampleServerMod: Saving custom data...");

            // Example: Save custom data to a file or to the game's save system
            // SaveCustomDataToFile();
        }

        /// <summary>
        /// Called after the server saves data.
        /// </summary>
        public override void OnAfterSave()
        {
            LoggerInstance.Msg("ExampleServerMod: Save complete");
        }

        /// <summary>
        /// Called before the server loads data.
        /// </summary>
        public override void OnBeforeLoad()
        {
            LoggerInstance.Msg("ExampleServerMod: Preparing to load data...");
        }

        /// <summary>
        /// Called after the server loads data.
        /// Use this to load your mod's custom data.
        /// </summary>
        public override void OnAfterLoad()
        {
            LoggerInstance.Msg("ExampleServerMod: Load complete");

            // Example: Load custom data
            // LoadCustomDataFromFile();
        }

        /// <summary>
        /// Called when the server is shutting down.
        /// Use this for cleanup and final saves.
        /// </summary>
        public override void OnServerShutdown()
        {
            API.ModManager.ServerCustomMessageReceived -= HandleCustomMessageReceived;
            API.ModManager.ServerPlayerDisconnected -= HandlePlayerDisconnected;
            API.ModManager.ServerPlayerConnected -= HandlePlayerConnected;

            LoggerInstance.Msg("ExampleServerMod: Shutting down...");

            // Example: Perform cleanup
            // CleanupResources();
        }

        /// <summary>
        /// Handles the event-first player-connected hook.
        /// </summary>
        /// <param name="player">The tracked player that completed the join flow.</param>
        private void HandlePlayerConnected(ConnectedPlayerInfo player)
        {
            LoggerInstance.Msg($"Player connected: {player.DisplayName} ({player.TrustedUniqueId})");

            // Example: Send a personal welcome
            // if (player?.Connection != null)
            // {
            //     Shared.Networking.CustomMessaging.SendToClient(player.Connection, "welcome", "Welcome to my server!");
            // }
        }

        /// <summary>
        /// Handles the event-first player-disconnected hook.
        /// </summary>
        /// <param name="player">The tracked player captured at disconnect time.</param>
        private void HandlePlayerDisconnected(ConnectedPlayerInfo player)
        {
            LoggerInstance.Msg($"Player disconnected: {player.DisplayName} ({player.TrustedUniqueId})");
        }

        /// <summary>
        /// Handles forwarded custom messages from client mods.
        /// </summary>
        /// <param name="messageType">Type of the message.</param>
        /// <param name="data">Message data, typically JSON.</param>
        /// <param name="sender">Tracked sender details when available.</param>
        private void HandleCustomMessageReceived(string messageType, byte[] data, ConnectedPlayerInfo sender)
        {
            LoggerInstance.Msg($"Received custom message from {sender?.TrustedUniqueId ?? "unknown"}: {messageType}");

            // Example: Handle specific message types here.
        }
    }
}
