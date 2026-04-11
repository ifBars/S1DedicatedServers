using MelonLoader;
using System.ComponentModel;
#if SERVER
using DedicatedServerMod.Server.Player;
#endif

namespace DedicatedServerMod.API
{
    /// <summary>
    /// Convenience base for server-side mods. Provides no-op virtual implementations so inheritors
    /// override only the callbacks they care about.
    /// </summary>
    /// <remarks>
    /// This base class implements the legacy <see cref="IServerMod"/> lifecycle contract and also
    /// exposes typed <see cref="ConnectedPlayerInfo"/> overloads for player and message events.
    /// New mod code should generally prefer <see cref="ModManager"/> events for optional hooks and
    /// use these overloads when a single base-class override is the most natural fit.
    /// </remarks>
    public abstract class ServerModBase : IServerMod
    {
        /// <inheritdoc />
        public virtual void OnServerInitialize() { }

        /// <inheritdoc />
        public virtual void OnServerStarted() { }

        /// <inheritdoc />
        public virtual void OnServerShutdown() { }

        /// <inheritdoc />
        [Obsolete("Use ModManager.ServerPlayerConnected or override OnPlayerConnected(ConnectedPlayerInfo).", false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual void OnPlayerConnected(string playerId) { }

        /// <inheritdoc />
        [Obsolete("Use ModManager.ServerPlayerDisconnected or override OnPlayerDisconnected(ConnectedPlayerInfo).", false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual void OnPlayerDisconnected(string playerId) { }

        /// <inheritdoc />
        public virtual void OnBeforeSave() { }

        /// <inheritdoc />
        public virtual void OnAfterSave() { }

        /// <inheritdoc />
        public virtual void OnBeforeLoad() { }

        /// <inheritdoc />
        public virtual void OnAfterLoad() { }

        /// <inheritdoc />
        [Obsolete("Use ModManager.ServerCustomMessageReceived or override OnCustomMessage(string, byte[], ConnectedPlayerInfo).", false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual bool OnCustomMessage(string messageType, byte[] data, string senderId) { return false; }

#if SERVER
        /// <summary>
        /// Called when a tracked player completes the dedicated-server join flow.
        /// </summary>
        /// <param name="player">The tracked player that completed the join flow.</param>
        /// <remarks>
        /// The default implementation routes back to <see cref="OnPlayerConnected(string)"/> using
        /// the player's trusted unique ID, tracked unique ID, or FishNet client ID.
        /// </remarks>
        public virtual void OnPlayerConnected(ConnectedPlayerInfo player)
        {
#pragma warning disable CS0618
            OnPlayerConnected(player?.TrustedUniqueId ?? player?.UniqueId ?? string.Empty);
#pragma warning restore CS0618
        }

        /// <summary>
        /// Called when a tracked player disconnects from the dedicated server.
        /// </summary>
        /// <param name="player">The tracked player captured at disconnect time.</param>
        /// <remarks>
        /// The default implementation routes back to <see cref="OnPlayerDisconnected(string)"/>
        /// using the player's trusted unique ID, tracked unique ID, or FishNet client ID.
        /// </remarks>
        public virtual void OnPlayerDisconnected(ConnectedPlayerInfo player)
        {
#pragma warning disable CS0618
            OnPlayerDisconnected(player?.TrustedUniqueId ?? player?.UniqueId ?? string.Empty);
#pragma warning restore CS0618
        }

        /// <summary>
        /// Called when a forwarded custom message is received from a client mod.
        /// </summary>
        /// <param name="messageType">Logical message command or type.</param>
        /// <param name="data">Raw UTF-8 payload bytes for the message.</param>
        /// <param name="sender">Tracked sender details when available.</param>
        /// <returns>
        /// <see langword="true"/> if the message was handled; otherwise
        /// <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// The default implementation routes back to
        /// <see cref="OnCustomMessage(string, byte[], string)"/> using the sender's trusted unique
        /// ID, tracked unique ID, or FishNet client ID.
        /// </remarks>
        public virtual bool OnCustomMessage(string messageType, byte[] data, ConnectedPlayerInfo sender)
        {
#pragma warning disable CS0618
            return OnCustomMessage(messageType, data, sender?.TrustedUniqueId ?? sender?.UniqueId ?? string.Empty);
#pragma warning restore CS0618
        }
#endif
    }

    /// <summary>
    /// Convenience base which also derives from <see cref="MelonMod"/> for auto-discovery.
    /// </summary>
    /// <remarks>
    /// If a mod derives from this class, <see cref="ModManager"/> discovers it automatically and
    /// delivers the standard server lifecycle callbacks. Use <see cref="ModManager"/> events for
    /// optional lifecycle hooks, or override the typed <see cref="ConnectedPlayerInfo"/> overloads
    /// here when that keeps the mod simpler.
    /// </remarks>
    public abstract class ServerMelonModBase : MelonMod, IServerMod
    {
        /// <inheritdoc />
        public virtual void OnServerInitialize() { }

        /// <inheritdoc />
        public virtual void OnServerStarted() { }

        /// <inheritdoc />
        public virtual void OnServerShutdown() { }

        /// <inheritdoc />
        [Obsolete("Use ModManager.ServerPlayerConnected or override OnPlayerConnected(ConnectedPlayerInfo).", false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual void OnPlayerConnected(string playerId) { }

        /// <inheritdoc />
        [Obsolete("Use ModManager.ServerPlayerDisconnected or override OnPlayerDisconnected(ConnectedPlayerInfo).", false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual void OnPlayerDisconnected(string playerId) { }

        /// <inheritdoc />
        public virtual void OnBeforeSave() { }

        /// <inheritdoc />
        public virtual void OnAfterSave() { }

        /// <inheritdoc />
        public virtual void OnBeforeLoad() { }

        /// <inheritdoc />
        public virtual void OnAfterLoad() { }

        /// <inheritdoc />
        [Obsolete("Use ModManager.ServerCustomMessageReceived or override OnCustomMessage(string, byte[], ConnectedPlayerInfo).", false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual bool OnCustomMessage(string messageType, byte[] data, string senderId) { return false; }

#if SERVER
        /// <summary>
        /// Called when a tracked player completes the dedicated-server join flow.
        /// </summary>
        /// <param name="player">The tracked player that completed the join flow.</param>
        /// <remarks>
        /// The default implementation routes back to <see cref="OnPlayerConnected(string)"/> using
        /// the player's trusted unique ID, tracked unique ID, or FishNet client ID.
        /// </remarks>
        public virtual void OnPlayerConnected(ConnectedPlayerInfo player)
        {
#pragma warning disable CS0618
            OnPlayerConnected(player?.TrustedUniqueId ?? player?.UniqueId ?? string.Empty);
#pragma warning restore CS0618
        }

        /// <summary>
        /// Called when a tracked player disconnects from the dedicated server.
        /// </summary>
        /// <param name="player">The tracked player captured at disconnect time.</param>
        /// <remarks>
        /// The default implementation routes back to <see cref="OnPlayerDisconnected(string)"/>
        /// using the player's trusted unique ID, tracked unique ID, or FishNet client ID.
        /// </remarks>
        public virtual void OnPlayerDisconnected(ConnectedPlayerInfo player)
        {
#pragma warning disable CS0618
            OnPlayerDisconnected(player?.TrustedUniqueId ?? player?.UniqueId ?? string.Empty);
#pragma warning restore CS0618
        }

        /// <summary>
        /// Called when a forwarded custom message is received from a client mod.
        /// </summary>
        /// <param name="messageType">Logical message command or type.</param>
        /// <param name="data">Raw UTF-8 payload bytes for the message.</param>
        /// <param name="sender">Tracked sender details when available.</param>
        /// <returns>
        /// <see langword="true"/> if the message was handled; otherwise
        /// <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// The default implementation routes back to
        /// <see cref="OnCustomMessage(string, byte[], string)"/> using the sender's trusted unique
        /// ID, tracked unique ID, or FishNet client ID.
        /// </remarks>
        public virtual bool OnCustomMessage(string messageType, byte[] data, ConnectedPlayerInfo sender)
        {
#pragma warning disable CS0618
            return OnCustomMessage(messageType, data, sender?.TrustedUniqueId ?? sender?.UniqueId ?? string.Empty);
#pragma warning restore CS0618
        }
#endif
    }
}
