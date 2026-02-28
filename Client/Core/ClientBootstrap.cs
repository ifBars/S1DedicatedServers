using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
#if IL2CPP
using Il2CppScheduleOne.PlayerScripts;
#else
using ScheduleOne.PlayerScripts;
#endif
using DedicatedServerMod.Client.Managers;
using DedicatedServerMod.Client.Patchers;

[assembly: MelonInfo(typeof(DedicatedServerMod.Client.Core.ClientBootstrap), "DedicatedServerClient", "0.2.1-beta", "Ghost")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace DedicatedServerMod.Client.Core
{
    /// <summary>
    /// Main entry point for the Dedicated Server Client mod.
    /// Minimal responsibilities - delegates to specialized managers and patch classes.
    /// </summary>
    /// <remarks>
    /// This class handles initialization and coordinates between the various
    /// client-side subsystems. All game patches are delegated to specialized
    /// patch classes in the Patches folder.
    /// </remarks>
    public sealed class ClientBootstrap : MelonMod
    {
        #region Private Fields

        /// <summary>
        /// The singleton instance of the bootstrap.
        /// </summary>
        private static ClientBootstrap _instance;

        /// <summary>
        /// The logger instance for this mod.
        /// </summary>
        private MelonLogger.Instance _logger;



        /// <summary>
        /// The connection manager for server communication.
        /// </summary>
        private ClientConnectionManager _connectionManager;

        /// <summary>
        /// The auth manager for Steam ticket handshake.
        /// </summary>
        private ClientAuthManager _authManager;

        /// <summary>
        /// The UI manager for client UI elements.
        /// </summary>
        private ClientUIManager _uiManager;

        /// <summary>
        /// The quest manager for quest-related functionality.
        /// </summary>
        private ClientQuestManager _questManager;

        /// <summary>
        /// The console manager for admin console access.
        /// </summary>
        private ClientConsoleManager _consoleManager;

        /// <summary>
        /// The loopback handler for ghost player management.
        /// </summary>
        private ClientLoopbackHandler _loopbackHandler;

        /// <summary>
        /// The transport patcher for network transport modifications.
        /// </summary>
        private ClientTransportPatcher _transportPatcher;

        /// <summary>
        /// Whether to ignore the ghost host when checking sleep readiness.
        /// </summary>
        private bool _ignoreGhostHostForSleep = true;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the singleton instance of the ClientBootstrap.
        /// </summary>
        public static ClientBootstrap Instance => _instance;

        /// <summary>
        /// Gets the logger instance for this mod.
        /// </summary>
        public MelonLogger.Instance Logger => _logger;

        /// <summary>
        /// Gets the connection manager.
        /// </summary>
        public ClientConnectionManager ConnectionManager => _connectionManager;

        /// <summary>
        /// Gets the auth manager.
        /// </summary>
        public ClientAuthManager AuthManager => _authManager;

        /// <summary>
        /// Gets the UI manager.
        /// </summary>
        public ClientUIManager UIManager => _uiManager;

        /// <summary>
        /// Gets the quest manager.
        /// </summary>
        public ClientQuestManager QuestManager => _questManager;

        /// <summary>
        /// Gets the console manager.
        /// </summary>
        public ClientConsoleManager ConsoleManager => _consoleManager;

        /// <summary>
        /// Gets the loopback handler.
        /// </summary>
        public ClientLoopbackHandler LoopbackHandler => _loopbackHandler;

        /// <summary>
        /// Gets the transport patcher.
        /// </summary>
        public ClientTransportPatcher TransportPatcher => _transportPatcher;

        /// <summary>
        /// Gets or sets whether to ignore the ghost host when checking sleep readiness.
        /// </summary>
        public static bool IgnoreGhostHostForSleep
        {
            get => Instance?._ignoreGhostHostForSleep ?? true;
            set
            {
                if (Instance != null)
                {
                    Instance._ignoreGhostHostForSleep = value;
                    Patches.SleepPatches.IgnoreGhostHostForSleep = value;
                    Instance._logger?.Msg($"Client: Ignore ghost host for sleep set to: {value}");
                }
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Called when the application starts.
        /// </summary>
        public override void OnApplicationStart()
        {
            _instance = this;
        }

        /// <summary>
        /// Called when the melon is initialized.
        /// </summary>
        public override void OnInitializeMelon()
        {
            _logger = LoggerInstance;
            _logger.Msg("DedicatedServerClient mod initializing...");

            try
            {
                // Initialize the debug log system
                Utils.DebugLog.Initialize(_logger);

                // Initialize MessageRouter for client-side message handling
                Shared.Networking.MessageRouter.Initialize(_logger);

                // Initialize messaging service (backend selection)
                Shared.Networking.CustomMessaging.Initialize();

                // Initialize API mod discovery
                API.ModManager.Initialize();

                // Apply client-side patches
                ApplyClientPatches();

                // Subscribe to server data delivery
                SubscribeToServerData();

                // Initialize all client managers
                InitializeManagers();

                _logger.Msg("DedicatedServerClient mod initialized successfully");

                // Notify API mods that client is initialized
                API.ModManager.NotifyClientInitialize();
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to initialize DedicatedServerClient: {ex}");
            }
        }

        #endregion

        #region Patch Application

        /// <summary>
        /// Applies all client-side Harmony patches.
        /// </summary>
        /// <remarks>
        /// MelonLoader automatically applies patches with [HarmonyPatch] attributes.
        /// This method just initializes the patch classes with logger instances.
        /// </remarks>
        private void ApplyClientPatches()
        {
            try
            {
                // Initialize attribute-based patches with logger
                // MelonLoader will automatically apply patches marked with [HarmonyPatch]
                Patches.SleepPatches.Initialize(_logger);
                Patches.MessagingPatches.Initialize(_logger);

                // Apply transport patches (runtime patching needed for flexibility)
                _transportPatcher = new ClientTransportPatcher(_logger);
                _transportPatcher.Initialize();

                _logger.Msg("All client patches initialized");
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to initialize client patches: {ex}");
            }
        }

        #endregion

        #region Manager Initialization

        /// <summary>
        /// Initializes all client manager systems.
        /// </summary>
        private void InitializeManagers()
        {
            // Initialize transport patcher first (needed for connection patching)
            _transportPatcher?.Initialize();

            // Initialize connection manager
            _connectionManager = new ClientConnectionManager(_logger);
            _connectionManager.Initialize();

            // Initialize authentication manager
            _authManager = new ClientAuthManager(_logger);
            _authManager.Initialize();

            // Initialize console manager (for admin console access)
            _consoleManager = new ClientConsoleManager(_logger);
            _consoleManager.Initialize();

            // Initialize quest manager
            _questManager = new ClientQuestManager(_logger);
            _questManager.Initialize();

            // Initialize loopback handler
            _loopbackHandler = new ClientLoopbackHandler(_logger);
            _loopbackHandler.Initialize();

            // Initialize UI manager (depends on connection manager)
            _uiManager = new ClientUIManager(_logger, _connectionManager);
            _uiManager.Initialize();
        }

        #endregion

        #region Server Data Subscription

        /// <summary>
        /// Subscribes to server data delivery events.
        /// </summary>
        private void SubscribeToServerData()
        {
            try
            {
                Patches.MessagingPatches.ClientMessageReceived += (cmd, data) =>
                {
                    try
                    {
                        if (cmd == Utils.Constants.Messages.ServerData)
                        {
                            var serverData = Newtonsoft.Json.JsonConvert.DeserializeObject<Shared.ServerData>(data);
                            if (serverData != null)
                            {
                                Managers.ServerDataStore.Update(serverData);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Error handling server message '{cmd}': {ex}");
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.Error($"Error subscribing to server data: {ex}");
            }
        }

        #endregion

        #region Scene and Update Handling

        /// <summary>
        /// Called when a scene is loaded.
        /// </summary>
        /// <param name="buildIndex">The build index of the loaded scene</param>
        /// <param name="sceneName">The name of the loaded scene</param>
        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            try
            {
                // Delegate scene handling to appropriate managers
                _uiManager?.OnSceneLoaded(sceneName);
                _questManager?.OnSceneLoaded(sceneName);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error handling scene load ({sceneName}): {ex}");
            }
        }

        /// <summary>
        /// Called every frame.
        /// </summary>
        public override void OnUpdate()
        {
            try
            {
                // Handle UI input
                _uiManager?.HandleInput();

                // Handle auth updates
                _authManager?.Update();

                // Handle debug input
                HandleDebugInput();

                // Pump messaging backend callbacks
                Shared.Networking.CustomMessaging.Tick();
            }
            catch (Exception ex)
            {
                _logger.Error($"Error in OnUpdate: {ex}");
            }
        }

        /// <summary>
        /// Called when the application quits.
        /// </summary>
        public override void OnApplicationQuit()
        {
            try
            {
                _authManager?.Shutdown();
            }
            catch (Exception ex)
            {
                _logger?.Warning($"Error shutting down client auth manager: {ex.Message}");
            }

            try
            {
                Shared.Networking.CustomMessaging.Shutdown();
            }
            catch (Exception ex)
            {
                _logger?.Warning($"Error shutting down messaging service: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles debug key inputs for testing purposes.
        /// </summary>
        private void HandleDebugInput()
        {
            // Debug key for testing (F9)
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F9))
            {
                _logger.Msg("F9 pressed - triggering prototype connection");
                _connectionManager?.StartDedicatedConnection();
            }
        }

        #endregion

        #region Debugging

        /// <summary>
        /// Logs a message with the debug logging system.
        /// </summary>
        /// <param name="message">The message to log</param>
        public void DebugLog(string message)
        {
            Utils.DebugLog.Debug(message);
        }

        #endregion
    }
}
