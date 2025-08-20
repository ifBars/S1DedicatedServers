#if SERVER
using DedicatedServerMod.Server.Core;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Server.Network;
using DedicatedServerMod.Server.Game;
using DedicatedServerMod.Server.Persistence;

namespace DedicatedServerMod.API
{
    /// <summary>
    /// Server-side partial class providing typed access to server managers
    /// Only compiled when SERVER preprocessor directive is defined
    /// </summary>
    public static partial class S1DS
    {
        /// <summary>
        /// Server-side API access. Only available in server builds.
        /// </summary>
        public static class Server
        {
            /// <summary>
            /// Access to player management functionality
            /// </summary>
            public static PlayerManager Players => ServerBootstrap.Players;

            /// <summary>
            /// Access to network management functionality
            /// </summary>
            public static NetworkManager Network => ServerBootstrap.Network;

            /// <summary>
            /// Access to game system management
            /// </summary>
            public static GameSystemManager GameSystems => ServerBootstrap.GameSystems;

            /// <summary>
            /// Access to persistence functionality
            /// </summary>
            public static PersistenceManager Persistence => ServerBootstrap.Persistence;

            /// <summary>
            /// Checks if the server is currently running and initialized
            /// </summary>
            public static bool IsRunning => ServerBootstrap.IsInitialized;

            /// <summary>
            /// Gets the number of connected players
            /// </summary>
            public static int PlayerCount => Players?.ConnectedPlayerCount ?? 0;
        }
    }
}
#endif
