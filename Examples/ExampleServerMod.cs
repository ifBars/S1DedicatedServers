using System;
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
    public class ExampleServerMod : API.ServerModBase
    {
        /// <summary>
        /// Called when the server is starting up.
        /// Use this for initialization that should happen before players connect.
        /// </summary>
        protected override void OnServerInitialize()
        {
            LoggerInstance.Msg("ExampleServerMod initializing...");

            // Example: Register custom commands
            // CommandManager.Instance.RegisterCommand(new MyCustomCommand());

            // Example: Subscribe to player events
            // API.ModManager.NotifyPlayerConnected += OnPlayerConnected;

            LoggerInstance.Msg("ExampleServerMod initialized successfully");
        }

        /// <summary>
        /// Called when the server is fully started and ready to accept connections.
        /// </summary>
        protected override void OnServerStarted()
        {
            LoggerInstance.Msg("ExampleServerMod: Server is now running!");

            // Example: Broadcast a welcome message
            // S1DS.Server.Messaging.BroadcastToClients("welcome", "Welcome to my modded server!");
        }

        /// <summary>
        /// Called when a player connects to the server.
        /// </summary>
        /// <param name="playerId">The connecting player's ID (typically Steam ID)</param>
        protected override void OnPlayerConnected(string playerId)
        {
            LoggerInstance.Msg($"Player connected: {playerId}");

            // Example: Send a personal welcome
            // var player = Permissions.PlayerResolver.GetPlayerBySteamId(playerId);
            // if (player != null)
            // {
            //     S1DS.Server.Messaging.SendToClient(player.Owner, "welcome", "Welcome to my server!");
            // }
        }

        /// <summary>
        /// Called when a player disconnects from the server.
        /// </summary>
        /// <param name="playerId">The disconnecting player's ID</param>
        protected override void OnPlayerDisconnected(string playerId)
        {
            LoggerInstance.Msg($"Player disconnected: {playerId}");
        }

        /// <summary>
        /// Called before the server saves data.
        /// Use this to save your mod's custom data.
        /// </summary>
        protected override void OnBeforeSave()
        {
            LoggerInstance.Msg("ExampleServerMod: Saving custom data...");

            // Example: Save custom data to a file or to the game's save system
            // SaveCustomDataToFile();
        }

        /// <summary>
        /// Called after the server saves data.
        /// </summary>
        protected override void OnAfterSave()
        {
            LoggerInstance.Msg("ExampleServerMod: Save complete");
        }

        /// <summary>
        /// Called before the server loads data.
        /// </summary>
        protected override void OnBeforeLoad()
        {
            LoggerInstance.Msg("ExampleServerMod: Preparing to load data...");
        }

        /// <summary>
        /// Called after the server loads data.
        /// Use this to load your mod's custom data.
        /// </summary>
        protected override void OnAfterLoad()
        {
            LoggerInstance.Msg("ExampleServerMod: Load complete");

            // Example: Load custom data
            // LoadCustomDataFromFile();
        }

        /// <summary>
        /// Called when the server is shutting down.
        /// Use this for cleanup and final saves.
        /// </summary>
        protected override void OnServerShutdown()
        {
            LoggerInstance.Msg("ExampleServerMod: Shutting down...");

            // Example: Perform cleanup
            // CleanupResources();
        }

        /// <summary>
        /// Called when the server receives a custom message from a client mod.
        /// Return true if this mod handled the message, false otherwise.
        /// </summary>
        /// <param name="messageType">Type of the message</param>
        /// <param name="data">Message data (typically JSON)</param>
        /// <param name="senderId">ID of the sender</param>
        /// <returns>True if handled, false to pass to other mods</returns>
        protected override bool OnCustomMessage(string messageType, byte[] data, string senderId)
        {
            LoggerInstance.Msg($"Received custom message from {senderId}: {messageType}");

            // Example: Handle specific message types
            // if (messageType == "player_action")
            // {
            //     var actionData = System.Text.Encoding.UTF8.GetString(data);
            //     ProcessPlayerAction(senderId, actionData);
            //     return true;
            // }

            return false; // Not handled by this mod
        }
    }
}
