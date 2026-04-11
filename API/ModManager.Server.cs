#if SERVER
using DedicatedServerMod.Server.Core;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Shared.Networking;
using DedicatedServerMod.Utils;
using MelonLoader;

namespace DedicatedServerMod.API
{
    public static partial class ModManager
    {
        /// <summary>
        /// Occurs when the dedicated-server runtime begins notifying mods that the server is initializing.
        /// </summary>
        public static event Action ServerInitializing;

        /// <summary>
        /// Occurs when the dedicated-server runtime begins notifying mods that the server has started.
        /// </summary>
        public static event Action ServerStarted;

        /// <summary>
        /// Occurs when the dedicated-server runtime begins notifying mods that the server is shutting down.
        /// </summary>
        public static event Action ServerShuttingDown;

        /// <summary>
        /// Occurs when a tracked player completes the dedicated-server join flow.
        /// </summary>
        public static event Action<ConnectedPlayerInfo> ServerPlayerConnected;

        /// <summary>
        /// Occurs when a tracked player disconnects from the dedicated server.
        /// </summary>
        public static event Action<ConnectedPlayerInfo> ServerPlayerDisconnected;

        /// <summary>
        /// Occurs before server save callbacks are delivered.
        /// </summary>
        public static event Action ServerBeforeSave;

        /// <summary>
        /// Occurs after server save callbacks are delivered.
        /// </summary>
        public static event Action ServerAfterSave;

        /// <summary>
        /// Occurs before server load callbacks are delivered.
        /// </summary>
        public static event Action ServerBeforeLoad;

        /// <summary>
        /// Occurs after server load callbacks are delivered.
        /// </summary>
        public static event Action ServerAfterLoad;

        /// <summary>
        /// Occurs when the server receives a forwarded custom message from a client mod.
        /// </summary>
        public static event Action<string, byte[], ConnectedPlayerInfo> ServerCustomMessageReceived;

        /// <summary>
        /// Notifies all server mods that the server is initializing.
        /// </summary>
        public static void NotifyServerInitialize()
        {
            if (!S1DS.IsServer)
            {
                return;
            }

            MelonLogger.Msg("Notifying server mods of initialization...");
            InvokeEventSafely(ServerInitializing, nameof(ServerInitializing));
            foreach (IServerMod mod in _serverMods.ToList())
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
        /// Notifies all server mods that the server has started.
        /// </summary>
        public static void NotifyServerStarted()
        {
            if (!S1DS.IsServer)
            {
                return;
            }

            MelonLogger.Msg("Notifying server mods of startup...");
            InvokeEventSafely(ServerStarted, nameof(ServerStarted));
            foreach (IServerMod mod in _serverMods.ToList())
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
        /// Notifies all server mods that the server is shutting down.
        /// </summary>
        public static void NotifyServerShutdown()
        {
            if (!S1DS.IsServer)
            {
                return;
            }

            MelonLogger.Msg("Notifying server mods of shutdown...");
            InvokeEventSafely(ServerShuttingDown, nameof(ServerShuttingDown));
            foreach (IServerMod mod in _serverMods.ToList())
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
        /// Notifies all server mods that a player connected.
        /// </summary>
        /// <param name="player">Tracked player details for the completed join.</param>
        public static void NotifyPlayerConnected(ConnectedPlayerInfo player)
        {
            if (!S1DS.IsServer || player == null)
            {
                return;
            }

            InvokeEventSafely(ServerPlayerConnected, nameof(ServerPlayerConnected), player);
            foreach (IServerMod mod in _serverMods.ToList())
            {
                try
                {
                    DispatchServerPlayerConnected(mod, player);
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error in server mod {mod.GetType().Name}.OnPlayerConnected(): {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Notifies all server mods that a player connected using the legacy string identifier.
        /// </summary>
        /// <param name="playerId">The legacy player identifier.</param>
        public static void NotifyPlayerConnected(string playerId)
        {
            if (!S1DS.IsServer)
            {
                return;
            }

            foreach (IServerMod mod in _serverMods.ToList())
            {
                try
                {
#pragma warning disable CS0618
                    mod.OnPlayerConnected(playerId);
#pragma warning restore CS0618
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error in server mod {mod.GetType().Name}.OnPlayerConnected(): {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Notifies all server mods that a player disconnected.
        /// </summary>
        /// <param name="player">Tracked player details captured at disconnect time.</param>
        public static void NotifyPlayerDisconnected(ConnectedPlayerInfo player)
        {
            if (!S1DS.IsServer || player == null)
            {
                return;
            }

            InvokeEventSafely(ServerPlayerDisconnected, nameof(ServerPlayerDisconnected), player);
            foreach (IServerMod mod in _serverMods.ToList())
            {
                try
                {
                    DispatchServerPlayerDisconnected(mod, player);
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error in server mod {mod.GetType().Name}.OnPlayerDisconnected(): {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Notifies all server mods that a player disconnected using the legacy string identifier.
        /// </summary>
        /// <param name="playerId">The legacy player identifier.</param>
        public static void NotifyPlayerDisconnected(string playerId)
        {
            if (!S1DS.IsServer)
            {
                return;
            }

            foreach (IServerMod mod in _serverMods.ToList())
            {
                try
                {
#pragma warning disable CS0618
                    mod.OnPlayerDisconnected(playerId);
#pragma warning restore CS0618
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error in server mod {mod.GetType().Name}.OnPlayerDisconnected(): {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Notifies all server mods that a save is about to begin.
        /// </summary>
        public static void NotifyBeforeSave()
        {
            if (!S1DS.IsServer)
            {
                return;
            }

            InvokeEventSafely(ServerBeforeSave, nameof(ServerBeforeSave));
            foreach (IServerMod mod in _serverMods.ToList())
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
            if (!S1DS.IsServer)
            {
                return;
            }

            InvokeEventSafely(ServerAfterSave, nameof(ServerAfterSave));
            foreach (IServerMod mod in _serverMods.ToList())
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
            if (!S1DS.IsServer)
            {
                return;
            }

            InvokeEventSafely(ServerBeforeLoad, nameof(ServerBeforeLoad));
            foreach (IServerMod mod in _serverMods.ToList())
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
            if (!S1DS.IsServer)
            {
                return;
            }

            InvokeEventSafely(ServerAfterLoad, nameof(ServerAfterLoad));
            foreach (IServerMod mod in _serverMods.ToList())
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

        /// <summary>
        /// Ensures forwarded server custom-message handling is wired exactly once.
        /// </summary>
        public static void EnsureServerMessageForwarding()
        {
            if (_serverMsgWired)
            {
                return;
            }

            try
            {
                CustomMessaging.ServerMessageReceived += (conn, cmd, data) =>
                {
                    ConnectedPlayerInfo sender = null;
                    try
                    {
                        sender = ServerBootstrap.Players?.GetPlayer(conn);
                    }
                    catch
                    {
                    }

                    byte[] payload = System.Text.Encoding.UTF8.GetBytes(data ?? string.Empty);

                    InvokeEventSafely(ServerCustomMessageReceived, nameof(ServerCustomMessageReceived), cmd, payload, sender);
                    foreach (IServerMod mod in _serverMods.ToList())
                    {
                        try
                        {
                            DispatchServerCustomMessage(mod, cmd, payload, sender);
                        }
                        catch (Exception ex)
                        {
                            DebugLog.Error($"Error in server mod {mod.GetType().Name}.OnCustomMessage(): {ex.Message}", ex);
                        }
                    }
                };

                _serverMsgWired = true;
                DebugLog.StartupDebug("Server message forwarding wired");
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error wiring server message forwarding: {ex.Message}", ex);
            }
        }

        private static void DispatchServerPlayerConnected(IServerMod mod, ConnectedPlayerInfo player)
        {
            switch (mod)
            {
                case ServerModBase serverMod:
                    serverMod.OnPlayerConnected(player);
                    break;
                case ServerMelonModBase serverMelonMod:
                    serverMelonMod.OnPlayerConnected(player);
                    break;
                default:
#pragma warning disable CS0618
                    mod.OnPlayerConnected(GetLegacyPlayerIdentifier(player));
#pragma warning restore CS0618
                    break;
            }
        }

        private static void DispatchServerPlayerDisconnected(IServerMod mod, ConnectedPlayerInfo player)
        {
            switch (mod)
            {
                case ServerModBase serverMod:
                    serverMod.OnPlayerDisconnected(player);
                    break;
                case ServerMelonModBase serverMelonMod:
                    serverMelonMod.OnPlayerDisconnected(player);
                    break;
                default:
#pragma warning disable CS0618
                    mod.OnPlayerDisconnected(GetLegacyPlayerIdentifier(player));
#pragma warning restore CS0618
                    break;
            }
        }

        private static void DispatchServerCustomMessage(IServerMod mod, string messageType, byte[] data, ConnectedPlayerInfo sender)
        {
            switch (mod)
            {
                case ServerModBase serverMod:
                    serverMod.OnCustomMessage(messageType, data, sender);
                    break;
                case ServerMelonModBase serverMelonMod:
                    serverMelonMod.OnCustomMessage(messageType, data, sender);
                    break;
                default:
#pragma warning disable CS0618
                    mod.OnCustomMessage(messageType, data, GetLegacyPlayerIdentifier(sender));
#pragma warning restore CS0618
                    break;
            }
        }

        private static string GetLegacyPlayerIdentifier(ConnectedPlayerInfo player)
        {
            if (player == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(player.TrustedUniqueId))
            {
                return player.TrustedUniqueId;
            }

            if (!string.IsNullOrWhiteSpace(player.UniqueId))
            {
                return player.UniqueId;
            }

            return player.ClientId.ToString();
        }
    }
}
#endif
