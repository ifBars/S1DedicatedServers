using MelonLoader;
using UnityEngine;
using FishNet;
using FishNet.Managing;
using FishNet.Component.Scenes;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using ScheduleOne.Persistence;
using ScheduleOne.DevUtilities;
using ScheduleOne.PlayerScripts;
using ScheduleOne.GameTime;
using System.Reflection;
using HarmonyLib;
using System;
using UnityEngine.SceneManagement;
using System.Collections;
using System.IO;
using ScheduleOne.Intro;
using ScheduleOne.Quests;

[assembly: MelonInfo(typeof(DedicatedServerMod.DedicatedServerHost), "DedicatedServerHost", "1.0.0", "Bars")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace DedicatedServerMod
{
    /// <summary>
    /// Dedicated server host prototype that sets up a FishNet server with Tugboat transport
    /// for clients to connect to. This demonstrates server-side patterns learned from 
    /// </summary>
    public class DedicatedServerHost : MelonMod
    {
        private static MelonLogger.Instance logger;
        private static bool _isServerMode = false;
        private static bool _serverStarted = false;
        private static int _serverPort = 38465;
        private static string _saveGamePath = "";
        private static bool _autoStartServer = false;
        private static bool _debugMode = false;
        private static bool _loopbackPlayerHandled = false;
        private static bool _ignoreGhostHostForSleep = true; // Configurable option for server owners
        private static bool _timeNeverStops = true; // Configurable option to prevent 4 AM time freeze
        private static bool _autoSaveEnabled = true; // Enable/disable auto-save functionality
        private static float _autoSaveIntervalMinutes = 10f; // Auto-save interval in minutes
        private static bool _autoSaveOnPlayerJoin = true; // Save when a new player joins
        private static bool _autoSaveOnPlayerLeave = true; // Save when a player leaves
        private static DateTime _lastAutoSave = DateTime.MinValue; // Track last auto-save time

        /// <summary>
        /// Gets or sets whether to ignore the ghost host when checking if all players are ready to sleep.
        /// This allows sleep cycling to work properly on dedicated servers.
        /// </summary>
        public static bool IgnoreGhostHostForSleep
        {
            get => _ignoreGhostHostForSleep;
            set
            {
                _ignoreGhostHostForSleep = value;
                logger?.Msg($"Ignore ghost host for sleep set to: {_ignoreGhostHostForSleep}");
            }
        }

        /// <summary>
        /// Gets or sets whether time never stops at 4 AM on the dedicated server.
        /// When enabled, time will continue past 4 AM without requiring players to sleep.
        /// </summary>
        public static bool TimeNeverStops
        {
            get => _timeNeverStops;
            set
            {
                _timeNeverStops = value;
                logger?.Msg($"Time never stops set to: {_timeNeverStops}");
            }
        }

        /// <summary>
        /// Gets or sets whether auto-save is enabled on the dedicated server.
        /// </summary>
        public static bool AutoSaveEnabled
        {
            get => _autoSaveEnabled;
            set
            {
                _autoSaveEnabled = value;
                logger?.Msg($"Auto-save enabled set to: {_autoSaveEnabled}");
            }
        }

        /// <summary>
        /// Gets or sets the auto-save interval in minutes.
        /// </summary>
        public static float AutoSaveIntervalMinutes
        {
            get => _autoSaveIntervalMinutes;
            set
            {
                if (value > 0)
                {
                    _autoSaveIntervalMinutes = value;
                    logger?.Msg($"Auto-save interval set to: {_autoSaveIntervalMinutes} minutes");
                }
                else
                {
                    logger?.Warning("Auto-save interval must be greater than 0");
                }
            }
        }

        /// <summary>
        /// Gets or sets whether to auto-save when a player joins.
        /// </summary>
        public static bool AutoSaveOnPlayerJoin
        {
            get => _autoSaveOnPlayerJoin;
            set
            {
                _autoSaveOnPlayerJoin = value;
                logger?.Msg($"Auto-save on player join set to: {_autoSaveOnPlayerJoin}");
            }
        }

        /// <summary>
        /// Gets or sets whether to auto-save when a player leaves.
        /// </summary>
        public static bool AutoSaveOnPlayerLeave
        {
            get => _autoSaveOnPlayerLeave;
            set
            {
                _autoSaveOnPlayerLeave = value;
                logger?.Msg($"Auto-save on player leave set to: {_autoSaveOnPlayerLeave}");
            }
        }

        /// <summary>
        /// Gets the time of the last auto-save.
        /// </summary>
        public static DateTime LastAutoSave => _lastAutoSave;

        /// <summary>
        /// Manually triggers an auto-save (if conditions are met).
        /// </summary>
        public static void ManualSave()
        {
            if (_isServerMode && InstanceFinder.IsServer)
            {
                TriggerAutoSave("manual_request");
            }
            else
            {
                logger?.Warning("Manual save can only be called on a dedicated server");
            }
        }

        public override void OnInitializeMelon()
        {
            logger = LoggerInstance;
            logger.Msg("DedicatedServerHost initialized");
            
            // Parse command line arguments
            ParseCommandLineArgs();
            
            // Apply server patches using MelonLoader's Harmony
            ApplyServerPatches();
            
            // Auto-start server if requested via command line
            if (_autoStartServer)
            {
                logger.Msg("Auto-starting server due to command line flag");
                MelonCoroutines.Start(DelayedServerStart());
            }
        }

        private void ParseCommandLineArgs()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--dedicated-server":
                        _isServerMode = true;
                        _autoStartServer = true;
                        logger.Msg("Dedicated server mode enabled");
                        break;
                    case "--server-port":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int port))
                        {
                            _serverPort = port;
                            logger.Msg($"Server port set to: {_serverPort}");
                        }
                        break;
                    case "--save-path":
                        if (i + 1 < args.Length)
                        {
                            _saveGamePath = args[i + 1];
                            logger.Msg($"Save game path set to: {_saveGamePath}");
                        }
                        break;
                    case "--debug-server":
                        _debugMode = true;
                        logger.Msg("Server debug mode enabled");
                        break;
                    case "--ignore-ghost-sleep":
                        if (i + 1 < args.Length && bool.TryParse(args[i + 1], out bool ignoreGhost))
                        {
                            _ignoreGhostHostForSleep = ignoreGhost;
                            logger.Msg($"Ignore ghost host for sleep set to: {_ignoreGhostHostForSleep}");
                        }
                        break;
                    case "--time-never-stops":
                        if (i + 1 < args.Length && bool.TryParse(args[i + 1], out bool timeNeverStops))
                        {
                            _timeNeverStops = timeNeverStops;
                            logger.Msg($"Time never stops set to: {_timeNeverStops}");
                        }
                        break;
                    case "--auto-save":
                        if (i + 1 < args.Length && bool.TryParse(args[i + 1], out bool autoSaveEnabled))
                        {
                            _autoSaveEnabled = autoSaveEnabled;
                            logger.Msg($"Auto-save enabled set to: {_autoSaveEnabled}");
                        }
                        break;
                    case "--auto-save-interval":
                        if (i + 1 < args.Length && float.TryParse(args[i + 1], out float autoSaveInterval))
                        {
                            _autoSaveIntervalMinutes = autoSaveInterval;
                            logger.Msg($"Auto-save interval set to: {_autoSaveIntervalMinutes} minutes");
                        }
                        break;
                    case "--auto-save-on-join":
                        if (i + 1 < args.Length && bool.TryParse(args[i + 1], out bool autoSaveOnJoin))
                        {
                            _autoSaveOnPlayerJoin = autoSaveOnJoin;
                            logger.Msg($"Auto-save on player join set to: {_autoSaveOnPlayerJoin}");
                        }
                        break;
                    case "--auto-save-on-leave":
                        if (i + 1 < args.Length && bool.TryParse(args[i + 1], out bool autoSaveOnLeave))
                        {
                            _autoSaveOnPlayerLeave = autoSaveOnLeave;
                            logger.Msg($"Auto-save on player leave set to: {_autoSaveOnPlayerLeave}");
                        }
                        break;
                }
            }
        }

        private void ApplyServerPatches()
        {
            try
            {
                var harmony = HarmonyInstance;
                
                // Patch Multipass.Initialize to add Tugboat transport
                var multipassType = typeof(Multipass);
                var initializeMethod = multipassType.GetMethod("Initialize");
                
                if (initializeMethod != null)
                {
                    var prefixMethod = typeof(DedicatedServerHost).GetMethod(nameof(MultipassInitializePrefix), 
                        BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(initializeMethod, new HarmonyMethod(prefixMethod));
                    logger.Msg("Patched Multipass.Initialize for server");
                }

                // Patch LoadManager to intercept save loading for server
                var loadManagerType = typeof(LoadManager);
                var startGameMethod = loadManagerType.GetMethod("StartGame", 
                    BindingFlags.Public | BindingFlags.Instance);
                
                if (startGameMethod != null)
                {
                    var prefixMethod = typeof(DedicatedServerHost).GetMethod(nameof(StartGamePrefix), 
                        BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(startGameMethod, new HarmonyMethod(prefixMethod));
                    logger.Msg("Patched LoadManager.StartGame for dedicated server mode");
                }

                // Patch Player spawning to ensure proper quest initialization for new clients
                var playerType = typeof(Player);
                var playerInitMethod = playerType.GetMethod("MarkPlayerInitialized", BindingFlags.Public | BindingFlags.Instance);
                if (playerInitMethod != null)
                {
                    var postfixMethod = typeof(DedicatedServerHost).GetMethod(nameof(PlayerInitializedPostfix), 
                        BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(playerInitMethod, postfix: new HarmonyMethod(postfixMethod));
                    logger.Msg("Patched Player.MarkPlayerInitialized for quest initialization");
                }

                // Patch AreAllPlayersReadyToSleep to ignore ghost host for sleep cycling
                var areAllPlayersReadyMethod = playerType.GetMethod("AreAllPlayersReadyToSleep", 
                    BindingFlags.Public | BindingFlags.Static);
                if (areAllPlayersReadyMethod != null)
                {
                    var prefixMethod = typeof(DedicatedServerHost).GetMethod(nameof(AreAllPlayersReadyToSleepPrefix), 
                        BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(areAllPlayersReadyMethod, new HarmonyMethod(prefixMethod));
                    logger.Msg("Patched Player.AreAllPlayersReadyToSleep to ignore ghost host");
                }

                // Patch TimeManager.Tick to prevent time from stopping at 4 AM (inspired by TimeNeverStopsMod)
                var timeManagerType = typeof(ScheduleOne.GameTime.TimeManager);
                var tickMethod = timeManagerType.GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance);
                if (tickMethod != null)
                {
                    var prefixMethod = typeof(DedicatedServerHost).GetMethod(nameof(TimeManagerTickPrefix), 
                        BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(tickMethod, new HarmonyMethod(prefixMethod));
                    logger.Msg("Patched TimeManager.Tick to prevent time stopping at 4 AM on dedicated servers");
                }

                // Patch Player OnDestroy to handle player disconnect events for auto-save
                var playerOnDestroyMethod = playerType.GetMethod("OnDestroy", BindingFlags.NonPublic | BindingFlags.Instance);
                if (playerOnDestroyMethod != null)
                {
                    var prefixMethod = typeof(DedicatedServerHost).GetMethod(nameof(PlayerOnDestroyPrefix), 
                        BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(playerOnDestroyMethod, new HarmonyMethod(prefixMethod));
                    logger.Msg("Patched Player.OnDestroy for player disconnect auto-save");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to apply server patches: {ex}");
            }
        }

        private static void MultipassInitializePrefix(Multipass __instance)
        {
            try
            {
                // Add Tugboat component if not present (same pattern as client mod)
                var tugboat = __instance.gameObject.GetComponent<Tugboat>();
                if (tugboat == null)
                {
                    tugboat = __instance.gameObject.AddComponent<Tugboat>();
                    logger.Msg("Added Tugboat component to Multipass for server");
                    
                    // Add to transports list using reflection
                    var transportsField = typeof(Multipass).GetField("_transports", 
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (transportsField != null)
                    {
                        var transports = transportsField.GetValue(__instance) as List<Transport>;
                        if (transports != null)
                        {
                            transports.Add(tugboat);
                            logger.Msg("Added Tugboat to server transports list");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error in Multipass Initialize patch: {ex}");
            }
        }

        private static bool StartGamePrefix(LoadManager __instance, SaveInfo info, bool allowLoadStacking = false)
        {
            if (!_isServerMode)
            {
                return true; // Execute original method for normal gameplay
            }

            logger.Msg($"Intercepting StartGame for dedicated server mode");
            
            // Start our custom server loading process with the SaveInfo
            MelonCoroutines.Start(LoadAsDedicatedServer(__instance, info));
            
            return false; // Skip original method
        }

        private static IEnumerator LoadAsDedicatedServer(LoadManager loadManager, SaveInfo saveInfo)
        {
            logger.Msg("Starting dedicated server loading sequence");
            
            // Step 1: Setup server transport (Tugboat instead of Steam)
            logger.Msg("Step 1: Setting up Tugboat transport for server");
            
            var networkManager = InstanceFinder.NetworkManager;
            if (networkManager == null)
            {
                logger.Error("NetworkManager not found");
                yield break;
            }

            var transportManager = networkManager.TransportManager;
            var transport = transportManager.Transport;
            var multipass = transport as Multipass;

            if (multipass == null)
            {
                logger.Error("Multipass transport not found");
                yield break;
            }

            var tugboat = multipass.gameObject.GetComponent<Tugboat>();
            if (tugboat == null)
            {
                logger.Error("Tugboat component not found - should have been added in Initialize patch");
                yield break;
            }

            // Configure Tugboat for server
            tugboat.SetPort((ushort)_serverPort);
            
            // Set as server transport using reflection
            SetServerTransport(multipass, tugboat);
            
            logger.Msg($"Tugboat transport configured for server on port {_serverPort}");

            // Step 2: Prepare save info (mirror StartGame method)
            logger.Msg("Step 2: Preparing save info");
            
            SaveInfo actualSaveInfo = saveInfo;
            if (actualSaveInfo == null)
            {
                // Find the most recent save game and create SaveInfo
                string savePath = !string.IsNullOrEmpty(_saveGamePath) ? _saveGamePath : FindMostRecentSave();
                if (string.IsNullOrEmpty(savePath))
                {
                    logger.Error("No save game found to load");
                    yield break;
                }
                
                // Try to load save info from the folder
                if (!LoadManager.TryLoadSaveInfo(savePath, 0, out actualSaveInfo))
                {
                    logger.Error($"Failed to load save info from: {savePath}");
                    yield break;
                }
            }

            logger.Msg($"Loading save game: {actualSaveInfo.SavePath} ({actualSaveInfo.OrganisationName})");
            
            // Set up LoadManager state like StartGame does
            loadManager.ActiveSaveInfo = actualSaveInfo;
            loadManager.IsLoading = true;
            loadManager.TimeSinceGameLoaded = 0f;
            loadManager.LoadedGameFolderPath = actualSaveInfo.SavePath;
            LoadManager.LoadHistory.Add("Loading game: " + actualSaveInfo.OrganisationName);

            // Stop any existing network connections (mirror game's LoadRoutine order)
            if (InstanceFinder.IsServer)
            {
                InstanceFinder.NetworkManager.ServerManager.StopConnection(false);
            }
            if (InstanceFinder.IsClient)
            {
                InstanceFinder.NetworkManager.ClientManager.StopConnection();
            }

            // Invoke pre-scene change hooks and perform cleanup (mirror game's flow)
            if (loadManager.onPreSceneChange != null)
            {
                loadManager.onPreSceneChange.Invoke();
            }
            var cleanUpMethod = typeof(LoadManager).GetMethod("CleanUp", BindingFlags.NonPublic | BindingFlags.Instance);
            cleanUpMethod?.Invoke(loadManager, null);

            // Ensure FishNet online scene is set to Main for client scene sync
            var defaultScene = networkManager.gameObject.GetComponent<DefaultScene>();
            defaultScene?.SetOnlineScene("Main");

            // Step 3: Load scene and initialize
            logger.Msg("Step 3: Loading Main scene");
            loadManager.LoadStatus = LoadManager.ELoadStatus.LoadingScene;

            // Load the main scene
            var sceneLoad = SceneManager.LoadSceneAsync("Main");
            while (!sceneLoad.isDone)
            {
                yield return new WaitForEndOfFrame();
            }

            logger.Msg("Main scene loaded");
            loadManager.LoadStatus = LoadManager.ELoadStatus.Initializing;

            // Step 4: Start FishNet server
            logger.Msg("Step 4: Starting FishNet server");
            
            bool serverStarted = tugboat.StartConnection(true); // true = server mode
            if (!serverStarted)
            {
                logger.Error("Failed to start Tugboat server");
                yield break;
            }

            // Wait for server to initialize
            float timeout = 10f;
            float elapsed = 0f;
            while (!InstanceFinder.IsServer && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (!InstanceFinder.IsServer)
            {
                logger.Error("Server failed to start within timeout");
                yield break;
            }

            logger.Msg("FishNet server started successfully");

            // Step 5: Start loopback client on server to mirror host flow
            logger.Msg("Step 5: Starting loopback client on server (mirror host flow)");
            SetClientTransport(multipass, tugboat);
            tugboat.SetClientAddress("127.0.0.1");
            bool clientStarted = tugboat.StartConnection(false);
            if (!clientStarted)
            {
                logger.Error("Failed to start loopback client");
                yield break;
            }

            // Wait for client to initialize
            float clientTimeout = 10f;
            float clientElapsed = 0f;
            while (!networkManager.IsClient && clientElapsed < clientTimeout)
            {
                yield return new WaitForSeconds(0.1f);
                clientElapsed += 0.1f;
            }
            if (!networkManager.IsClient)
            {
                logger.Warning("Loopback client did not initialize within timeout");
            }

            // Ensure loopback client's Player (when it spawns) is hidden/ignored for all clients
            TryHandleExistingLoopbackPlayer();
            Player.onPlayerSpawned = (Action<Player>)Delegate.Remove(Player.onPlayerSpawned, new Action<Player>(OnPlayerSpawned_LoopbackHandler));
            Player.onPlayerSpawned = (Action<Player>)Delegate.Combine(Player.onPlayerSpawned, new Action<Player>(OnPlayerSpawned_LoopbackHandler));
            
            // Subscribe to player events for auto-save
            if (_autoSaveOnPlayerJoin)
            {
                Player.onPlayerSpawned = (Action<Player>)Delegate.Remove(Player.onPlayerSpawned, new Action<Player>(OnPlayerJoined));
                Player.onPlayerSpawned = (Action<Player>)Delegate.Combine(Player.onPlayerSpawned, new Action<Player>(OnPlayerJoined));
                logger.Msg("Subscribed to player join events for auto-save");
            }

            // Step 6: Load save data
            logger.Msg("Step 6: Loading save data");
            loadManager.LoadStatus = LoadManager.ELoadStatus.LoadingData;
            yield return MelonCoroutines.Start(LoadSaveData(loadManager));

            // Step 7: Complete server initialization
            logger.Msg("Step 7: Completing server initialization");
            
            loadManager.LoadStatus = LoadManager.ELoadStatus.None;
            loadManager.IsLoading = false;
            loadManager.IsGameLoaded = true;
            _serverStarted = true;

            // Ensure time progression is active
            if (NetworkSingleton<TimeManager>.Instance != null)
            {
                NetworkSingleton<TimeManager>.Instance.TimeProgressionMultiplier = 1f;
            }
            
            ServerManager.Initialize();

            // Start auto-save system if enabled
            if (_autoSaveEnabled)
            {
                MelonCoroutines.Start(AutoSaveCoroutine());
                logger.Msg($"Auto-save system started with {_autoSaveIntervalMinutes} minute intervals");
            }

            logger.Msg("=== DEDICATED SERVER READY ===");
            logger.Msg($"Server running on port {_serverPort}");
            logger.Msg($"Loaded save: {Path.GetFileName(actualSaveInfo.SavePath)}");
            logger.Msg("Waiting for client connections...");
        }

        /// <summary>
        /// Auto-save coroutine that runs periodically to save game data
        /// </summary>
        private static IEnumerator AutoSaveCoroutine()
        {
            logger.Msg("Auto-save coroutine started");
            
            while (_serverStarted && _autoSaveEnabled)
            {
                // Wait for the specified interval
                yield return new WaitForSeconds(_autoSaveIntervalMinutes * 60f);
                
                // Only save if server is still running and game is loaded
                if (_serverStarted && InstanceFinder.IsServer && Singleton<LoadManager>.Instance.IsGameLoaded)
                {
                    TriggerAutoSave("interval");
                }
            }
            
            logger.Msg("Auto-save coroutine ended");
        }

        /// <summary>
        /// Triggers an auto-save with the specified reason
        /// </summary>
        private static void TriggerAutoSave(string reason)
        {
            try
            {
                var saveManager = Singleton<SaveManager>.Instance;
                if (saveManager != null && !saveManager.IsSaving)
                {
                    var timeSinceLastSave = DateTime.Now - _lastAutoSave;
                    logger.Msg($"Auto-save triggered by: {reason} (last save: {timeSinceLastSave.TotalMinutes:F1} minutes ago)");
                    
                    saveManager.Save();
                    _lastAutoSave = DateTime.Now;
                    
                    logger.Msg("Auto-save completed successfully");
                }
                else if (saveManager?.IsSaving == true)
                {
                    logger.Msg($"Auto-save skipped - save already in progress (triggered by: {reason})");
                }
                else
                {
                    logger.Warning($"Auto-save failed - SaveManager not available (triggered by: {reason})");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Auto-save error (triggered by: {reason}): {ex}");
            }
        }

        /// <summary>
        /// Handles player joining events for auto-save
        /// </summary>
        private static void OnPlayerJoined(Player player)
        {
            if (!_isServerMode || !InstanceFinder.IsServer || !_autoSaveOnPlayerJoin)
                return;

            // Don't trigger save for the loopback player
            if (player.gameObject.name == "[DedicatedServerHostLoopback]")
                return;

            logger.Msg($"Player joined: {player.PlayerName} - triggering auto-save");
            MelonCoroutines.Start(DelayedPlayerJoinSave(player.PlayerName));
        }

        /// <summary>
        /// Handles player leaving events for auto-save
        /// </summary>
        private static void OnPlayerLeft(Player player)
        {
            if (!_isServerMode || !InstanceFinder.IsServer || !_autoSaveOnPlayerLeave)
                return;

            // Don't trigger save for the loopback player
            if (player.gameObject.name == "[DedicatedServerHostLoopback]")
                return;

            logger.Msg($"Player left: {player.PlayerName} - triggering auto-save");
            TriggerAutoSave($"player_leave_{player.PlayerName}");
        }

        /// <summary>
        /// Delayed save after player joins to ensure all data is synchronized
        /// </summary>
        private static IEnumerator DelayedPlayerJoinSave(string playerName)
        {
            // Wait a few seconds for the player to fully initialize
            yield return new WaitForSeconds(5f);
            TriggerAutoSave($"player_join_{playerName}");
        }

        private static void OnPlayerSpawned_LoopbackHandler(Player p)
        {
            if (_loopbackPlayerHandled)
                return;
            try
            {
                if (p != null && p.Owner != null && p.Owner.IsLocalClient)
                {
                    // Trigger intro completion events for quest initialization
                    /*
                    var introManager = Singleton<IntroManager>.Instance;
                    if (introManager != null)
                    {
                        logger.Msg("Triggering intro completion events for dedicated server quest initialization");
                        if (introManager.onIntroDoneAsServer != null)
                            introManager.onIntroDoneAsServer.Invoke();
                        if (introManager.onIntroDone != null)
                            introManager.onIntroDone.Invoke();
                    }
                    */
                                        
                    // Initialize default quests for the server
                    MelonCoroutines.Start(InitializeServerQuests());
                    
                    // Rename and hide the loopback player's visuals everywhere
                    p.gameObject.name = "[DedicatedServerHostLoopback]";
                    p.SetVisible(false, network: true);
                    p.SetVisibleToLocalPlayer(false);
                    
                    // Move the ghost player far away from the game world to prevent quest interference
                    // Use the game's proper teleport system to avoid issues
                    var playerMovement = p.GetComponent<PlayerMovement>();
                    if (playerMovement != null)
                    {
                        playerMovement.Teleport(new Vector3(16.456f, 31.176f, -165.366f));
                        logger.Msg("Teleported loopback player far from game world to prevent quest interference");
                    }
                    else
                    {
                        // Fallback to direct transform if PlayerMovement not available
                        p.transform.position = new Vector3(16.456f, 31.176f, -165.366f);
                        logger.Msg("Moved loopback player far from game world (fallback method)");
                    }

                    _loopbackPlayerHandled = true;
                    logger.Msg("Loopback client player hidden and renamed for dedicated server.");
                    Player.onPlayerSpawned = (Action<Player>)Delegate.Remove(Player.onPlayerSpawned, new Action<Player>(OnPlayerSpawned_LoopbackHandler));
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error handling loopback player on spawn: {ex}");
            }
        }

        private static void TryHandleExistingLoopbackPlayer()
        {
            if (_loopbackPlayerHandled)
                return;
            try
            {
                foreach (var p in Player.PlayerList)
                {
                    if (p == null || p.Owner == null || !p.Owner.IsLocalClient) continue;
                    // Trigger intro completion events for quest initialization
                    /*
                    var introManager = Singleton<IntroManager>.Instance;
                    if (introManager != null)
                    {
                        logger.Msg("Triggering intro completion events for dedicated server quest initialization (existing player)");
                        if (introManager.onIntroDoneAsServer != null)
                            introManager.onIntroDoneAsServer.Invoke();
                        if (introManager.onIntroDone != null)
                            introManager.onIntroDone.Invoke();
                    }
                    */

                    // Initialize default quests for the server
                    MelonCoroutines.Start(InitializeServerQuests());
                    
                    p.gameObject.name = "[DedicatedServerHostLoopback]";
                    p.SetVisible(false, network: true);
                    p.SetVisibleToLocalPlayer(false);
                    
                    // Move the ghost player far away from the game world to prevent quest interference
                    // Use the game's proper teleport system to avoid issues
                    var playerMovement = p.GetComponent<PlayerMovement>();
                    if (playerMovement != null)
                    {
                        playerMovement.Teleport(new Vector3(16.456f, 31.176f, -165.366f));
                        logger.Msg("Teleported existing loopback player far from game world to prevent quest interference");
                    }
                    else
                    {
                        // Fallback to direct transform if PlayerMovement not available
                        p.transform.position = new Vector3(16.456f, 31.176f, 165.366f);
                        logger.Msg("Moved existing loopback player far from game world (fallback method)");
                    }

                    _loopbackPlayerHandled = true;
                    logger.Msg("Loopback client player (existing) hidden and renamed for dedicated server.");
                    Player.onPlayerSpawned = (Action<Player>)Delegate.Remove(Player.onPlayerSpawned, new Action<Player>(OnPlayerSpawned_LoopbackHandler));
                    break;
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error checking existing loopback player: {ex}");
            }
        }

        private static void SetServerTransport(Multipass multipass, Transport transport)
        {
            try
            {
                // Use reflection to set server transport (similar to client approach)
                multipass.SetClientTransport(transport);
            }
            catch (Exception ex)
            {
                logger.Error($"Error setting server transport: {ex}");
            }
        }

        private static void SetClientTransport(Multipass multipass, Transport transport)
        {
            // Use reflection to set client transport (mirror client approach)
            var clientTransportField = typeof(Multipass).GetField("_clientTransport",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (clientTransportField != null)
            {
                clientTransportField.SetValue(multipass, transport);
                logger.Msg("Successfully set Tugboat as client transport (loopback)");
            }
            else
            {
                logger.Warning("Could not find _clientTransport field");
            }
        }

        private static string FindMostRecentSave()
        {
            try
            {
                string savesDir = Path.Combine(Application.persistentDataPath, "saves");
                if (!Directory.Exists(savesDir))
                {
                    return null;
                }

                var saveDirectories = Directory.GetDirectories(savesDir);
                if (saveDirectories.Length == 0)
                {
                    return null;
                }

                // Find the most recently modified save
                string mostRecentSave = null;
                DateTime mostRecentTime = DateTime.MinValue;

                foreach (string saveDir in saveDirectories)
                {
                    var info = new DirectoryInfo(saveDir);
                    if (info.LastWriteTime > mostRecentTime)
                    {
                        mostRecentTime = info.LastWriteTime;
                        mostRecentSave = saveDir;
                    }
                }

                return mostRecentSave;
            }
            catch (Exception ex)
            {
                logger.Error($"Error finding most recent save: {ex}");
                return null;
            }
        }

        private static IEnumerator LoadSaveData(LoadManager loadManager)
        {
            // Invoke the LoadManager's save loading logic (mirror the StartGame->LoadRoutine flow)
            if (loadManager.onPreLoad != null)
            {
                loadManager.onPreLoad.Invoke();
            }

            // Directly create load requests like the game's Load() nested method does
            logger.Msg("Creating load requests for save data");
            
            foreach (var baseSaveable in Singleton<SaveManager>.Instance.BaseSaveables)
            {
                string loadPath = Path.Combine(loadManager.LoadedGameFolderPath, baseSaveable.SaveFolderName);
                logger.Msg($"Creating load request for: {baseSaveable.SaveFolderName} at {loadPath}");
                new LoadRequest(loadPath, baseSaveable.Loader);
            }

            // Wait for all load requests to complete (mirror Load() logic)
            var loadRequestsField = typeof(LoadManager).GetField("loadRequests", BindingFlags.NonPublic | BindingFlags.Instance);
            var loadRequests = loadRequestsField?.GetValue(loadManager) as List<LoadRequest>;
            
            if (loadRequests != null)
            {
                while (loadRequests.Count > 0)
                {
                    // Process up to 50 requests per frame (same as game)
                    for (int i = 0; i < 50; i++)
                    {
                        if (loadRequests.Count <= 0)
                            break;
                            
                        var loadRequest = loadRequests[0];
                        try
                        {
                            loadRequest.Complete();
                        }
                        catch (Exception ex)
                        {
                            logger.Error($"LOAD ERROR for load request: {loadRequest.Path} : {ex.Message}");
                            if (loadRequests.Count > 0 && loadRequests[0] == loadRequest)
                            {
                                loadRequests.RemoveAt(0);
                            }
                        }
                    }
                    yield return new WaitForEndOfFrame();
                }
            }

            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            if (loadManager.onLoadComplete != null)
            {
                loadManager.onLoadComplete.Invoke();
            }

            logger.Msg("Save data loaded successfully");
        }

        private static IEnumerator DelayedServerStart()
        {
            // Wait for game to initialize
            yield return new WaitForSeconds(2f);
            
            logger.Msg("Auto-starting dedicated server...");
            StartDedicatedServer();
        }

        public static void StartDedicatedServer()
        {
            if (_serverStarted)
            {
                logger.Warning("Server already started");
                return;
            }

            logger.Msg("Starting dedicated server manually");
            
            var loadManager = Singleton<LoadManager>.Instance;
            if (loadManager != null)
            {
                string savePath = _saveGamePath;
                if (string.IsNullOrEmpty(savePath))
                {
                    savePath = FindMostRecentSave();
                }
                
                if (!string.IsNullOrEmpty(savePath))
                {
                    logger.Msg($"Loading save: {savePath}");
                    // Create a SaveInfo object to pass to StartGame
                    var saveInfo = new SaveInfo(savePath, 0, "Dedicated Server", DateTime.Now, DateTime.Now, 0f, Application.version, null);
                    loadManager.StartGame(saveInfo);
                }
                else
                {
                    logger.Error("No save game found to load");
                }
            }
            else
            {
                logger.Error("LoadManager not found");
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (_debugMode)
            {
                logger.Msg($"Scene loaded: {sceneName} (index: {buildIndex})");
            }
        }

        private static void PlayerInitializedPostfix(Player __instance)
        {
            if (!_isServerMode || !InstanceFinder.IsServer)
                return;

            try
            {
                // Don't handle the loopback player
                if (__instance.Owner != null && __instance.Owner.IsLocalClient)
                    return;

                logger.Msg($"Player initialized on dedicated server: {__instance.PlayerName} (ClientId: {__instance.Owner?.ClientId})");
                
                // Start coroutine to ensure quest initialization for this new player
                MelonCoroutines.Start(EnsureQuestInitializationForNewClient(__instance));
            }
            catch (Exception ex)
            {
                logger.Error($"Error in PlayerInitialized postfix: {ex}");
            }
        }

        private static bool AreAllPlayersReadyToSleepPrefix(ref bool __result)
        {
            // Only apply our custom logic if we're in server mode and the feature is enabled
            if (!_isServerMode || !InstanceFinder.IsServer || !_ignoreGhostHostForSleep)
            {
                return true; // Let the original method run
            }

            try
            {
                // Replicate the original logic but exclude the ghost loopback player
                var playerList = Player.PlayerList;
                if (playerList.Count == 0)
                {
                    if (_debugMode)
                        logger.Msg("Sleep check: No players in list");
                    __result = false;
                    return false; // Skip original method
                }

                int realPlayerCount = 0;
                int readyPlayerCount = 0;

                for (int i = 0; i < playerList.Count; i++)
                {
                    var player = playerList[i];
                    if (player == null) continue;

                    // Skip the ghost loopback player (identified by name)
                    if (player.gameObject.name == "[DedicatedServerHostLoopback]")
                    {
                        if (_debugMode)
                            logger.Msg($"Sleep check: Ignoring ghost host player: {player.PlayerName}");
                        continue;
                    }

                    realPlayerCount++;
                    
                    // Check if this non-ghost player is ready to sleep
                    if (player.IsReadyToSleep)
                    {
                        readyPlayerCount++;
                        if (_debugMode)
                            logger.Msg($"Sleep check: Player {player.PlayerName} is ready to sleep");
                    }
                    else
                    {
                        if (_debugMode)
                            logger.Msg($"Sleep check: Player {player.PlayerName} is NOT ready to sleep");
                        __result = false;
                        return false; // Skip original method
                    }
                }

                if (_debugMode)
                    logger.Msg($"Sleep check: All {realPlayerCount} real players are ready to sleep ({readyPlayerCount}/{realPlayerCount})");

                __result = true;
                return false; // Skip original method
            }
            catch (Exception ex)
            {
                logger.Error($"Error in AreAllPlayersReadyToSleep patch: {ex}");
                return true; // Let original method run as fallback
            }
        }

        /// <summary>
        /// Harmony prefix patch for TimeManager.Tick to prevent time from stopping at 4 AM
        /// Inspired by TimeNeverStopsMod - implements continuous time progression on dedicated servers
        /// </summary>
        private static bool TimeManagerTickPrefix(ScheduleOne.GameTime.TimeManager __instance)
        {
            // Only apply this patch in server mode and if time never stops is enabled
            if (!_isServerMode || !InstanceFinder.IsServer || !_timeNeverStops)
            {
                return true; // Let original method run normally
            }

            try
            {
                // Check if time would normally freeze (4 AM or 4-6 AM range)
                bool wouldFreeze = (__instance.CurrentTime == 400) || 
                                   (__instance.IsCurrentTimeWithinRange(400, 600) && !GameManager.IS_TUTORIAL);

                if (!wouldFreeze)
                {
                    return true; // Let normal tick happen - no freeze would occur
                }

                // Custom time progression logic to bypass the 4 AM freeze
                // This replicates the normal Tick logic but skips the freeze check
                
                if (Player.Local == null)
                {
                    logger.Warning("Local player does not exist. Waiting for player to spawn.");
                    return false; // Skip original method
                }

                __instance.TimeOnCurrentMinute = 0f;
                
                try
                {
                    // Trigger minute pass events using reflection since StaggeredMinPass is private
                    var staggeredMinPassMethod = typeof(ScheduleOne.GameTime.TimeManager).GetMethod("StaggeredMinPass", 
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (staggeredMinPassMethod != null)
                    {
                        var coroutine = staggeredMinPassMethod.Invoke(__instance, new object[] { 
                            1f / (__instance.TimeProgressionMultiplier * Time.timeScale) 
                        });
                        __instance.StartCoroutine((IEnumerator)coroutine);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error($"Error invoking onMinutePass: {ex}");
                }

                // Time advancement logic (same as original Tick)
                if (__instance.CurrentTime == 2359)
                {
                    __instance.ElapsedDays++;
                    __instance.CurrentTime = 0;
                    __instance.DailyMinTotal = 0;
                    __instance.onDayPass?.Invoke();
                    __instance.onHourPass?.Invoke();
                    if (__instance.CurrentDay == EDay.Monday && __instance.onWeekPass != null)
                    {
                        __instance.onWeekPass();
                    }
                }
                else if (__instance.CurrentTime % 100 >= 59)
                {
                    __instance.CurrentTime += 41;
                    __instance.onHourPass?.Invoke();
                }
                else
                {
                    __instance.CurrentTime++;
                }

                __instance.DailyMinTotal = TimeManager.GetMinSumFrom24HourTime(__instance.CurrentTime);
                __instance.HasChanged = true;
                
                // Handle first night event
                if (__instance.ElapsedDays == 0 && __instance.CurrentTime == 2000 && __instance.onFirstNight != null)
                {
                    __instance.onFirstNight.Invoke();
                }

                if (_debugMode)
                {
                    logger.Msg($"Dedicated server: Advanced time past 4 AM freeze - now {__instance.CurrentTime}");
                }

                return false; // Skip original method - we've handled the tick
            }
            catch (Exception ex)
            {
                logger.Error($"Error in TimeManager Tick patch: {ex}");
                return true; // Let original method run as fallback
            }
        }

        /// <summary>
        /// Harmony prefix patch for Player.OnDestroy to handle player disconnect events
        /// </summary>
        private static void PlayerOnDestroyPrefix(Player __instance)
        {
            if (!_isServerMode || !InstanceFinder.IsServer || !_autoSaveOnPlayerLeave)
                return;

            try
            {
                // Don't trigger save for the loopback player
                if (__instance.gameObject.name == "[DedicatedServerHostLoopback]")
                    return;

                // Check if this is a real disconnect (not just a scene change)
                if (__instance.Owner != null && !__instance.Owner.IsLocalClient)
                {
                    logger.Msg($"Player disconnecting: {__instance.PlayerName} - triggering auto-save");
                    TriggerAutoSave($"player_disconnect_{__instance.PlayerName}");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error in Player OnDestroy patch: {ex}");
            }
        }

        private static IEnumerator InitializeServerQuests()
        {
            // Wait for quest system to be ready
            yield return new WaitForSeconds(2f);
            
            try
            {
                logger.Msg("Initializing server-side quest system");
                
                var questManager = NetworkSingleton<QuestManager>.Instance;
                if (questManager != null)
                {
                    logger.Msg("QuestManager found - ensuring default quests are initialized");
                    
                    if (questManager.DefaultQuests != null && questManager.DefaultQuests.Length > 0)
                    {
                        logger.Msg($"Found {questManager.DefaultQuests.Length} default quests");
                        
                        // Only start the first default quest
                        var firstQuest = questManager.DefaultQuests[9];
                        if (firstQuest != null)
                        {
                            logger.Msg($"First quest '{firstQuest.GetQuestTitle()}' current state: {firstQuest.QuestState}");
                            
                            // Initialize quest if it's inactive
                            if (firstQuest.QuestState == EQuestState.Inactive && firstQuest is Quest_WelcomeToHylandPoint)
                            {
                                logger.Msg($"Initializing first quest: {firstQuest.GetQuestTitle()}");
                                firstQuest.Begin(network: true);
                            }
                        }
                        
                        // Save the quest state
                        Singleton<SaveManager>.Instance.Save();
                        logger.Msg("Server quest initialization completed");
                    }
                    else
                    {
                        logger.Warning("No default quests found in QuestManager");
                    }
                }
                else
                {
                    logger.Warning("QuestManager not found during server quest initialization");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error initializing server quests: {ex}");
            }
        }

        private static IEnumerator EnsureQuestInitializationForNewClient(Player player)
        {
            // Wait a moment for everything to settle
            yield return new WaitForSeconds(1f);
            
            try
            {
                if (player == null || player.gameObject == null)
                    yield break;

                logger.Msg($"Ensuring quest initialization for new client: {player.PlayerName}");
                
                var questManager = NetworkSingleton<QuestManager>.Instance;
                if (questManager != null)
                {
                    // For dedicated server, we want to ensure quest data is properly synced to new clients
                    // The quests should already be initialized by InitializeServerQuests, but we need to ensure
                    // the client receives the current quest state
                    
                    logger.Msg($"Synchronizing quest state for new client: {player.PlayerName}");
                    
                    if (questManager.DefaultQuests != null)
                    {
                        foreach (var quest in questManager.DefaultQuests)
                        {
                            if (quest != null)
                            {
                                logger.Msg($"Syncing quest '{quest.GetQuestTitle()}' (state: {quest.QuestState}) to client {player.PlayerName}");
                                
                                // Force sync quest state to this specific client
                                if (quest.QuestState != EQuestState.Inactive)
                                {
                                    questManager.ReceiveQuestState(player.Owner, quest.GUID.ToString(), quest.QuestState);
                                }
                                
                                // Sync quest entry states
                                for (int i = 0; i < quest.Entries.Count; i++)
                                {
                                    if (quest.Entries[i].State != EQuestState.Inactive)
                                    {
                                        questManager.ReceiveQuestEntryState(player.Owner, quest.GUID.ToString(), i, quest.Entries[i].State);
                                    }
                                }
                                
                                // Sync tracking state
                                if (quest.IsTracked)
                                {
                                    questManager.SetQuestTracked(player.Owner, quest.GUID.ToString(), true);
                                }
                            }
                        }
                    }
                    
                    logger.Msg($"Quest synchronization completed for new client: {player.PlayerName}");
                }
                else
                {
                    logger.Warning("QuestManager not found during new client quest sync");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error ensuring quest sync for new client: {ex}");
            }
        }

        public override void OnUpdate()
        {
            // Debug keys for testing
            if (_debugMode)
            {
                if (Input.GetKeyDown(KeyCode.F11))
                {
                    logger.Msg("F11 pressed - starting dedicated server");
                    StartDedicatedServer();
                }
                
                if (Input.GetKeyDown(KeyCode.F12))
                {
                    logger.Msg("F12 pressed - server status");
                    LogServerStatus();
                }
                
                if (Input.GetKeyDown(KeyCode.F10))
                {
                    logger.Msg("F10 pressed - quest status");
                    LogQuestStatus();
                }
                
                if (Input.GetKeyDown(KeyCode.F9))
                {
                    logger.Msg("F9 pressed - manual save");
                    TriggerAutoSave("manual_debug");
                }
            }
        }

        private void LogQuestStatus()
        {
            try
            {
                var questManager = NetworkSingleton<QuestManager>.Instance;
                if (questManager != null)
                {
                    var status = "=== Quest Manager Status ===\n";
                    
                    if (questManager.DefaultQuests != null)
                    {
                        status += $"Default Quests Count: {questManager.DefaultQuests.Length}\n";
                        foreach (var quest in questManager.DefaultQuests)
                        {
                            if (quest != null)
                            {
                                status += $"Quest: {quest.GetQuestTitle()} - State: {quest.QuestState}\n";
                            }
                        }
                    }
                    else
                    {
                        status += "No default quests found\n";
                    }
                    
                    // Check total quest count
                    var totalQuests = Quest.Quests?.Count ?? 0;
                    status += $"Total Active Quests: {totalQuests}\n";
                    
                    logger.Msg(status);
                }
                else
                {
                    logger.Warning("QuestManager not found for status check");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error getting quest status: {ex}");
            }
        }

        private void LogServerStatus()
        {
            try
            {
                var status = "=== Dedicated Server Status ===\n";
                status += $"Server Mode: {_isServerMode}\n";
                status += $"Server Started: {_serverStarted}\n";
                status += $"Server Port: {_serverPort}\n";
                status += $"Ignore Ghost Host for Sleep: {_ignoreGhostHostForSleep}\n";
                status += $"Time Never Stops: {_timeNeverStops}\n";
                status += $"Auto-Save Enabled: {_autoSaveEnabled}\n";
                status += $"Auto-Save Interval: {_autoSaveIntervalMinutes} minutes\n";
                status += $"Auto-Save on Join: {_autoSaveOnPlayerJoin}\n";
                status += $"Auto-Save on Leave: {_autoSaveOnPlayerLeave}\n";
                if (_lastAutoSave != DateTime.MinValue)
                {
                    var timeSinceLastSave = DateTime.Now - _lastAutoSave;
                    status += $"Last Auto-Save: {timeSinceLastSave.TotalMinutes:F1} minutes ago\n";
                }
                else
                {
                    status += $"Last Auto-Save: Never\n";
                }
                status += $"FishNet Is Server: {InstanceFinder.IsServer}\n";
                status += $"FishNet Is Client: {InstanceFinder.IsClient}\n";
                status += $"Current Scene: {SceneManager.GetActiveScene().name}\n";
                
                var loadManager = Singleton<LoadManager>.Instance;
                if (loadManager != null)
                {
                    status += $"LoadManager Status: {loadManager.LoadStatus}\n";
                    status += $"Is Loading: {loadManager.IsLoading}\n";
                    status += $"Is Game Loaded: {loadManager.IsGameLoaded}\n";
                    status += $"Loaded Save Path: {loadManager.LoadedGameFolderPath}\n";
                }

                var networkManager = InstanceFinder.NetworkManager;
                if (networkManager != null)
                {
                    var serverManager = networkManager.ServerManager;
                    if (serverManager != null)
                    {
                        status += $"Connected Clients: {serverManager.Clients.Count}\n";
                    }
                }

                logger.Msg(status);
            }
            catch (Exception ex)
            {
                logger.Error($"Error getting server status: {ex}");
            }
        }
    }
}
