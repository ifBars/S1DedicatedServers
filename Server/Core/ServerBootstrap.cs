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
using HarmonyLib;
using DedicatedServerMod.API;
using UnityEngine;
using DedicatedServerMod.Server.TcpConsole;

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
        public static ServerConfig Configuration => ServerConfig.Instance;
        
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
        /// Gets the persistence manager instance
        /// </summary>
        public static PersistenceManager Persistence => _persistenceManager;
        
        /// <summary>
        /// Gets the game system manager instance
        /// </summary>
        public static GameSystemManager GameSystems => _gameSystemManager;

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
            logger.Msg("Initializing server subsystems...");
            
            // Step 1: Initialize existing ServerConfig system (must be first)
            ServerConfig.Initialize(logger);
            logger.Msg("✓ Configuration system initialized");
            
            // Step 2: Parse command line arguments early
            ParseCommandLineArguments();
            
            // Abort startup until the user configures a save path
            if (string.IsNullOrEmpty(ServerConfig.Instance.SaveGamePath))
            {
                logger.Error("Server startup aborted: 'saveGamePath' is not configured in server_config.json.");
                logger.Msg($"Please edit the config and set 'saveGamePath' to your save folder. Make sure to use double backslashes not single, otherwise your server will not load the config.");
                logger.Msg($"Config file location: {ServerConfig.ConfigFilePath}");
                return;
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
            
            // Start TCP Console if enabled in ServerConfig
            TryStartTcpConsole();
            
            // Step 9: Wire up player events with persistence
            WirePlayerEvents();
            
            // Step 10: Auto-start server if requested via command line
            if (_autoStartServer)
            {
                logger.Msg("Auto-starting server due to command line flag (full orchestrated sequence)");
                MelonCoroutines.Start(ServerStartupOrchestrator.StartDedicatedServer());
            }
            
            _isInitialized = true;
            logger.Msg("=== Dedicated Server Bootstrap Complete ===");

            // Notify API mods: server initialized and running
            ModManager.NotifyServerInitialize();

            // Ensure server message forwarding is wired after init
            ModManager.EnsureServerMessageForwarding();
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
                ServerConfig.ParseCommandLineArgs(args);
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
                // Notify API mods prior to tearing down subsystems
                ModManager.NotifyServerShutdown();
                // Shutdown in reverse order
                try { _tcpConsole?.Dispose(); } catch { }
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
                var cfg = ServerConfig.Instance;
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
                MaxPlayers = ServerConfig.Instance.MaxPlayers,
                ServerName = ServerConfig.Instance.ServerName,
                Uptime = _networkManager?.Uptime ?? TimeSpan.Zero,
                Message = "Server operational"
            };
        }

        public static bool IgnoreGhostHostForSleep
        {
            get => ServerConfig.Instance.IgnoreGhostHostForSleep;
            set => ServerConfig.Instance.IgnoreGhostHostForSleep = value;
        }

        public static bool TimeNeverStops
        {
            get => ServerConfig.Instance.TimeNeverStops;
            set => ServerConfig.Instance.TimeNeverStops = value;
        }

        public static bool AutoSaveEnabled
        {
            get => ServerConfig.Instance.AutoSaveEnabled;
            set => ServerConfig.Instance.AutoSaveEnabled = value;
        }

        public static float AutoSaveIntervalMinutes
        {
            get => ServerConfig.Instance.AutoSaveIntervalMinutes;
            set => ServerConfig.Instance.AutoSaveIntervalMinutes = value;
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
}
