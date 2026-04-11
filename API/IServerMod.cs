#if SERVER
using DedicatedServerMod.Server.Player;
#endif
using System.ComponentModel;

namespace DedicatedServerMod.API
{
    /// <summary>
    /// Defines lifecycle hooks for mods that participate in the DedicatedServerMod server runtime.
    /// </summary>
    /// <remarks>
    /// Implement this interface in a server-side Melon mod to receive notifications from
    /// <see cref="ModManager"/>. These callbacks provide the compatibility-first server mod surface
    /// and remain available for existing mods that still consume string-based player identifiers.
    /// <para>
    /// For new work, prefer the event-based hooks exposed by <see cref="ModManager"/>, such as
    /// <see cref="ModManager.ServerPlayerConnected"/>,
    /// <see cref="ModManager.ServerPlayerDisconnected"/>, and
    /// <see cref="ModManager.ServerCustomMessageReceived"/>. If you are already deriving from
    /// <see cref="ServerModBase"/> or <see cref="ServerMelonModBase"/>, you can also override the
    /// typed <see cref="ConnectedPlayerInfo"/> overloads there.
    /// </para>
    /// </remarks>
    public interface IServerMod
    {
        /// <summary>
        /// Called when the server is starting up.
        /// </summary>
        void OnServerInitialize();

        /// <summary>
        /// Called when the server is fully started.
        /// </summary>
        void OnServerStarted();

        /// <summary>
        /// Called when the server is shutting down.
        /// </summary>
        void OnServerShutdown();

        /// <summary>
        /// Called when a player completes the dedicated-server join flow.
        /// </summary>
        /// <param name="playerId">
        /// Legacy player identifier for compatibility. This is now the player's trusted unique ID
        /// when available, which normally means the authenticated SteamID64. When trusted identity
        /// is unavailable, the framework falls back to the tracked SteamID and finally the FishNet
        /// client ID.
        /// </param>
        /// <remarks>
        /// New mods should prefer <see cref="ModManager.ServerPlayerConnected"/> or the typed
        /// <see cref="ConnectedPlayerInfo"/> overloads on <see cref="ServerModBase"/> /
        /// <see cref="ServerMelonModBase"/>.
        /// </remarks>
        [Obsolete("Use ModManager.ServerPlayerConnected or override the ConnectedPlayerInfo overload on ServerModBase/ServerMelonModBase.", false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        void OnPlayerConnected(string playerId);

        /// <summary>
        /// Called when a player disconnects from the server.
        /// </summary>
        /// <param name="playerId">
        /// Legacy player identifier for compatibility. This follows the same resolution order as
        /// <see cref="OnPlayerConnected(string)"/>.
        /// </param>
        /// <remarks>
        /// New mods should prefer <see cref="ModManager.ServerPlayerDisconnected"/> or the typed
        /// <see cref="ConnectedPlayerInfo"/> overloads on <see cref="ServerModBase"/> /
        /// <see cref="ServerMelonModBase"/>.
        /// </remarks>
        [Obsolete("Use ModManager.ServerPlayerDisconnected or override the ConnectedPlayerInfo overload on ServerModBase/ServerMelonModBase.", false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        void OnPlayerDisconnected(string playerId);

        /// <summary>
        /// Called before the server saves data.
        /// </summary>
        void OnBeforeSave();

        /// <summary>
        /// Called after the server saves data.
        /// </summary>
        void OnAfterSave();

        /// <summary>
        /// Called before the server loads data.
        /// </summary>
        void OnBeforeLoad();

        /// <summary>
        /// Called after the server loads data.
        /// </summary>
        void OnAfterLoad();

        /// <summary>
        /// Called when the server receives a custom message from a client mod.
        /// </summary>
        /// <param name="messageType">Logical message command or type.</param>
        /// <param name="data">Raw UTF-8 payload bytes for the message.</param>
        /// <param name="senderId">
        /// Legacy sender identifier for compatibility. This uses the same trusted unique ID
        /// resolution as the legacy player lifecycle callbacks.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if this mod handled the message and no further processing is
        /// required; otherwise <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// New mods should prefer <see cref="ModManager.ServerCustomMessageReceived"/> or the typed
        /// <see cref="ConnectedPlayerInfo"/> overloads on <see cref="ServerModBase"/> /
        /// <see cref="ServerMelonModBase"/>.
        /// </remarks>
        [Obsolete("Use ModManager.ServerCustomMessageReceived or override the ConnectedPlayerInfo overload on ServerModBase/ServerMelonModBase.", false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        bool OnCustomMessage(string messageType, byte[] data, string senderId);
    }
}
