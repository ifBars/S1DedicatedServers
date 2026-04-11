#if CLIENT
using DedicatedServerMod.Client.Core;
using DedicatedServerMod.Client.Managers;
using DedicatedServerMod.Client.Patchers;
using MelonLoader;

namespace DedicatedServerMod.API
{
    /// <summary>
    /// Client-side partial class providing typed access to client systems.
    /// Compiled only when the <c>CLIENT</c> preprocessor directive is defined.
    /// </summary>
    public static partial class S1DS
    {
        /// <summary>
        /// Primary facade for DedicatedServerMod client functionality.
        /// </summary>
        /// <remarks>
        /// Client mods should normally access dedicated-server features through this facade
        /// rather than by depending directly on <see cref="ClientBootstrap"/>. The bootstrap
        /// creates and owns the managers exposed here, while <see cref="IClientMod"/> lifecycle
        /// callbacks tell you when those managers are ready to use.
        /// <para>
        /// Use <see cref="IsInitialized"/> to determine whether client systems have been created.
        /// Use <see cref="ModManager.ClientPlayerReady"/> or
        /// <see cref="IClientMod.OnClientPlayerReady"/> when your mod needs the local player,
        /// custom messaging, or UI interactions to be fully ready.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// public sealed class MyClientMod : ClientMelonModBase
        /// {
        ///     public override void OnClientInitialize()
        ///     {
        ///         ModManager.ClientPlayerReady += OnClientReady;
        ///     }
        ///
        ///     public override void OnClientShutdown()
        ///     {
        ///         ModManager.ClientPlayerReady -= OnClientReady;
        ///     }
        ///
        ///     private void OnClientReady()
        ///     {
        ///         var connection = S1DS.Client.Connection;
        ///         var ui = S1DS.Client.UI;
        ///     }
        /// }
        /// </code>
        /// </example>
        public static class Client
        {
            /// <summary>
            /// Gets the underlying client bootstrap created by MelonLoader for the client build.
            /// </summary>
            /// <remarks>
            /// This is the lowest-level client entry point exposed by the API. Most mods should
            /// prefer the more specific properties on <see cref="Client"/>, such as
            /// <see cref="Connection"/> or <see cref="UI"/>, unless they specifically need
            /// bootstrap-owned state.
            /// </remarks>
            public static ClientBootstrap ClientCore => ClientBootstrap.Instance;

            /// <summary>
            /// Gets the client connection manager responsible for dedicated-server connection state.
            /// </summary>
            /// <remarks>
            /// Available after <see cref="IClientMod.OnClientInitialize"/>. This manager is the
            /// usual entry point for connection status and dedicated-server connect or disconnect
            /// operations.
            /// </remarks>
            public static ClientConnectionManager Connection => ClientBootstrap.Instance?.ConnectionManager;

            /// <summary>
            /// Gets the client UI manager used by the dedicated-server client experience.
            /// </summary>
            /// <remarks>
            /// Prefer using this from <see cref="ModManager.ClientPlayerReady"/> or
            /// <see cref="IClientMod.OnClientPlayerReady"/> when your mod depends on scene objects,
            /// player presence, or server-driven UI state.
            /// </remarks>
            public static ClientUIManager UI => ClientBootstrap.Instance?.UIManager;

            /// <summary>
            /// Gets the client console manager used for dedicated-server admin console features.
            /// </summary>
            public static ClientConsoleManager Console => ClientBootstrap.Instance?.ConsoleManager;

            /// <summary>
            /// Gets the client-side Steam avatar helper used to resolve player profile images.
            /// </summary>
            /// <remarks>
            /// This helper is available only in client builds. It provides Steam-backed avatar
            /// lookup and caching by SteamID64 without requiring mods to register Steam callbacks
            /// directly.
            /// </remarks>
            public static DedicatedServerMod.API.Client.ClientSteamAvatarService Avatars =>
                DedicatedServerMod.API.Client.ClientSteamAvatarService.Instance;

            /// <summary>
            /// Gets the client quest manager used by dedicated-server quest integration.
            /// </summary>
            public static ClientQuestManager Quests => ClientBootstrap.Instance?.QuestManager;

            /// <summary>
            /// Gets a value indicating whether the client is currently connected to a dedicated server.
            /// </summary>
            /// <remarks>
            /// This reports the dedicated-server connection state tracked by
            /// <see cref="Connection"/>. It does not imply that the player-facing systems are fully
            /// ready; use <see cref="ModManager.ClientPlayerReady"/> or
            /// <see cref="IClientMod.OnClientPlayerReady"/> for that point in the lifecycle.
            /// </remarks>
            public static bool IsConnected => Connection?.IsConnectedToDedicatedServer ?? false;

            /// <summary>
            /// Gets a value indicating whether the DedicatedServerMod client bootstrap has initialized.
            /// </summary>
            /// <remarks>
            /// When this returns <see langword="true"/>, <see cref="ClientBootstrap"/> has finished
            /// creating the client managers exposed through this facade. This is normally true by
            /// the time <see cref="IClientMod.OnClientInitialize"/> is called.
            /// </remarks>
            public static bool IsInitialized => ClientBootstrap.Instance?.IsApiModsReady ?? false;
        }
    }
}
#endif
