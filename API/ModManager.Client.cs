#if CLIENT
using DedicatedServerMod.Shared.Networking;
using DedicatedServerMod.Utils;

namespace DedicatedServerMod.API
{
    public static partial class ModManager
    {
        /// <summary>
        /// Occurs when the client runtime begins notifying mods that the client is initializing.
        /// </summary>
        public static event Action ClientInitializing;

        /// <summary>
        /// Occurs when the client runtime begins notifying mods that the client is shutting down.
        /// </summary>
        public static event Action ClientShuttingDown;

        /// <summary>
        /// Occurs when the client runtime begins notifying mods that it connected to a dedicated server.
        /// </summary>
        public static event Action ClientConnectedToServer;

        /// <summary>
        /// Occurs when the client runtime begins notifying mods that it disconnected from a dedicated server.
        /// </summary>
        public static event Action ClientDisconnectedFromServer;

        /// <summary>
        /// Occurs when the local client player is ready and dedicated-server client systems are initialized.
        /// </summary>
        public static event Action ClientPlayerReady;

        /// <summary>
        /// Occurs when the client receives a forwarded custom message from the dedicated server.
        /// </summary>
        public static event Action<string, byte[]> ClientCustomMessageReceived;

        /// <summary>
        /// Notifies all client mods that the client is initializing.
        /// </summary>
        public static void NotifyClientInitialize()
        {
            if (!S1DS.IsClient)
            {
                return;
            }

            DebugLog.StartupDebug("Notifying client mods of initialization...");
            InvokeEventSafely(ClientInitializing, nameof(ClientInitializing));
            foreach (IClientMod mod in _clientMods.ToList())
            {
                try
                {
                    mod.OnClientInitialize();
                }
                catch (Exception ex)
                {
                    DebugLog.Error($"Error in client mod {mod.GetType().Name}.OnClientInitialize(): {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Notifies all client mods that the client is shutting down.
        /// </summary>
        public static void NotifyClientShutdown()
        {
            if (!S1DS.IsClient)
            {
                return;
            }

            DebugLog.Info("Notifying client mods of shutdown...");
            InvokeEventSafely(ClientShuttingDown, nameof(ClientShuttingDown));
            foreach (IClientMod mod in _clientMods.ToList())
            {
                try
                {
                    mod.OnClientShutdown();
                }
                catch (Exception ex)
                {
                    DebugLog.Error($"Error in client mod {mod.GetType().Name}.OnClientShutdown(): {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Notifies all client mods that they connected to a dedicated server.
        /// </summary>
        public static void NotifyConnectedToServer()
        {
            if (!S1DS.IsClient)
            {
                return;
            }

            WireClientMessageForwarding();
            InvokeEventSafely(ClientConnectedToServer, nameof(ClientConnectedToServer));
            foreach (IClientMod mod in _clientMods.ToList())
            {
                try
                {
                    mod.OnConnectedToServer();
                }
                catch (Exception ex)
                {
                    DebugLog.Error($"Error in client mod {mod.GetType().Name}.OnConnectedToServer(): {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Notifies all client mods that the local player is spawned and systems are ready.
        /// </summary>
        public static void NotifyClientPlayerReady()
        {
            if (!S1DS.IsClient)
            {
                return;
            }

            InvokeEventSafely(ClientPlayerReady, nameof(ClientPlayerReady));
            foreach (IClientMod mod in _clientMods.ToList())
            {
                try
                {
                    mod.OnClientPlayerReady();
                }
                catch (Exception ex)
                {
                    DebugLog.Error($"Error in client mod {mod.GetType().Name}.OnClientPlayerReady(): {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Notifies all client mods that they disconnected from a dedicated server.
        /// </summary>
        public static void NotifyDisconnectedFromServer()
        {
            if (!S1DS.IsClient)
            {
                return;
            }

            InvokeEventSafely(ClientDisconnectedFromServer, nameof(ClientDisconnectedFromServer));
            foreach (IClientMod mod in _clientMods.ToList())
            {
                try
                {
                    mod.OnDisconnectedFromServer();
                }
                catch (Exception ex)
                {
                    DebugLog.Error($"Error in client mod {mod.GetType().Name}.OnDisconnectedFromServer(): {ex.Message}", ex);
                }
            }
        }

        private static void WireClientMessageForwarding()
        {
            if (_clientMsgWired)
            {
                return;
            }

            try
            {
                CustomMessaging.ClientMessageReceived += (cmd, data) =>
                {
                    string payloadText = data ?? string.Empty;
                    byte[] payload = System.Text.Encoding.UTF8.GetBytes(payloadText);

                    InvokeEventSafely(ClientCustomMessageReceived, nameof(ClientCustomMessageReceived), cmd, payload);
                    foreach (IClientMod mod in _clientMods.ToList())
                    {
                        try
                        {
                            mod.OnCustomMessage(cmd, System.Text.Encoding.UTF8.GetBytes(payloadText));
                        }
                        catch (Exception ex)
                        {
                            DebugLog.Error($"Error in client mod {mod.GetType().Name}.OnCustomMessage(): {ex.Message}", ex);
                        }
                    }
                };

                _clientMsgWired = true;
                DebugLog.StartupDebug("Client message forwarding wired");
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error wiring client message forwarding: {ex.Message}", ex);
            }
        }
    }
}
#endif
