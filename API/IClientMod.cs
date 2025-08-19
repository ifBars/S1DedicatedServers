using System;

namespace DedicatedServerMod.API
{
    /// <summary>
    /// Interface for client-side mods
    /// Implement this interface in your MelonMod to receive client-specific callbacks
    /// </summary>
    public interface IClientMod
    {
        /// <summary>
        /// Called when the client is starting up and this mod should initialize
        /// </summary>
        void OnClientInitialize();

        /// <summary>
        /// Called when the client is shutting down and this mod should clean up
        /// </summary>
        void OnClientShutdown();

        /// <summary>
        /// Called when the client connects to a server
        /// </summary>
        void OnConnectedToServer();

        /// <summary>
        /// Called when the client disconnects from a server
        /// </summary>
        void OnDisconnectedFromServer();

        /// <summary>
        /// Called when the client receives a custom message from the server
        /// Return true if this mod handled the message, false otherwise
        /// </summary>
        /// <param name="messageType">Type of the message</param>
        /// <param name="data">Message data</param>
        /// <returns>True if handled, false otherwise</returns>
        bool OnCustomMessage(string messageType, byte[] data);

        /// <summary>
        /// Called when the client's UI is being updated
        /// </summary>
        void OnUIUpdate();

        /// <summary>
        /// Called when the client needs to handle a server event
        /// </summary>
        /// <param name="eventType">Type of the event</param>
        /// <param name="eventData">Event data</param>
        void OnServerEvent(string eventType, object eventData);
    }
}
