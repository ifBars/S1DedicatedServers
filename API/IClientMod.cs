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
        /// Called when the local client player has spawned and client-side systems
        /// (including custom messaging) are initialized and ready for interaction.
        /// Use this instead of OnConnectedToServer when you need messaging/UI.
        /// </summary>
        void OnClientPlayerReady();

        /// <summary>
        /// Called when the client receives a custom message from the server
        /// Return true if this mod handled the message, false otherwise
        /// </summary>
        /// <param name="messageType">Type of the message</param>
        /// <param name="data">Message data</param>
        /// <returns>True if handled, false otherwise</returns>
        bool OnCustomMessage(string messageType, byte[] data);
    }
}
