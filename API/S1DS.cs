using System;
using MelonLoader;
using DedicatedServerMod.Shared;
using DedicatedServerMod.Shared.Configuration;

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
        /// Gets the current build configuration
        /// </summary>
        public static string BuildConfig
        {
            get
            {
#if SERVER && CLIENT
                return "ServerClient";
#elif SERVER
                return "Server";
#elif CLIENT
                return "Client";
#else
                return "Unknown";
#endif
            }
        }

        /// <summary>
        /// Shared functionality available on both server and client
        /// </summary>
        public static class Shared
        {
            /// <summary>
            /// Access to server configuration
            /// </summary>
            public static DedicatedServerMod.Shared.Configuration.ServerConfig Config => DedicatedServerMod.Shared.Configuration.ServerConfig.Instance;

            /// <summary>
            /// Checks if server config is loaded
            /// </summary>
            public static bool IsConfigLoaded => Config != null;

        }
    }
}
