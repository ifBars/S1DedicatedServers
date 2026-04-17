using DedicatedServerMod.Utils;
using MelonLoader;
using DedicatedServerMod.API.Metadata;

namespace DedicatedServerMod.API
{
    /// <summary>
    /// Manages registration and lifecycle of server and client mods.
    /// </summary>
    public static partial class ModManager
    {
        private static readonly List<IServerMod> _serverMods = new List<IServerMod>();
        private static readonly List<IClientMod> _clientMods = new List<IClientMod>();
        private static bool _initialized;

#if CLIENT
        private static bool _clientMsgWired;
#endif

#if SERVER
        private static bool _serverMsgWired;
#endif

        /// <summary>
        /// Initializes the mod manager and discovers mods.
        /// </summary>
        internal static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            DebugLog.StartupDebug("Initializing ModManager...");
            DiscoverMods();
            _initialized = true;
            DebugLog.StartupDebug($"ModManager initialized. Found {_serverMods.Count} server mods and {_clientMods.Count} client mods.");
        }

        /// <summary>
        /// Registers a server mod manually.
        /// </summary>
        /// <param name="mod">The server mod to register.</param>
        public static void RegisterServerMod(IServerMod mod)
        {
            if (mod == null || _serverMods.Contains(mod))
            {
                return;
            }

            _serverMods.Add(mod);
            MelonLogger.Msg($"Registered server mod: {mod.GetType().Name}");

#if SERVER
            if (S1DS.IsServer && S1DS.Server.IsRunning)
            {
                try
                {
                    mod.OnServerInitialize();
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error initializing server mod {mod.GetType().Name}: {ex.Message}");
                }
            }
#endif
        }

        /// <summary>
        /// Registers a client mod manually.
        /// </summary>
        /// <param name="mod">The client mod to register.</param>
        public static void RegisterClientMod(IClientMod mod)
        {
            if (mod == null || _clientMods.Contains(mod))
            {
                return;
            }

            _clientMods.Add(mod);
            MelonLogger.Msg($"Registered client mod: {mod.GetType().Name}");

#if CLIENT
            try
            {
                WireClientMessageForwarding();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error wiring client message forwarding during registration: {ex.Message}");
            }

            if (S1DS.IsClient && S1DS.Client.IsInitialized)
            {
                try
                {
                    mod.OnClientInitialize();
                    MelonLogger.Msg($"Initialized client mod immediately: {mod.GetType().Name}");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error initializing client mod {mod.GetType().Name}: {ex.Message}");
                }
            }
#endif
        }

        /// <summary>
        /// Gets all registered server mods.
        /// </summary>
        public static IReadOnlyList<IServerMod> ServerMods => _serverMods.AsReadOnly();

        /// <summary>
        /// Gets all registered client mods.
        /// </summary>
        public static IReadOnlyList<IClientMod> ClientMods => _clientMods.AsReadOnly();

        /// <summary>
        /// Gets descriptive metadata for the locally loaded client mods.
        /// </summary>
        /// <remarks>
        /// This list is intended for addon discovery and diagnostics. It mirrors the same local
        /// Melon-discovered client mods used by the client mod verification handshake and excludes
        /// the core DedicatedServerMod client runtime assembly.
        /// <para>
        /// Use <see cref="ClientMods"/> when you need the registered <see cref="IClientMod"/>
        /// lifecycle instances. Use <see cref="ClientModMetadata"/> when you need stable metadata
        /// like display name, version, author, assembly name, or declared mod ID.
        /// </para>
        /// </remarks>
        public static IReadOnlyList<ClientModMetadata> ClientModMetadata
        {
            get
            {
                if (!S1DS.IsClient)
                {
                    return Array.Empty<ClientModMetadata>();
                }

                return GetLoadedClientModMetadata(typeof(ModManager).Assembly);
            }
        }

        private static void DiscoverMods()
        {
            try
            {
                IReadOnlyList<MelonBase> melonMods = MelonMod.RegisteredMelons;
                if (melonMods == null)
                {
                    return;
                }

                foreach (MelonBase melonMod in melonMods)
                {
                    if (melonMod == null)
                    {
                        continue;
                    }

                    if (melonMod is IServerMod serverMod && S1DS.IsServer)
                    {
                        RegisterServerMod(serverMod);
                    }

                    if (melonMod is IClientMod clientMod && S1DS.IsClient)
                    {
                        RegisterClientMod(clientMod);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error discovering mods: {ex.Message}");
            }
        }

        private static void InvokeEventSafely(Action handler, string eventName)
        {
            if (handler == null)
            {
                return;
            }

            foreach (Action subscriber in handler.GetInvocationList())
            {
                try
                {
                    subscriber();
                }
                catch (Exception ex)
                {
                    DebugLog.Error($"Error in ModManager event {eventName}: {ex.Message}", ex);
                }
            }
        }

        private static void InvokeEventSafely<T>(Action<T> handler, string eventName, T arg)
        {
            if (handler == null)
            {
                return;
            }

            foreach (Action<T> subscriber in handler.GetInvocationList())
            {
                try
                {
                    subscriber(arg);
                }
                catch (Exception ex)
                {
                    DebugLog.Error($"Error in ModManager event {eventName}: {ex.Message}", ex);
                }
            }
        }

        private static void InvokeEventSafely<T1, T2>(Action<T1, T2> handler, string eventName, T1 arg1, T2 arg2)
        {
            if (handler == null)
            {
                return;
            }

            foreach (Action<T1, T2> subscriber in handler.GetInvocationList())
            {
                try
                {
                    subscriber(arg1, arg2);
                }
                catch (Exception ex)
                {
                    DebugLog.Error($"Error in ModManager event {eventName}: {ex.Message}", ex);
                }
            }
        }

        private static void InvokeEventSafely<T1, T2, T3>(Action<T1, T2, T3> handler, string eventName, T1 arg1, T2 arg2, T3 arg3)
        {
            if (handler == null)
            {
                return;
            }

            foreach (Action<T1, T2, T3> subscriber in handler.GetInvocationList())
            {
                try
                {
                    subscriber(arg1, arg2, arg3);
                }
                catch (Exception ex)
                {
                    DebugLog.Error($"Error in ModManager event {eventName}: {ex.Message}", ex);
                }
            }
        }
    }
}
