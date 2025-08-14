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
                        var transports = transportsField.GetValue(__instance) as System.Collections.Generic.List<Transport>;
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

            logger.Msg("=== DEDICATED SERVER READY ===");
            logger.Msg($"Server running on port {_serverPort}");
            logger.Msg($"Loaded save: {Path.GetFileName(actualSaveInfo.SavePath)}");
            logger.Msg("Waiting for client connections...");
        }

        private static void OnPlayerSpawned_LoopbackHandler(Player p)
        {
            if (_loopbackPlayerHandled)
                return;
            try
            {
                if (p != null && p.Owner != null && p.Owner.IsLocalClient)
                {
                    // Rename and hide the loopback player's visuals everywhere
                    p.gameObject.name = "[DedicatedServerHostLoopback]";
                    p.SetVisible(false, network: true);
                    p.SetVisibleToLocalPlayer(false);

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
                for (int i = 0; i < Player.PlayerList.Count; i++)
                {
                    var p = Player.PlayerList[i];
                    if (p != null && p.Owner != null && p.Owner.IsLocalClient)
                    {
                        p.gameObject.name = "[DedicatedServerHostLoopback]";
                        p.SetVisible(false, network: true);
                        p.SetVisibleToLocalPlayer(false);

                        _loopbackPlayerHandled = true;
                        logger.Msg("Loopback client player (existing) hidden and renamed for dedicated server.");
                        Player.onPlayerSpawned = (Action<Player>)Delegate.Remove(Player.onPlayerSpawned, new Action<Player>(OnPlayerSpawned_LoopbackHandler));
                        break;
                    }
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
                string loadPath = System.IO.Path.Combine(loadManager.LoadedGameFolderPath, baseSaveable.SaveFolderName);
                logger.Msg($"Creating load request for: {baseSaveable.SaveFolderName} at {loadPath}");
                new LoadRequest(loadPath, baseSaveable.Loader);
            }

            // Wait for all load requests to complete (mirror Load() logic)
            var loadRequestsField = typeof(LoadManager).GetField("loadRequests", BindingFlags.NonPublic | BindingFlags.Instance);
            var loadRequests = loadRequestsField?.GetValue(loadManager) as System.Collections.Generic.List<LoadRequest>;
            
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
                        catch (System.Exception ex)
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
