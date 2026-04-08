using MelonLoader;
using DedicatedServerMod.Server.Network;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Server.Commands;
using DedicatedServerMod.Server.Persistence;
using DedicatedServerMod.Server.Game;
using DedicatedServerMod.Shared.Configuration;
using DedicatedServerMod.Shared.Permissions;
using DedicatedServerMod.API;
using UnityEngine;
using DedicatedServerMod.Utils;
using DedicatedServerMod.Server.HostConsole;
using DedicatedServerMod.Server.Permissions;
using DedicatedServerMod.Server.WebPanel;
using DedicatedServerMod.Server.CustomClothing;

[assembly: MelonInfo(typeof(DedicatedServerMod.Server.Core.ServerBootstrap), "DedicatedServerHost", DedicatedServerMod.API.Version.ModVersion, DedicatedServerMod.Utils.Constants.Author)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace DedicatedServerMod.Server.Core
{
    /// <summary>
    /// Main server bootstrap class responsible for initializing all server subsystems.
    /// This is the central coordinator that starts up the dedicated server in the correct order.
    /// </summary>
    public class ServerBootstrap : MelonMod
    {
        private static MelonLogger.Instance _logger;
        private static bool _isInitialized = false;
        private static bool _isShuttingDown = false;
        private static bool _quitRequested = false;
        
        // Server subsystems
        private static NetworkManager _networkManager;
        private static PlayerManager _playerManager;
        private static ServerPermissionService _permissionService;
        private static CommandManager _commandManager;
        private static PersistenceManager _persistenceManager;
        private static GameSystemManager _gameSystemManager;
        private static HostConsoleManager _hostConsoleManager;
        private static WebPanelManager _webPanelManager;
        private static SteamNetworkLibCompatService _steamNetworkLibCompatService;
        private static ServerStatusQueryService _serverStatusQueryService;
        private static ServerCustomClothingManager _customClothingManager;
        
        // Server state
        private static bool _autoStartServer = false;
        
        /// <summary>
        /// Gets whether the server has been fully initialized
        /// </summary>
        public static bool IsInitialized => _isInitialized;
        
        /// <summary>
        /// Gets the network manager instance
        /// </summary>
        public static NetworkManager Network => _networkManager;
        
        /// <summary>
        /// Gets the player manager instance
        /// </summary>
        public static PlayerManager Players => _playerManager;
        
        /// <summary>
        /// Gets the command manager instance
        /// </summary>
        public static CommandManager Commands => _commandManager;

        /// <summary>
        /// Gets the permission service instance.
        /// </summary>
        public static ServerPermissionService Permissions => _permissionService;
        
        /// <summary>
        /// Gets the persistence manager instance
        /// </summary>
        public static PersistenceManager Persistence => _persistenceManager;
        
        /// <summary>
        /// Gets the game system manager instance
        /// </summary>
        public static GameSystemManager GameSystems => _gameSystemManager;
        
        /// <summary>
        /// Gets the SteamNetworkLib dedicated compatibility service.
        /// </summary>
        public static SteamNetworkLibCompatService SteamNetworkLibCompat => _steamNetworkLibCompatService;

        internal static ServerCustomClothingManager CustomClothing => _customClothingManager;

        public override void OnInitializeMelon()
        {
            _logger = LoggerInstance;
            DebugLog.Initialize(_logger);
            DebugLog.Info("=== Dedicated Server Starting ===");
            
            try
            {
                // Initialize mod discovery so OnServerInitialize can be delivered after subsystems init
                ModManager.Initialize();
                InitializeServer();
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Critical error during server initialization: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Initialize all server subsystems in the correct order
        /// </summary>
        private void InitializeServer()
        {
            AudioListener.volume = 0;
            
            DebugLog.StartupDebug("Initializing server subsystems...");
            
            // Step 1: Initialize existing ServerConfig system (must be first)
            ServerConfig.Initialize(_logger);
            DebugLog.StartupDebug("Configuration system initialized");
            
            // Step 2: Parse command line arguments early
            ParseCommandLineArguments();

            PermissionManager.Initialize(_logger);
            PlayerResolver.Initialize(_logger);
            
            ServerRuntimeConfigurationApplier runtimeConfigurationApplier = new ServerRuntimeConfigurationApplier(ServerConfig.Instance, _logger);
            runtimeConfigurationApplier.Apply();
            _permissionService = new ServerPermissionService(_logger);
            _permissionService.Initialize();
            DebugLog.StartupDebug("Permission service initialized");

            _customClothingManager = new ServerCustomClothingManager(_logger);
            DebugLog.StartupDebug("Custom clothing manager created");
            
            // Step 3: Apply Harmony patches via GameSystemManager (which owns patch manager)
            
            // Step 4: Network Manager
            _networkManager = new NetworkManager(_logger);
            _networkManager.Initialize();
            DebugLog.StartupDebug("Network manager initialized");
            
            // Step 5: Player Manager
            _playerManager = new PlayerManager(_logger);
            _playerManager.Initialize();
            _permissionService.AttachPlayerManager(_playerManager);
            DebugLog.StartupDebug("Player manager initialized");

            // Step 5a: Messaging Service (required for custom server-client communication)
            Shared.Networking.CustomMessaging.Initialize();
            DebugLog.StartupDebug("Messaging service initialized");

            // Step 5b: SteamNetworkLib dedicated compatibility layer
            _steamNetworkLibCompatService = new SteamNetworkLibCompatService(_logger, _playerManager);
            _steamNetworkLibCompatService.Initialize();
            DebugLog.StartupDebug("SteamNetworkLib compatibility service initialized");

            // Step 6: Command Manager
            _commandManager = new CommandManager(_playerManager, _networkManager);
            _commandManager.Initialize();
            DebugLog.StartupDebug("Command manager initialized");
            
            // Step 7: Persistence Manager
            _persistenceManager = new PersistenceManager(_logger);
            _persistenceManager.Initialize();
            DebugLog.StartupDebug("Persistence manager initialized");
            
            // Step 8: Game System Manager
            _gameSystemManager = new GameSystemManager(_logger);
            _gameSystemManager.Initialize();
            DebugLog.StartupDebug("Game system manager initialized");
            
            _hostConsoleManager = new HostConsoleManager(_commandManager, _logger);
            _hostConsoleManager.Start();
            TryStartWebPanel();
            TryStartStatusQueryService();
            
            // Step 10: Wire up player events with persistence
            WirePlayerEvents();
            
            _isInitialized = true;
            DebugLog.StartupDebug("=== Dedicated Server Bootstrap Complete ===");

            // Notify API mods: server initialized and running
            ModManager.NotifyServerInitialize();

            // Ensure server message forwarding is wired after init
            ModManager.EnsureServerMessageForwarding();
        }

        public override void OnLateInitializeMelon()
        {
            base.OnLateInitializeMelon();
            
            // Step 11: Auto-start server if requested via command line
            // Moved here to ensure Unity's coroutine system is ready
            if (_autoStartServer)
            {
                _logger.Msg("Auto-starting server due to command line flag (full orchestrated sequence)");
                MelonCoroutines.Start(ServerStartupOrchestrator.StartDedicatedServer());
            }
        }

        /// <summary>
        /// Ticks runtime server subsystems that require frame updates.
        /// </summary>
        public override void OnUpdate()
        {
            try
            {
                _playerManager?.Update();
            }
            catch (Exception ex)
            {
                _logger?.Warning($"Server update tick error: {ex.Message}");
            }

            try
            {
                _permissionService?.Tick();
            }
            catch (Exception ex)
            {
                _logger?.Warning($"Permission service tick error: {ex.Message}");
            }

            try
            {
                Shared.Networking.CustomMessaging.Tick();
            }
            catch (Exception ex)
            {
                _logger?.Warning($"Messaging service tick error: {ex.Message}");
            }

            try
            {
                _webPanelManager?.Tick();
            }
            catch (Exception ex)
            {
                _logger?.Warning($"Web panel tick error: {ex.Message}");
            }

            try
            {
                _customClothingManager?.Tick();
            }
            catch (Exception ex)
            {
                _logger?.Warning($"Custom clothing tick error: {ex.Message}");
            }
        }

        public override void OnApplicationQuit()
        {
            if (_isInitialized && !_isShuttingDown)
            {
                Shutdown("Application quit");
            }
        }

        /// <summary>
        /// Process command line arguments for server startup
        /// </summary>
        private void ParseCommandLineArguments()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                
                // Process server-specific arguments
                for (int i = 0; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "--dedicated-server":
                        case "--server":
                            _autoStartServer = true;
                            DebugLog.StartupDebug("Dedicated server mode enabled via command line");
                            break;
                    }
                }
                
                // Let ServerConfig handle its own command line arguments
                ServerConfig.ParseCommandLineArgs(args, persistChanges: false);
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"Error processing command line arguments: {ex.Message}");
            }
        }

        /// <summary>
        /// Wire up player events with persistence system
        /// </summary>
        private void WirePlayerEvents()
        {
            try
            {
                if (_playerManager != null && _persistenceManager != null)
                {
                    _playerManager.OnPlayerJoined += (player) =>
                    {
                        if (player != null && player.IsLoopbackConnection)
                        {
                            _logger.Msg("Skipping auto-save for dedicated server loopback host join.");
                            return;
                        }

                        _persistenceManager.OnPlayerJoined(player.DisplayName);
                    };
                    
                    _playerManager.OnPlayerLeft += (player) =>
                    {
                        if (player != null && player.IsLoopbackConnection)
                        {
                            DebugLog.PlayerLifecycleDebug("Skipping auto-save for dedicated server loopback host leave.");
                            return;
                        }

                        _persistenceManager.OnPlayerLeft(player.DisplayName);
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error wiring player events: {ex}");
            }
        }

        // Orchestrator handles the entire boot sequence now

        /// <summary>
        /// Shutdown the server gracefully
        /// </summary>
        public static void Shutdown(string reason = "Server shutdown requested")
        {
            if (_isShuttingDown)
            {
                _logger?.Warning($"Server shutdown already in progress: {reason}");
                return;
            }

            if (!_isInitialized)
            {
                _logger?.Warning("Server not initialized, cannot shutdown");
                return;
            }

            _isShuttingDown = true;
            _logger.Msg($"=== Server Shutdown Initiated: {reason} ===");
            
            try
            {
                // Notify API mods prior to tearing down subsystems
                ModManager.NotifyServerShutdown();

                // Notify players before messaging and transport layers are torn down.
                _playerManager?.NotifyShutdownAndDisconnectAll(reason);

                // Shutdown in reverse order
                try { _webPanelManager?.Dispose(); } catch { }
                try { _hostConsoleManager?.Dispose(); } catch { }
                _serverStatusQueryService?.Shutdown();
                _gameSystemManager?.Shutdown();
                _persistenceManager?.Shutdown();
                _commandManager?.Shutdown();
                _permissionService?.Shutdown();
                _steamNetworkLibCompatService?.Shutdown();
                Shared.Networking.CustomMessaging.Shutdown();
                _playerManager?.Shutdown();
                _networkManager?.Shutdown();
                
                _isInitialized = false;
                _logger.Msg("=== Server Shutdown Complete ===");

                if (!_quitRequested)
                {
                    _quitRequested = true;
                    _logger.Msg("Requesting application quit");
                    Application.Quit();
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error during server shutdown: {ex}");
            }
        }

        private void TryStartStatusQueryService()
        {
            try
            {
                _serverStatusQueryService = new ServerStatusQueryService(_logger, _playerManager, _customClothingManager);
                _serverStatusQueryService.Start();
                DebugLog.StartupDebug("Status query service initialized");
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"Status query service failed to start: {ex.Message}");
            }
        }

        private void TryStartWebPanel()
        {
            try
            {
                _webPanelManager = new WebPanelManager(_logger, _networkManager, _playerManager, _permissionService, _commandManager, _persistenceManager);
                _webPanelManager.Start();
                DebugLog.StartupDebug("Web panel initialized");
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"Web panel failed to start: {ex.Message}");
            }
        }

        public static DateTime LastAutoSave => _persistenceManager?.LastAutoSave ?? DateTime.MinValue;
    }
}

