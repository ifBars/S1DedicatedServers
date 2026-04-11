using DedicatedServerMod.Shared.Configuration;
using System.ComponentModel;

namespace DedicatedServerMod.API
{
    /// <summary>
    /// Main entry point for Schedule One Dedicated Server mod API.
    /// Provides side-aware access to server/client functionality.
    /// </summary>
    public static partial class S1DS
    {
        /// <summary>
        /// Indicates if we're running on a server build
        /// </summary>
        public static bool IsServer
        {
            get
            {
#if SERVER
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Indicates if we're running on a client build
        /// </summary>
        public static bool IsClient
        {
            get
            {
#if CLIENT
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Gets the current typed build configuration for this assembly.
        /// </summary>
        public static S1DSBuildConfiguration BuildConfiguration
        {
            get
            {
#if SERVER && CLIENT
                return S1DSBuildConfiguration.ServerClient;
#elif SERVER
                return S1DSBuildConfiguration.Server;
#elif CLIENT
                return S1DSBuildConfiguration.Client;
#else
                return S1DSBuildConfiguration.Unknown;
#endif
            }
        }

        /// <summary>
        /// Gets the current build configuration as a legacy string value.
        /// </summary>
        /// <remarks>
        /// New code should prefer <see cref="BuildConfiguration"/> so build checks remain typed and
        /// resilient to future API evolution.
        /// </remarks>
        [Obsolete("Use BuildConfiguration instead.", false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static string BuildConfig => BuildConfiguration.ToString();

        /// <summary>
        /// Shared functionality available on both server and client
        /// </summary>
        public static class Shared
        {
            /// <summary>
            /// Gets the runtime <see cref="ServerConfig"/> instance used by the current build.
            /// </summary>
            /// <remarks>
            /// In <c>SERVER</c> builds, this is the authoritative configuration loaded from disk and
            /// persisted through <see cref="ServerConfig.SaveConfig()"/>.
            /// <para>
            /// In <c>CLIENT</c> builds, this is an in-memory configuration instance used by shared
            /// DedicatedServerMod client/runtime systems. It is not the authoritative server config,
            /// is not automatically synchronized from the server, and client-side changes only update
            /// the local in-memory copy.
            /// </para>
            /// <para>
            /// If your client mod needs server-advertised gameplay or session metadata, prefer the
            /// dedicated client managers and data stores exposed through <see cref="S1DS.Client"/>
            /// rather than assuming <see cref="Config"/> mirrors the active server.
            /// </para>
            /// </remarks>
            public static ServerConfig Config => ServerConfig.Instance;

            /// <summary>
            /// Gets a value indicating whether the runtime configuration object is available.
            /// </summary>
            public static bool IsConfigLoaded => Config != null;
        }
    }
}
