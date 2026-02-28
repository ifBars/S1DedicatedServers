#if CLIENT
using DedicatedServerMod.Client.Core;
using DedicatedServerMod.Client.Managers;
using DedicatedServerMod.Client.Patchers;
using MelonLoader;

namespace DedicatedServerMod.API
{
    /// <summary>
    /// Client-side partial class providing typed access to client managers
    /// Only compiled when CLIENT preprocessor directive is defined
    /// </summary>
    public static partial class S1DS
    {
        /// <summary>
        /// Client-side API access. Only available in client builds.
        /// </summary>
        public static class Client
        {
            /// <summary>
            /// Access to client core functionality
            /// </summary>
            public static ClientBootstrap ClientCore => ClientBootstrap.Instance;

            /// <summary>
            /// Access to client connection management
            /// </summary>
            public static ClientConnectionManager Connection => ClientBootstrap.Instance?.ConnectionManager;

            /// <summary>
            /// Access to client UI management
            /// </summary>
            public static ClientUIManager UI => ClientBootstrap.Instance?.UIManager;

            /// <summary>
            /// Access to client console management
            /// </summary>
            public static ClientConsoleManager Console => ClientBootstrap.Instance?.ConsoleManager;

            /// <summary>
            /// Access to client quest management
            /// </summary>
            public static ClientQuestManager Quests => ClientBootstrap.Instance?.QuestManager;

            /// <summary>
            /// Access to client loopback handler
            /// </summary>
            public static ClientLoopbackHandler Loopback => ClientBootstrap.Instance?.LoopbackHandler;

            /// <summary>
            /// Access to client transport patcher
            /// </summary>
            public static ClientTransportPatcher Transport => ClientBootstrap.Instance?.TransportPatcher;

            /// <summary>
            /// Checks if the client is currently connected to a server
            /// </summary>
            public static bool IsConnected => Connection?.IsConnectedToDedicatedServer ?? false;

            /// <summary>
            /// Checks if the client core is initialized
            /// </summary>
            public static bool IsInitialized => ClientBootstrap.Instance != null;
        }
    }
}
#endif
