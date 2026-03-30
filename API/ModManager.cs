using System;
using System.Collections.Generic;
using System.Linq;
using DedicatedServerMod.Shared.Networking;
using DedicatedServerMod.Shared.ModVerification;
using DedicatedServerMod.Utils;
using MelonLoader;

namespace DedicatedServerMod.API
{
    /// <summary>
    /// Manages registration and lifecycle of server/client mods
    /// </summary>
    public static class ModManager
    {
        private static readonly List<IServerMod> _serverMods = new List<IServerMod>();
        private static readonly List<IClientMod> _clientMods = new List<IClientMod>();
        private static bool _initialized = false;

        /// <summary>
        /// Initializes the mod manager and discovers mods
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            DebugLog.StartupDebug("Initializing ModManager...");

            // Discover and register mods from loaded MelonMods
            DiscoverMods();

            _initialized = true;
            DebugLog.StartupDebug($"ModManager initialized. Found {_serverMods.Count} server mods and {_clientMods.Count} client mods.");
        }

        /// <summary>
        /// Registers a server mod manually
        /// </summary>
        /// <param name="mod">The server mod to register</param>
        public static void RegisterServerMod(IServerMod mod)
        {
            if (mod == null) return;

            if (!_serverMods.Contains(mod))
            {
                _serverMods.Add(mod);
                MelonLogger.Msg($"Registered server mod: {mod.GetType().Name}");

                // If we're already running, initialize immediately (server builds only)
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
        }

        /// <summary>
        /// Registers a client mod manually
        /// </summary>
        /// <param name="mod">The client mod to register</param>
        public static void RegisterClientMod(IClientMod mod)
        {
            if (mod == null) return;

            if (!_clientMods.Contains(mod))
            {
                _clientMods.Add(mod);
                MelonLogger.Msg($"Registered client mod: {mod.GetType().Name}");

                // If we're already running, initialize immediately (client builds only)
                #if CLIENT
                try
                {
                    // Ensure message forwarding is wired so mods can receive OnCustomMessage
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
        }

        /// <summary>
        /// Discovers mods from loaded MelonMods
        /// </summary>
        private static void DiscoverMods()
        {
            try
            {
                var melonMods = MelonMod.RegisteredMelons;
                if (melonMods == null) return;

                foreach (var melonMod in melonMods)
                {
                    if (melonMod == null) continue;

                    // Check if this MelonMod implements our interfaces
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

        #region Server Events
        #if SERVER

        /// <summary>
        /// Notifies all server mods that the server is initializing
        /// </summary>
        public static void NotifyServerInitialize()
        {
            if (!S1DS.IsServer) return;

            MelonLogger.Msg("Notifying server mods of initialization...");
            foreach (var mod in _serverMods.ToList())
            {
                try
                {
                    mod.OnServerInitialize();
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error in server mod {mod.GetType().Name}.OnServerInitialize(): {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Notifies all server mods that the server is started
        /// </summary>
        public static void NotifyServerStarted()
        {
            if (!S1DS.IsServer) return;

            MelonLogger.Msg("Notifying server mods of startup...");
            foreach (var mod in _serverMods.ToList())
            {
                try
                {
                    mod.OnServerStarted();
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error in server mod {mod.GetType().Name}.OnServerStarted(): {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Notifies all server mods that the server is shutting down
        /// </summary>
        public static void NotifyServerShutdown()
        {
            if (!S1DS.IsServer) return;

            MelonLogger.Msg("Notifying server mods of shutdown...");
            foreach (var mod in _serverMods.ToList())
            {
                try
                {
                    mod.OnServerShutdown();
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error in server mod {mod.GetType().Name}.OnServerShutdown(): {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Notifies all server mods that a player connected
        /// </summary>
        /// <param name="playerId">The connecting player's ID</param>
        public static void NotifyPlayerConnected(string playerId)
        {
            if (!S1DS.IsServer) return;

            foreach (var mod in _serverMods.ToList())
            {
                try
                {
                    mod.OnPlayerConnected(playerId);
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error in server mod {mod.GetType().Name}.OnPlayerConnected(): {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Notifies all server mods that a player disconnected
        /// </summary>
        /// <param name="playerId">The disconnecting player's ID</param>
        public static void NotifyPlayerDisconnected(string playerId)
        {
            if (!S1DS.IsServer) return;

            foreach (var mod in _serverMods.ToList())
            {
                try
                {
                    mod.OnPlayerDisconnected(playerId);
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error in server mod {mod.GetType().Name}.OnPlayerDisconnected(): {ex.Message}");
                }
            }
        }

        internal static List<DeclaredClientCompanionRequirement> GetDeclaredServerCompanions()
        {
            List<DeclaredClientCompanionRequirement> discovered = new List<DeclaredClientCompanionRequirement>();

            try
            {
                var melonMods = MelonMod.RegisteredMelons;
                if (melonMods == null)
                {
                    return discovered;
                }

                foreach (MelonBase melon in melonMods)
                {
                    System.Reflection.Assembly assembly = melon?.MelonAssembly?.Assembly;
                    if (!(melon is IServerMod) || assembly == null)
                    {
                        continue;
                    }

                    object[] attributes = assembly.GetCustomAttributes(typeof(S1DSClientCompanionAttribute), false);
                    for (int i = 0; i < attributes.Length; i++)
                    {
                        if (!(attributes[i] is S1DSClientCompanionAttribute attribute))
                        {
                            continue;
                        }

                        discovered.Add(new DeclaredClientCompanionRequirement
                        {
                            ModId = ClientModPolicy.NormalizeValue(attribute.ModId),
                            DisplayName = ClientModPolicy.NormalizeValue(attribute.DisplayName),
                            Required = attribute.Required,
                            MinVersion = ClientModPolicy.NormalizeValue(attribute.MinVersion),
                            PinnedSha256 = (attribute.PinnedSha256 ?? Array.Empty<string>())
                                .Select(ClientModPolicy.NormalizeHash)
                                .Where(value => !string.IsNullOrEmpty(value))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToList(),
                            SourceAssemblyName = assembly.GetName().Name ?? string.Empty
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error discovering server companion metadata: {ex.Message}");
            }

            return discovered;
        }

        /// <summary>
        /// Notifies all server mods that a save is about to begin.
        /// </summary>
        public static void NotifyBeforeSave()
        {
            if (!S1DS.IsServer) return;

            foreach (var mod in _serverMods.ToList())
            {
                try
                {
                    mod.OnBeforeSave();
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error in server mod {mod.GetType().Name}.OnBeforeSave(): {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Notifies all server mods that a save has completed.
        /// </summary>
        public static void NotifyAfterSave()
        {
            if (!S1DS.IsServer) return;

            foreach (var mod in _serverMods.ToList())
            {
                try
                {
                    mod.OnAfterSave();
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error in server mod {mod.GetType().Name}.OnAfterSave(): {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Notifies all server mods that a load is about to begin.
        /// </summary>
        public static void NotifyBeforeLoad()
        {
            if (!S1DS.IsServer) return;

            foreach (var mod in _serverMods.ToList())
            {
                try
                {
                    mod.OnBeforeLoad();
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error in server mod {mod.GetType().Name}.OnBeforeLoad(): {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Notifies all server mods that a load has completed.
        /// </summary>
        public static void NotifyAfterLoad()
        {
            if (!S1DS.IsServer) return;

            foreach (var mod in _serverMods.ToList())
            {
                try
                {
                    mod.OnAfterLoad();
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error in server mod {mod.GetType().Name}.OnAfterLoad(): {ex.Message}");
                }
            }
        }
        #endif
        #endregion

        internal static List<ClientModDescriptor> GetLoadedClientModsForVerification(System.Reflection.Assembly coreClientAssembly)
        {
            List<ClientModDescriptor> mods = new List<ClientModDescriptor>();

            try
            {
                var melonMods = MelonMod.RegisteredMelons;
                if (melonMods == null)
                {
                    return mods;
                }

                foreach (MelonBase melon in melonMods)
                {
                    System.Reflection.Assembly assembly = melon?.MelonAssembly?.Assembly;
                    if (assembly == null)
                    {
                        continue;
                    }

                    if (coreClientAssembly != null && ReferenceEquals(assembly, coreClientAssembly))
                    {
                        continue;
                    }

                    S1DSClientModIdentityAttribute identity = assembly.GetCustomAttributes(typeof(S1DSClientModIdentityAttribute), false)
                        .OfType<S1DSClientModIdentityAttribute>()
                        .FirstOrDefault();

                    MelonInfoAttribute info = melon.Info;
                    string assemblyName = assembly.GetName().Name ?? string.Empty;
                    string displayName = info?.Name ?? melon.MelonTypeName ?? assemblyName;
                    string version = identity?.Version ?? info?.Version ?? assembly.GetName().Version?.ToString() ?? string.Empty;

                    mods.Add(new ClientModDescriptor
                    {
                        ModId = identity?.ModId ?? string.Empty,
                        Version = version,
                        DisplayName = displayName,
                        Author = info?.Author ?? string.Empty,
                        AssemblyName = assemblyName,
                        Sha256 = ClientModHashUtility.TryResolveSha256(melon),
                        IdentityDeclared = identity != null
                    });
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error discovering client mods for verification: {ex.Message}");
            }

            return mods;
        }

        #region Client Events
        #if CLIENT

        /// <summary>
        /// Notifies all client mods that the client is initializing
        /// </summary>
        public static void NotifyClientInitialize()
        {
            if (!S1DS.IsClient) return;

            MelonLogger.Msg("Notifying client mods of initialization...");
            foreach (var mod in _clientMods.ToList())
            {
                try
                {
                    mod.OnClientInitialize();
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error in client mod {mod.GetType().Name}.OnClientInitialize(): {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Notifies all client mods that the client is shutting down
        /// </summary>
        public static void NotifyClientShutdown()
        {
            if (!S1DS.IsClient) return;

            MelonLogger.Msg("Notifying client mods of shutdown...");
            foreach (var mod in _clientMods.ToList())
            {
                try
                {
                    mod.OnClientShutdown();
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error in client mod {mod.GetType().Name}.OnClientShutdown(): {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Notifies all client mods that they connected to a server
        /// </summary>
        public static void NotifyConnectedToServer()
        {
            if (!S1DS.IsClient) return;

            WireClientMessageForwarding();

            foreach (var mod in _clientMods.ToList())
            {
                try
                {
                    mod.OnConnectedToServer();
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error in client mod {mod.GetType().Name}.OnConnectedToServer(): {ex.Message}");
                }
            }
        }
        /// <summary>
        /// Notifies all client mods that the local player is spawned and systems are ready.
        /// This should be invoked after messaging/UI are initialized to avoid race conditions.
        /// </summary>
        public static void NotifyClientPlayerReady()
        {
            if (!S1DS.IsClient) return;

            foreach (var mod in _clientMods.ToList())
            {
                try
                {
                    mod.OnClientPlayerReady();
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error in client mod {mod.GetType().Name}.OnClientPlayerReady(): {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Notifies all client mods that they disconnected from a server
        /// </summary>
        public static void NotifyDisconnectedFromServer()
        {
            if (!S1DS.IsClient) return;

            foreach (var mod in _clientMods.ToList())
            {
                try
                {
                    mod.OnDisconnectedFromServer();
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error in client mod {mod.GetType().Name}.OnDisconnectedFromServer(): {ex.Message}");
                }
            }
        }
        #endif
        #endregion

#if CLIENT
        private static bool _clientMsgWired;
#endif

#if SERVER
        private static bool _serverMsgWired;
#endif

        #if CLIENT
        private static void WireClientMessageForwarding()
        {
            if (_clientMsgWired) return;
            try
            {
                CustomMessaging.ClientMessageReceived += (cmd, data) =>
                {
                    foreach (var mod in _clientMods.ToList())
                    {
                        if (mod is IClientMod adv)
                        {
                            try { adv.OnCustomMessage(cmd, System.Text.Encoding.UTF8.GetBytes(data ?? string.Empty)); } catch { }
                        }
                    }
                };
                _clientMsgWired = true;
                MelonLogger.Msg("Client message forwarding wired");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error wiring client message forwarding: {ex.Message}");
            }
        }
        #endif

        #if SERVER
        public static void EnsureServerMessageForwarding()
        {
            if (_serverMsgWired) return;
            try
            {
                DedicatedServerMod.Shared.Networking.CustomMessaging.ServerMessageReceived += (conn, cmd, data) =>
                {
                    // Best-effort sender id
                    string senderId = null;
                    try
                    {
                        var p = ScheduleOne.PlayerScripts.Player.GetPlayer(conn);
                        senderId = p?.PlayerCode;
                    }
                    catch { }

                    foreach (var mod in _serverMods.ToList())
                    {
                        if (mod is IServerMod adv)
                        {
                            try { adv.OnCustomMessage(cmd, System.Text.Encoding.UTF8.GetBytes(data ?? string.Empty), senderId); } catch { }
                        }
                    }
                };
                _serverMsgWired = true;
                MelonLogger.Msg("Server message forwarding wired");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error wiring server message forwarding: {ex.Message}");
            }
        }
        #endif

        /// <summary>
        /// Gets all registered server mods
        /// </summary>
        public static IReadOnlyList<IServerMod> ServerMods => _serverMods.AsReadOnly();

        /// <summary>
        /// Gets all registered client mods
        /// </summary>
        public static IReadOnlyList<IClientMod> ClientMods => _clientMods.AsReadOnly();
    }
}
