using System;
using System.Collections.Generic;
using System.Linq;
using DedicatedServerMod.Shared.Networking;
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

            MelonLogger.Msg("Initializing ModManager...");

            // Discover and register mods from loaded MelonMods
            DiscoverMods();

            _initialized = true;
            MelonLogger.Msg($"ModManager initialized. Found {_serverMods.Count} server mods and {_clientMods.Count} client mods.");
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

                if (S1DS.IsClient)
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
        #endif
        #endregion

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

        private static bool _clientMsgWired;
        private static bool _serverMsgWired;

        #if CLIENT
        private static void WireClientMessageForwarding()
        {
            if (_clientMsgWired) return;
            try
            {
                Shared.Networking.CustomMessaging.ClientMessageReceived += (cmd, data) =>
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
