using MelonLoader;
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using DedicatedServerMod.Server.Network;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Server.Commands;
using DedicatedServerMod.Server.Persistence;
using DedicatedServerMod.Server.Game;
using DedicatedServerMod.Shared;
using DedicatedServerMod.Shared.Configuration;
using HarmonyLib;
using DedicatedServerMod.API;
using UnityEngine;
using DedicatedServerMod.Server.TcpConsole;
using MasterServerClient = DedicatedServerMod.Server.Network.MasterServerClient;

[assembly: MelonInfo(typeof(DedicatedServerMod.Server.Core.ServerBootstrap), "DedicatedServerHost", "1.0.0", "Bars")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace DedicatedServerMod.Server.Core
{
    /// <summary>
    /// Main server bootstrap class responsible for initializing all server subsystems.
    /// This is the central coordinator that starts up the dedicated server in the correct order.
    /// </summary>
    public class ServerBootstrap : MelonMod
    {
        private static MelonLogger.Instance logger;
        private static bool _isInitialized = false;
        
        // Server subsystems
        private static NetworkManager _networkManager;
        private static PlayerManager _playerManager;
        private static CommandManager _commandManager;
        private static PersistenceManager _persistenceManager;
        private static GameSystemManager _gameSystemManager;
        private static TcpConsoleServer _tcpConsole;
        private static MasterServerClient _masterServerClient;
        
        // Server state
        private static bool _isServerMode = false;
        private static bool _autoStartServer = false;
        
        /// <summary>
        /// Gets whether the server has been fully initialized
        /// </summary>
        public static bool IsInitialized => _isInitialized;
        
        /// <summary>
        /// Gets the server configuration instance (from existing ServerConfig)
        /// </summary>
        public static Shared.Configuration.ServerConfig Configuration => Shared.Configuration.ServerConfig.Instance;
        
        /// <summary>
        /// Gets the network manager instance
        /// </summary>
        public static NetworkManager Network => _networkManager;
        
        /// <summary>
        /// Gets the player manager instance
        /// </summary>
        public static PlayerManager Players => _playerManager;
        
        /// <summary>
        /// Gets the player manager instance (method form for compatibility).
        /// </summary>
        public PlayerManager GetPlayerManager() => _playerManager;
        
        /// <summary>
        /// Gets the command manager instance
        /// </summary>
        public static CommandManager Commands => _commandManager;
        
        /// <summary>
        /// Gets the persistence manager instance
        /// </summary>
        public static PersistenceManager Persistence => _persistenceManager;
        
        /// <summary>
        /// Gets the game system manager instance
        /// </summary>
        public static GameSystemManager GameSystems => _gameSystemManager;
        
        /// <summary>
        /// Gets the master server client instance
        /// </summary>
        public static MasterServerClient MasterServer => _masterServerClient;

        public override void OnInitializeMelon()
        {
            logger = LoggerInstance;
            logger.Msg("=== Dedicated Server Bootstrap Starting ===");
            
            try
            {
                // Initialize mod discovery so OnServerInitialize can be delivered after subsystems init
                ModManager.Initialize();
                InitializeServer();
            }
            catch (Exception ex)
            {
                logger.Error($"Critical error during server initialization: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Initialize all server subsystems in the correct order
        /// </summary>
        private void InitializeServer()
        {
            AudioListener.volume = 0;
            
            // Suppress Unity rendering/shader errors in headless mode
            SetupLogFiltering();
            
            logger.Msg("Initializing server subsystems...");
            
            // Step 1: Initialize existing ServerConfig system (must be first)
            Shared.Configuration.ServerConfig.Initialize(logger);
            logger.Msg("✓ Configuration system initialized");
            
            // Step 2: Parse command line arguments early
            ParseCommandLineArguments();
            
            // Apply performance optimizations for headless server
            Application.targetFrameRate = Shared.Configuration.ServerConfig.Instance.TargetFrameRate;
            QualitySettings.vSyncCount = Shared.Configuration.ServerConfig.Instance.VSyncCount;
            logger.Msg($"✓ Performance settings applied: Target FPS={Application.targetFrameRate}, VSync={QualitySettings.vSyncCount}");
            
            // Log the save path being used
            string resolvedSavePath = Shared.Configuration.ServerConfig.GetResolvedSaveGamePath();
            if (string.IsNullOrEmpty(Shared.Configuration.ServerConfig.Instance.SaveGamePath))
            {
                logger.Msg($"Using default save location: {resolvedSavePath}");
                logger.Msg("Tip: You can set a custom 'saveGamePath' in server_config.json to use a different save folder.");
            }
            else
            {
                logger.Msg($"Using custom save location: {resolvedSavePath}");
            }
            
            // Step 3: Apply Harmony patches via GameSystemManager (which owns patch manager)
            
            // Step 4: Network Manager
            _networkManager = new NetworkManager(logger);
            _networkManager.Initialize();
            logger.Msg("✓ Network manager initialized");
            
            // Step 5: Player Manager  
            _playerManager = new PlayerManager(logger);
            _playerManager.Initialize();
            logger.Msg("✓ Player manager initialized");
            
            // Step 6: Command Manager
            _commandManager = new CommandManager(logger, _playerManager);
            _commandManager.Initialize();
            logger.Msg("✓ Command manager initialized");
            
            // Step 7: Persistence Manager
            _persistenceManager = new PersistenceManager(logger);
            _persistenceManager.Initialize();
            logger.Msg("✓ Persistence manager initialized");
            
            // Step 8: Game System Manager
            _gameSystemManager = new GameSystemManager(logger);
            _gameSystemManager.Initialize();
            logger.Msg("✓ Game system manager initialized");
            
            // Step 9: Master Server Client
            _masterServerClient = new MasterServerClient(logger);
            _masterServerClient.Initialize();
            logger.Msg("✓ Master server client initialized");
            
            // Start TCP Console if enabled in ServerConfig
            TryStartTcpConsole();
            
            // Step 10: Wire up player events with persistence
            WirePlayerEvents();
            
            _isInitialized = true;
            logger.Msg("=== Dedicated Server Bootstrap Complete ===");

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
                logger.Msg("Auto-starting server due to command line flag (full orchestrated sequence)");
                
                // Use a dedicated GameObject to ensure coroutines run reliably
                var runnerGo = GameObject.Find("DedicatedServerRoutineRunner");
                if (runnerGo == null)
                {
                    runnerGo = new GameObject("DedicatedServerRoutineRunner");
                    GameObject.DontDestroyOnLoad(runnerGo);
                }
                
                var runner = runnerGo.GetComponent<RoutineRunner>();
                if (runner == null) runner = runnerGo.AddComponent<RoutineRunner>();
                
                runner.StartCoroutine(ServerStartupOrchestrator.StartDedicatedServer());
            }
        }

        /// <summary>
        /// Setup Unity log filtering to suppress headless mode rendering errors
        /// </summary>
        private void SetupLogFiltering()
        {
            // Note: Unity's Application.logMessageReceived += cannot actually *suppress* logs
            // Log suppression is handled by command-line flags in start_server.bat:
            //   -logFile - -stackTraceLogType None
            // The patches in DedicatedServerPatches.cs prevent the systems from running that cause errors
            
            logger.Msg("Log filtering configured (via startup flags and system patches)");
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
                            _isServerMode = true;
                            _autoStartServer = true;
                            logger.Msg("Dedicated server mode enabled via command line");
                            break;
                    }
                }
                
                // Let ServerConfig handle its own command line arguments
                Shared.Configuration.ServerConfig.ParseCommandLineArgs(args);
            }
            catch (Exception ex)
            {
                logger.Warning($"Error processing command line arguments: {ex.Message}");
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
                        _persistenceManager.OnPlayerJoined(player.DisplayName);
                    };
                    
                    _playerManager.OnPlayerLeft += (player) =>
                    {
                        _persistenceManager.OnPlayerLeft(player.DisplayName);
                    };
                    
                    logger.Msg("Player events wired to persistence system");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error wiring player events: {ex}");
            }
        }

        // Orchestrator handles the entire boot sequence now

        /// <summary>
        /// Shutdown the server gracefully
        /// </summary>
        public static void Shutdown(string reason = "Server shutdown requested")
        {
            if (!_isInitialized)
            {
                logger?.Warning("Server not initialized, cannot shutdown");
                return;
            }

            logger.Msg($"=== Server Shutdown Initiated: {reason} ===");
            
            try
            {
                // Unregister from master server gracefully
                if (_masterServerClient != null && _masterServerClient.IsRegistered)
                {
                    logger.Msg("Unregistering from master server...");
                    var unregisterCoroutine = _masterServerClient.UnregisterFromMasterServer();
                    while (unregisterCoroutine.MoveNext())
                    {
                        System.Threading.Thread.Sleep(100); // Simple wait since we're in shutdown
                    }
                }
                
                // Notify API mods prior to tearing down subsystems
                ModManager.NotifyServerShutdown();
                // Shutdown in reverse order
                try { _tcpConsole?.Dispose(); } catch { }
                _masterServerClient?.Shutdown();
                _gameSystemManager?.Shutdown();
                _persistenceManager?.Shutdown();
                _commandManager?.Shutdown();
                _playerManager?.Shutdown();
                _networkManager?.Shutdown();
                
                _isInitialized = false;
                logger.Msg("=== Server Shutdown Complete ===");
            }
            catch (Exception ex)
            {
                logger.Error($"Error during server shutdown: {ex}");
            }
        }

        private void TryStartTcpConsole()
        {
            try
            {
                var cfg = Shared.Configuration.ServerConfig.Instance;
                if (!cfg.TcpConsoleEnabled)
                {
                    return;
                }

                _tcpConsole = new TcpConsoleServer(
                    cfg.TcpConsoleBindAddress,
                    cfg.TcpConsolePort,
                    cfg.TcpConsoleRequirePassword ? cfg.TcpConsolePassword : null,
                    (line) =>
                    {
                        try
                        {
                            if (string.IsNullOrWhiteSpace(line)) return "";
                            var parts = new List<string>(line.Trim().Split(' '));
                            var cmd = parts[0];
                            parts.RemoveAt(0);
                            var output = new System.Text.StringBuilder();
                            bool ok = _commandManager.ExecuteCommand(
                                cmd,
                                parts,
                                s => output.AppendLine(s),
                                s => output.AppendLine("[WARN] " + s),
                                s => output.AppendLine("[ERR] " + s)
                            );
                            if (!ok)
                            {
                                return $"Unknown or unauthorized command: {cmd}\r\n";
                            }
                            return output.ToString();
                        }
                        catch (Exception ex)
                        {
                            logger.Error($"TCP console command error: {ex}");
                            return $"Error: {ex.Message}\r\n";
                        }
                    },
                    logger
                );
                _tcpConsole.Start();
                logger.Msg($"✓ TCP console listening on {cfg.TcpConsoleBindAddress}:{cfg.TcpConsolePort}");
            }
            catch (Exception ex)
            {
                logger.Warning($"TCP console failed to start: {ex.Message}");
            }
        }

        public static ServerStatus GetStatus()
        {
            if (!_isInitialized)
            {
                return new ServerStatus
                {
                    IsRunning = false,
                    Message = "Server not initialized"
                };
            }

            return new ServerStatus
            {
                IsRunning = _networkManager?.IsServerRunning ?? false,
                PlayerCount = _playerManager?.ConnectedPlayerCount ?? 0,
                MaxPlayers = Shared.Configuration.ServerConfig.Instance.MaxPlayers,
                ServerName = Shared.Configuration.ServerConfig.Instance.ServerName,
                Uptime = _networkManager?.Uptime ?? TimeSpan.Zero,
                Message = "Server operational"
            };
        }

        public static bool IgnoreGhostHostForSleep
        {
            get => Shared.Configuration.ServerConfig.Instance.IgnoreGhostHostForSleep;
            set => Shared.Configuration.ServerConfig.Instance.IgnoreGhostHostForSleep = value;
        }

        public static bool TimeNeverStops
        {
            get => Shared.Configuration.ServerConfig.Instance.TimeNeverStops;
            set => Shared.Configuration.ServerConfig.Instance.TimeNeverStops = value;
        }

        public static bool AutoSaveEnabled
        {
            get => Shared.Configuration.ServerConfig.Instance.AutoSaveEnabled;
            set => Shared.Configuration.ServerConfig.Instance.AutoSaveEnabled = value;
        }

        public static float AutoSaveIntervalMinutes
        {
            get => Shared.Configuration.ServerConfig.Instance.AutoSaveIntervalMinutes;
            set => Shared.Configuration.ServerConfig.Instance.AutoSaveIntervalMinutes = value;
        }

        public static DateTime LastAutoSave => _persistenceManager?.LastAutoSave ?? DateTime.MinValue;
    }

    /// <summary>
    /// Server status information
    /// </summary>
    public class ServerStatus
    {
        public bool IsRunning { get; set; }
        public int PlayerCount { get; set; }
        public int MaxPlayers { get; set; }
        public string ServerName { get; set; }
        public TimeSpan Uptime { get; set; }
        public string Message { get; set; }
        
        public override string ToString()
        {
            return $"Server: {ServerName} | " +
                   $"Status: {(IsRunning ? "Running" : "Stopped")} | " +
                   $"Players: {PlayerCount}/{MaxPlayers} | " +
                   $"Uptime: {Uptime:hh\\:mm\\:ss} | " +
                   $"Message: {Message}";
        }
    }

    /// <summary>
    /// Helper MonoBehaviour to run coroutines in the scene context
    /// </summary>
    public class RoutineRunner : MonoBehaviour { }
}
