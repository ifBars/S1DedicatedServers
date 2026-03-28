namespace DedicatedServerMod.API
{
    /// <summary>
    /// Defines lifecycle hooks for mods that participate in the DedicatedServerMod client runtime.
    /// </summary>
    /// <remarks>
    /// Implement this interface in a client-side Melon mod to receive notifications from
    /// <see cref="ModManager"/>. These callbacks are intended for mods that integrate with the
    /// dedicated-server connection flow, client messaging, or client-only manager systems exposed
    /// through <see cref="S1DS.Client"/>.
    /// <para>
    /// If you only need a subset of these callbacks, inherit from <see cref="ClientModBase"/> or
    /// <see cref="ClientMelonModBase"/> instead of implementing every member manually.
    /// </para>
    /// </remarks>
    public interface IClientMod
    {
        /// <summary>
        /// Called after the DedicatedServerMod client bootstrap initializes its core client systems.
        /// </summary>
        /// <remarks>
        /// This is the first client-specific callback most mods should use. By this point,
        /// <see cref="S1DS.Client.IsInitialized"/> is expected to be <see langword="true"/>, and
        /// manager properties such as <see cref="S1DS.Client.Connection"/> are typically available.
        /// World or player-dependent systems may still be mid-startup, so use
        /// <see cref="OnClientPlayerReady"/> for player-facing initialization.
        /// </remarks>
        void OnClientInitialize();

        /// <summary>
        /// Called when the client runtime is shutting down and the mod should release client-side resources.
        /// </summary>
        void OnClientShutdown();

        /// <summary>
        /// Called when the client establishes a dedicated-server connection.
        /// </summary>
        /// <remarks>
        /// Use this for connection-state reactions such as resetting transient state, enabling
        /// connection-specific features, or logging. If your mod needs the local player, custom
        /// messaging endpoint readiness, or UI interactions, prefer <see cref="OnClientPlayerReady"/>.
        /// </remarks>
        void OnConnectedToServer();

        /// <summary>
        /// Called when the client disconnects from the dedicated server.
        /// </summary>
        void OnDisconnectedFromServer();

        /// <summary>
        /// Called when the local client player has spawned and client-side systems
        /// (including custom messaging) are initialized and ready for interaction.
        /// Use this instead of <see cref="OnConnectedToServer"/> when you need messaging or UI access.
        /// </summary>
        /// <remarks>
        /// This is the safest point to read server-provided client data, send custom messages, or
        /// initialize UI that depends on the dedicated-server session being fully ready.
        /// </remarks>
        void OnClientPlayerReady();

        /// <summary>
        /// Called when the client receives a custom message from the server.
        /// </summary>
        /// <param name="messageType">Type of the message</param>
        /// <param name="data">Raw message payload provided by the server.</param>
        /// <returns>
        /// <see langword="true"/> if this mod handled the message and no additional processing is
        /// required; otherwise, <see langword="false"/>.
        /// </returns>
        bool OnCustomMessage(string messageType, byte[] data);
    }
}
