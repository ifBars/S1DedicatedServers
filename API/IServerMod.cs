using System;

namespace DedicatedServerMod.API
{
    /// <summary>
    /// Interface for server-side mods
    /// Implement this interface in your MelonMod to receive server-specific callbacks
    /// </summary>
    public interface IServerMod
    {
        /// <summary>
        /// Called when the server is starting up and this mod should initialize
        /// </summary>
        void OnServerInitialize();

        /// <summary>
        /// Called when the server is shutting down and this mod should clean up
        /// </summary>
        void OnServerShutdown();

        /// <summary>
        /// Called when a player connects to the server
        /// </summary>
        /// <param name="playerId">The connecting player's ID</param>
        void OnPlayerConnected(string playerId);

        /// <summary>
        /// Called when a player disconnects from the server
        /// </summary>
        /// <param name="playerId">The disconnecting player's ID</param>
        void OnPlayerDisconnected(string playerId);

        /// <summary>
        /// Called before the server saves data
        /// </summary>
        void OnBeforeSave();

        /// <summary>
        /// Called after the server saves data
        /// </summary>
        void OnAfterSave();

        /// <summary>
        /// Called before the server loads data
        /// </summary>
        void OnBeforeLoad();

        /// <summary>
        /// Called after the server loads data
        /// </summary>
        void OnAfterLoad();

        /// <summary>
        /// Called when the server receives a custom message
        /// Return true if this mod handled the message, false otherwise
        /// </summary>
        /// <param name="messageType">Type of the message</param>
        /// <param name="data">Message data</param>
        /// <param name="senderId">ID of the sender</param>
        /// <returns>True if handled, false otherwise</returns>
        bool OnCustomMessage(string messageType, byte[] data, string senderId);
    }
}
