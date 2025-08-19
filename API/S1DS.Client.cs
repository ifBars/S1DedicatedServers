#if CLIENT
using DedicatedServerMod.Client;

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
            public static Core ClientCore => DedicatedServerMod.Client.Core.Instance;

            /// <summary>
            /// Access to client connection management
            /// </summary>
            public static ClientConnectionManager Connection => DedicatedServerMod.Client.Core.ConnectionManager;

            /// <summary>
            /// Access to client UI management
            /// </summary>
            public static ClientUIManager UI => DedicatedServerMod.Client.Core.Instance?.UIManager;

            /// <summary>
            /// Access to client player setup
            /// </summary>
            public static ClientPlayerSetup PlayerSetup => DedicatedServerMod.Client.Core.Instance?.PlayerSetupManager;

            /// <summary>
            /// Access to client console management
            /// </summary>
            public static ClientConsoleManager Console => DedicatedServerMod.Client.Core.Instance?.ConsoleManager;

            /// <summary>
            /// Access to client quest management
            /// </summary>
            public static ClientQuestManager Quests => DedicatedServerMod.Client.Core.Instance?.QuestManager;

            /// <summary>
            /// Access to client loopback handler
            /// </summary>
            public static ClientLoopbackHandler Loopback => DedicatedServerMod.Client.Core.Instance?.LoopbackManager;

            /// <summary>
            /// Access to client transport patcher
            /// </summary>
            public static ClientTransportPatcher Transport => DedicatedServerMod.Client.Core.Instance?.TransportPatcher;

            /// <summary>
            /// Checks if the client is currently connected to a server
            /// </summary>
            public static bool IsConnected => Connection?.IsConnectedToDedicatedServer ?? false;

            /// <summary>
            /// Checks if the client core is initialized
            /// </summary>
            public static bool IsInitialized => DedicatedServerMod.Client.Core.Instance != null;
        }
    }
}
#endif
