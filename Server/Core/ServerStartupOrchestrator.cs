using System.Collections;
using System.Reflection;
using DedicatedServerMod.API;
using DedicatedServerMod.Shared.Configuration;
using DedicatedServerMod.Shared.Networking;
using DedicatedServerMod.Utils;
#if IL2CPP
using Il2CppFishNet;
using Il2CppFishNet.Component.Scenes;
using Il2CppFishNet.Transporting;
using Il2CppFishNet.Transporting.Multipass;
using Il2CppFishNet.Transporting.Tugboat;
#else
using FishNet;
using FishNet.Component.Scenes;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
#endif
using MelonLoader;
#if IL2CPP
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.Persistence.Datas;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Quests;
#else
using ScheduleOne.DevUtilities;
using ScheduleOne.Persistence;
using ScheduleOne.Persistence.Datas;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Quests;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DedicatedServerMod.Server.Core
{
    /// <summary>
    /// Orchestrates the full dedicated server start sequence to mirror the legacy flow.
    /// </summary>
    public static class ServerStartupOrchestrator
    {
        private static bool _loopbackHandled = false;

        public static IEnumerator StartDedicatedServer(string savePathOverride = null)
        {
            DebugLog.Info("Starting dedicated server loading sequence (orchestrator)");

            // Step 1: Wait for the game to finish loading initial scenes
            DebugLog.StartupDebug("Waiting for game initialization...");
            
            // Wait a few frames for Unity to settle
            for (int i = 0; i < 10; i++)
                yield return null;
            
            DebugLog.StartupDebug($"Current scene: {SceneManager.GetActiveScene().name}");
            
            // Step 2: Ensure Multipass/Tugboat and configure transport
            FishNet.Managing.NetworkManager networkManager = null;
            
            // Wait for NetworkManager to be available (it might be initializing in the first scene)
            float waitNm = 0f;
            DebugLog.StartupDebug("Checking for NetworkManager...");
            
            while (networkManager == null && waitNm < 30f)
            {
                // Try both methods to find it
                try
                {
                    networkManager = InstanceFinder.NetworkManager;
                }
                catch (Exception ex)
                {
                    DebugLog.StartupDebug($"InstanceFinder threw: {ex.Message}");
                }
                
                if (networkManager == null)
                {
                    try
                    {
                        networkManager = UnityEngine.Object.FindObjectOfType<FishNet.Managing.NetworkManager>();
                        if (networkManager != null)
                            DebugLog.StartupDebug("Found NetworkManager via FindObjectOfType");
                    }
                    catch (Exception ex)
                    {
                        DebugLog.StartupDebug($"FindObjectOfType threw: {ex.Message}");
                    }
                }
                
                if (networkManager == null)
                {
                    DebugLog.StartupDebug($"Waiting for NetworkManager... ({waitNm}s elapsed)");
                    yield return new WaitForSeconds(1f);
                    waitNm += 1f;
                }
            }

            if (networkManager == null)
            {
                DebugLog.Error("NetworkManager not found in any open scenes after waiting 30 seconds.");
                yield break;
            }
            
            DebugLog.StartupDebug($"NetworkManager found! Scene: {networkManager.gameObject.scene.name}");

            var transportManager = networkManager.TransportManager;
            var transport = transportManager.Transport;
            if (!MultipassTransportResolver.TryResolve(transport, out var multipass))
            {
                DebugLog.Error($"Multipass transport not found. {MultipassTransportResolver.Describe(transport)}");
                yield break;
            }

            var tugboat = multipass.gameObject.GetComponent<Tugboat>();
            if (tugboat == null)
            {
                tugboat = multipass.gameObject.AddComponent<Tugboat>();
                if (tugboat == null)
                {
                    DebugLog.Error("Failed to add Tugboat component");
                    yield break;
                }
            }

            tugboat.SetPort((ushort)ServerConfig.Instance.ServerPort);

            // Step 2: Prepare SaveInfo and LoadManager state
            var loadManager = Singleton<LoadManager>.Instance;
            if (loadManager == null)
            {
                DebugLog.Error("LoadManager not found");
                yield break;
            }

            // Prefer explicit argument first. If none provided, use ServerConfig's resolved path (default or custom).
            SaveInfo actualSaveInfo = null;
            if (!string.IsNullOrEmpty(savePathOverride))
            {
                if (!LoadManager.TryLoadSaveInfo(savePathOverride, 0, out actualSaveInfo))
                {
                    DebugLog.Error($"Failed to load save info from: {savePathOverride}");
                    yield break;
                }
            }
            else
            {
                // Get resolved path (either custom or default)
                string configuredPath = ServerConfig.GetResolvedSaveGamePath();
                
                DebugLog.StartupDebug($"Preparing save folder: {configuredPath}");

                // Prepare/seed the save folder similar to host flow (DefaultSave + Player_0 + metadata)
                try
                {
                    var orgName = new DirectoryInfo(configuredPath).Name;
                    Persistence.SaveInitializer.EnsureSavePrepared(
                        configuredPath,
                        orgName,
                        Constants.GhostHostSyntheticSteamId
                    );
                }
                catch (Exception ex)
                {
                    DebugLog.Warning($"Save preparation encountered an issue: {ex.Message}");
                }

                if (!LoadManager.TryLoadSaveInfo(configuredPath, 0, out actualSaveInfo))
                {
                    DebugLog.Error($"Failed to load save info from: {configuredPath}");
                    yield break;
                }
            }

            DebugLog.Info($"Loading save game: {actualSaveInfo.SavePath} ({actualSaveInfo.OrganisationName})");
            loadManager.ActiveSaveInfo = actualSaveInfo;
            loadManager.IsLoading = true;
            loadManager.TimeSinceGameLoaded = 0f;
            loadManager.LoadedGameFolderPath = actualSaveInfo.SavePath;
            LoadManager.LoadHistory.Add("Loading game: " + actualSaveInfo.OrganisationName);

            if (InstanceFinder.IsServer)
                InstanceFinder.NetworkManager.ServerManager.StopConnection(false);
            if (InstanceFinder.IsClient)
                InstanceFinder.NetworkManager.ClientManager.StopConnection();

            loadManager.onPreSceneChange?.Invoke();
            var cleanUpMethod = typeof(LoadManager).GetMethod("CleanUp", BindingFlags.NonPublic | BindingFlags.Instance);
            cleanUpMethod?.Invoke(loadManager, null);

            var defaultScene = networkManager.gameObject.GetComponent<DefaultScene>();
            defaultScene?.SetOnlineScene("Main");

            // Step 3: Load Main scene
            DebugLog.Info("Loading Main scene");
            loadManager.LoadStatus = LoadManager.ELoadStatus.LoadingScene;
            var sceneLoad = SceneManager.LoadSceneAsync("Main");
            while (!sceneLoad.isDone)
                yield return new WaitForEndOfFrame();

            DebugLog.Info("Main scene loaded");
            loadManager.LoadStatus = LoadManager.ELoadStatus.Initializing;

            ServerBootstrap.CustomClothing?.Initialize();
            DebugLog.StartupDebug("Custom clothing manager initialized after Main scene load");

            // Ensure default quests are registered before save data loads
            TryRegisterDefaultQuestsWithGuidManager();

            // Reset loaded save path after scene load to prevent LoadManager.Start() debug override to DevSave
            loadManager.ActiveSaveInfo = actualSaveInfo;
            loadManager.LoadedGameFolderPath = actualSaveInfo.SavePath;
            DebugLog.StartupDebug($"Restored loaded save path: {loadManager.LoadedGameFolderPath}");

            TryDisableHeadlessAutoStart(networkManager.ServerManager);

            // Step 4: Start FishNet server via ServerManager (ensures Multipass TransportIdData registration)
            if (InstanceFinder.IsServer)
            {
                DebugLog.StartupDebug("Server already running, skipping ServerManager.StartConnection()");
            }
            else
            {
                DebugLog.Info("Starting FishNet server (via ServerManager)");
                networkManager.ServerManager.StartConnection();
            }
            float timeout = 10f, elapsed = 0f;
            while (!InstanceFinder.IsServer && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }
            if (!InstanceFinder.IsServer)
            {
                DebugLog.Error("Server failed to start within timeout");
                yield break;
            }
            DebugLog.Info("FishNet server started successfully");

            // Step 5: Start loopback client on server (mirror host flow)
            if (networkManager.IsClient)
            {
                DebugLog.StartupDebug("Loopback client already connected, skipping ClientManager.StartConnection()");
            }
            else
            {
                DebugLog.Info("Starting loopback client on server");
                TrySetClientTransport(multipass, tugboat);
                tugboat.SetClientAddress("127.0.0.1");
                bool clientStarted = networkManager.ClientManager.StartConnection();
                if (!clientStarted)
                {
                    DebugLog.Warning("ClientManager.StartConnection() returned false, client may already be starting");
                }
            }

            // Wait for loopback client to be ready
            float cTimeout = 10f, cElapsed = 0f;
            while (!networkManager.IsClient && cElapsed < cTimeout)
            {
                yield return new WaitForSeconds(0.1f);
                cElapsed += 0.1f;
            }
            if (!networkManager.IsClient)
            {
                DebugLog.Error("Loopback client did not initialize within timeout");
                yield break;
            }

            // Hide/teleport loopback player on spawn
#if MONO
            ScheduleOne.PlayerScripts.Player.onPlayerSpawned = (Action<ScheduleOne.PlayerScripts.Player>)Delegate.Remove(ScheduleOne.PlayerScripts.Player.onPlayerSpawned, new Action<ScheduleOne.PlayerScripts.Player>(OnLoopbackSpawned));
            ScheduleOne.PlayerScripts.Player.onPlayerSpawned = (Action<ScheduleOne.PlayerScripts.Player>)Delegate.Combine(ScheduleOne.PlayerScripts.Player.onPlayerSpawned, new Action<ScheduleOne.PlayerScripts.Player>(OnLoopbackSpawned));
#else
            DebugLog.StartupDebug("Skipping loopback spawn hook wiring on IL2CPP runtime");
#endif
            TryHandleExistingLoopbackPlayer();

            // Native host flow guarantees Player.Local before loading persistence data.
            loadManager.LoadStatus = LoadManager.ELoadStatus.SpawningPlayer;
            DebugLog.StartupDebug("Waiting for loopback local player spawn");
            float localPlayerTimeout = 30f;
            float localPlayerElapsed = 0f;
            while (ScheduleOne.PlayerScripts.Player.Local == null && localPlayerElapsed < localPlayerTimeout)
            {
                TryHandleExistingLoopbackPlayer();
                yield return new WaitForSeconds(0.1f);
                localPlayerElapsed += 0.1f;
            }

            if (ScheduleOne.PlayerScripts.Player.Local == null)
            {
                DebugLog.Error($"Loopback Player.Local did not spawn within {localPlayerTimeout:F1}s");
                yield break;
            }

            DebugLog.StartupDebug($"Loopback local player ready after {localPlayerElapsed:F1}s");
            TryHandleExistingLoopbackPlayer();
            TryNormalizeLoopbackHostPersistence(loadManager.LoadedGameFolderPath);

            // Step 6: Load save data
            DebugLog.Info("Loading save data");
            loadManager.LoadStatus = LoadManager.ELoadStatus.LoadingData;
            yield return MelonCoroutines.Start(LoadSaveData(loadManager));

            // Step 7: Finalize
            DebugLog.StartupDebug("Finalizing server initialization");
            loadManager.LoadStatus = LoadManager.ELoadStatus.None;
            loadManager.IsLoading = false;
            loadManager.IsGameLoaded = true;

            if (NetworkSingleton<GameManager>.Instance.IsTutorial)
                NetworkSingleton<GameManager>.Instance.EndTutorial(true);

            DebugLog.Info("=== DEDICATED SERVER READY (orchestrator) ===");
            DebugLog.Info($"Server running on port {ServerConfig.Instance.ServerPort}");
            DebugLog.Info($"Loaded save: {Path.GetFileName(actualSaveInfo.SavePath)}");
            DebugLog.Info("Waiting for client connections...");

            // Notify API mods: server started
            ModManager.NotifyServerStarted();
        }

        private static void TrySetClientTransport(Multipass multipass, Transport transport)
        {
            var clientField = typeof(Multipass).GetField("_clientTransport", BindingFlags.NonPublic | BindingFlags.Instance);
            if (clientField != null)
            {
                clientField.SetValue(multipass, transport);
                DebugLog.StartupDebug("Set client transport via _clientTransport field");
                return;
            }
            var clientProp = typeof(Multipass).GetProperty("ClientTransport", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (clientProp != null && clientProp.CanWrite)
            {
                clientProp.SetValue(multipass, transport);
                DebugLog.StartupDebug("Set client transport via ClientTransport property");
                return;
            }
            foreach (var m in typeof(Multipass).GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
            {
                if (!m.Name.ToLowerInvariant().Contains("client") || m.GetParameters().Length != 1) continue;
                if (!typeof(Transport).IsAssignableFrom(m.GetParameters()[0].ParameterType)) continue;
                m.Invoke(multipass, new object[] { transport });
                DebugLog.StartupDebug($"Set client transport via method {m.Name}");
                return;
            }
            DebugLog.Warning("Could not set client transport via reflection");
        }

        private static void TryDisableHeadlessAutoStart(FishNet.Managing.Server.ServerManager serverManager)
        {
            if (serverManager == null)
            {
                return;
            }

            try
            {
                if (!serverManager.GetStartOnHeadless())
                {
                    return;
                }

                serverManager.SetStartOnHeadless(false);
                DebugLog.StartupDebug("Disabled FishNet StartOnHeadless; orchestrator is the single server startup authority");
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"Failed to disable FishNet StartOnHeadless: {ex.Message}");
            }
        }

        private static void TryRegisterDefaultQuestsWithGuidManager()
        {
#if IL2CPP
            DebugLog.StartupDebug("Skipping GUIDManager quest registration on IL2CPP runtime");
            return;
#else
            try
            {
                var questManager = NetworkSingleton<QuestManager>.Instance ?? UnityEngine.Object.FindObjectOfType<QuestManager>();
                if (questManager == null || questManager.DefaultQuests == null) return;

                int total = questManager.DefaultQuests.Length;
                int registered = 0;
                for (int i = 0; i < questManager.DefaultQuests.Length; i++)
                {
                    var quest = questManager.DefaultQuests[i];
                    if (quest == null) continue;
                    try
                    {
                        string guidString = quest.StaticGUID;
                        if (!GUIDManager.IsGUIDValid(guidString))
                        {
                            var newGuid = GUIDManager.GenerateUniqueGUID();
                            guidString = newGuid.ToString();
                            quest.StaticGUID = guidString;
                        }
                        var guid = new Guid(guidString);
                        quest.SetGUID(guid);
                        registered++;
                    }
                    catch { }
                }
                if (registered > 0)
                    DebugLog.StartupDebug($"Pre-registered {registered}/{total} default quests with GUIDManager");
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"Quest pre-registration failed: {ex.Message}");
            }
#endif
        }

        private static string FindMostRecentSave()
        {
            try
            {
                string savesDir = Path.Combine(Application.persistentDataPath, "saves");
                if (!Directory.Exists(savesDir)) return null;
                var saveDirs = Directory.GetDirectories(savesDir);
                if (saveDirs.Length == 0) return null;
                string mostRecent = null; DateTime mostRecentTime = DateTime.MinValue;
                foreach (var dir in saveDirs)
                {
                    var info = new DirectoryInfo(dir);
                    if (info.LastWriteTime > mostRecentTime)
                    {
                        mostRecentTime = info.LastWriteTime;
                        mostRecent = dir;
                    }
                }
                return mostRecent;
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error finding most recent save: {ex}");
                return null;
            }
        }

        private static IEnumerator LoadSaveData(LoadManager loadManager)
        {
            ModManager.NotifyBeforeLoad();
            loadManager.onPreLoad?.Invoke();

            DebugLog.StartupDebug("Creating load requests for save data");
#if MONO
            foreach (var baseSaveable in Singleton<SaveManager>.Instance.BaseSaveables)
            {
                // The game's loaders expect per saveable subfolders; preserve per-loader path
                string loadPath = Path.Combine(loadManager.LoadedGameFolderPath, baseSaveable.SaveFolderName);
                new LoadRequest(loadPath, baseSaveable.Loader);
            }
#else
            DebugLog.StartupDebug("Skipping manual load request queue creation on IL2CPP runtime");
#endif

            var loadRequestsField = typeof(LoadManager).GetField("loadRequests", BindingFlags.NonPublic | BindingFlags.Instance);
            var loadRequests = loadRequestsField?.GetValue(loadManager) as List<LoadRequest>;
            if (loadRequests != null)
            {
                while (loadRequests.Count > 0)
                {
                    for (int i = 0; i < 50; i++)
                    {
                        if (loadRequests.Count <= 0) break;
                        var lr = loadRequests[0];
                        try { lr.Complete(); }
                        catch { if (loadRequests.Count > 0 && loadRequests[0] == lr) loadRequests.RemoveAt(0); }
                    }
                    yield return new WaitForEndOfFrame();
                }
            }

            EnsureLoopbackHostDataLoaded(loadManager.LoadedGameFolderPath);

            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            loadManager.onLoadComplete?.Invoke();
            ModManager.NotifyAfterLoad();
            DebugLog.Info("Save data loaded successfully");
        }

        private static void OnLoopbackSpawned(ScheduleOne.PlayerScripts.Player p)
        {
            if (_loopbackHandled) return;
            try
            {
                if (p != null && p.Owner != null && p.Owner.IsLocalClient)
                {
                    p.gameObject.name = Constants.GhostHostObjectName;
                    p.HasCompletedIntro = true;
                    p.SetVisible(false, network: true);
                    p.SetVisibleToLocalPlayer(false);
                    var mv = p.GetComponent<PlayerMovement>();
                    if (mv != null) mv.Teleport(new Vector3(16.456f, 31.176f, -165.366f));
                    else p.transform.position = new Vector3(16.456f, 31.176f, -165.366f);
                    // Ensure server-side quests are initialized once the ghost exists
                    MelonCoroutines.Start(InitializeServerQuests());
                    _loopbackHandled = true;
#if MONO
                    ScheduleOne.PlayerScripts.Player.onPlayerSpawned = (Action<ScheduleOne.PlayerScripts.Player>)Delegate.Remove(ScheduleOne.PlayerScripts.Player.onPlayerSpawned, new Action<ScheduleOne.PlayerScripts.Player>(OnLoopbackSpawned));
#endif
                    DebugLog.StartupDebug("Loopback player hidden/teleported");
                }
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"Error handling loopback spawn: {ex.Message}");
            }
        }

        private static void TryHandleExistingLoopbackPlayer()
        {
            if (_loopbackHandled) return;
            try
            {
                foreach (var p in ScheduleOne.PlayerScripts.Player.PlayerList)
                {
                    if (p == null || p.Owner == null || !p.Owner.IsLocalClient) continue;
                    OnLoopbackSpawned(p);
                    break;
                }
            }
            catch { }
        }

        /// <summary>
        /// Initialize default quests on server if needed (e.g., welcome quest) and save.
        /// </summary>
        public static IEnumerator InitializeServerQuests()
        {
            yield return new WaitForSeconds(2f);
            try
            {
                var questManager = NetworkSingleton<QuestManager>.Instance ?? UnityEngine.Object.FindObjectOfType<QuestManager>();
                if (questManager == null)
                {
                    DebugLog.Warning("QuestManager not found during server quest initialization");
                    yield break;
                }

                if (questManager.DefaultQuests != null && questManager.DefaultQuests.Length > 0)
                {
                    // Prefer the welcome quest if present
                    Quest welcomeQuest = null;
                    foreach (var q in questManager.DefaultQuests)
                    {
                        if (q == null) continue;
                        if (q.GetType().Name.Contains("WelcomeToHylandPoint"))
                        {
                            welcomeQuest = q;
                            break;
                        }
                    }
                    var firstQuest = welcomeQuest ?? questManager.DefaultQuests[0];
                    if (firstQuest != null && firstQuest.State == EQuestState.Inactive)
                    {
                        firstQuest.Begin(network: true);
                        DebugLog.StartupDebug($"Initialized quest: {firstQuest.GetQuestTitle()}");
                    }
                }

                DebugLog.StartupDebug("Server quest initialization completed.");
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error initializing server quests: {ex}");
            }
        }

        private static void EnsureLoopbackHostDataLoaded(string saveFolderPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(saveFolderPath) || ScheduleOne.PlayerScripts.Player.Local == null)
                {
                    return;
                }

                string player0Dir = Path.Combine(saveFolderPath, "Players", "Player_0");
                string playerJsonPath = Path.Combine(player0Dir, "Player.json");
                if (!File.Exists(playerJsonPath))
                {
                    DebugLog.Warning($"Loopback player data file missing: {playerJsonPath}");
                    ScheduleOne.PlayerScripts.Player.Local.HasCompletedIntro = true;
                    return;
                }

                string json = File.ReadAllText(playerJsonPath);
                PlayerData playerData = JsonUtility.FromJson<PlayerData>(json);
                if (playerData == null)
                {
                    DebugLog.Warning("Failed to deserialize loopback Player_0 data; forcing intro complete in memory.");
                    ScheduleOne.PlayerScripts.Player.Local.HasCompletedIntro = true;
                    return;
                }

                if (string.IsNullOrWhiteSpace(playerData.PlayerCode) && !string.IsNullOrWhiteSpace(ScheduleOne.PlayerScripts.Player.Local.PlayerCode))
                {
                    playerData.PlayerCode = ScheduleOne.PlayerScripts.Player.Local.PlayerCode;
                }

                playerData.IntroCompleted = true;
                ScheduleOne.PlayerScripts.Player.Local.Load(playerData, player0Dir);
                ScheduleOne.PlayerScripts.Player.Local.HasCompletedIntro = true;
                DebugLog.StartupDebug("Loopback host data loaded directly from Player_0 before onLoadComplete.");
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"Failed to load loopback host data directly: {ex.Message}");
                if (ScheduleOne.PlayerScripts.Player.Local != null)
                {
                    ScheduleOne.PlayerScripts.Player.Local.HasCompletedIntro = true;
                }
            }
        }

        private static void TryNormalizeLoopbackHostPersistence(string saveFolderPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(saveFolderPath) || ScheduleOne.PlayerScripts.Player.Local == null)
                {
                    return;
                }

                string loopbackPlayerCode = ScheduleOne.PlayerScripts.Player.Local.PlayerCode;
                if (string.IsNullOrWhiteSpace(loopbackPlayerCode))
                {
                    DebugLog.StartupDebug("Loopback PlayerCode not available yet; skipping Player_0 persistence normalization.");
                    return;
                }

                string playersDir = Path.Combine(saveFolderPath, "Players");
                string player0Dir = Path.Combine(playersDir, "Player_0");
                string loopbackDir = Path.Combine(playersDir, $"Player_{loopbackPlayerCode}");

                if (!Path.GetFullPath(loopbackDir).Equals(Path.GetFullPath(player0Dir), StringComparison.OrdinalIgnoreCase)
                    && Directory.Exists(loopbackDir))
                {
                    Directory.CreateDirectory(player0Dir);

                    PlayerData player0Data = ReadPlayerData(Path.Combine(player0Dir, "Player.json"));
                    bool player0LooksLikeBootstrap =
                        player0Data == null
                        || string.IsNullOrWhiteSpace(player0Data.PlayerCode)
                        || string.Equals(player0Data.PlayerCode, Constants.GhostHostSyntheticSteamId, StringComparison.Ordinal)
                        || string.Equals(player0Data.PlayerCode, "DedicatedServerHost", StringComparison.Ordinal);

                    if (player0LooksLikeBootstrap)
                    {
                        CopyDirectoryContents(loopbackDir, player0Dir, overwrite: true);
                        Directory.Delete(loopbackDir, recursive: true);
                        DebugLog.Info($"Adopted loopback host save folder '{Path.GetFileName(loopbackDir)}' into Player_0.");
                    }
                    else if (string.Equals(player0Data.PlayerCode, loopbackPlayerCode, StringComparison.Ordinal))
                    {
                        Directory.Delete(loopbackDir, recursive: true);
                        DebugLog.Info($"Removed duplicate loopback host save folder '{Path.GetFileName(loopbackDir)}'; Player_0 is canonical.");
                    }
                    else
                    {
                        DebugLog.Warning(
                            $"Found conflicting loopback host save folders: Player_0 uses '{player0Data.PlayerCode}', " +
                            $"but '{Path.GetFileName(loopbackDir)}' exists for '{loopbackPlayerCode}'. Keeping Player_0.");
                    }
                }

                NormalizePlayer0Identity(player0Dir, loopbackPlayerCode);
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"Failed to normalize loopback host persistence: {ex.Message}");
            }
        }

        private static void NormalizePlayer0Identity(string player0Dir, string loopbackPlayerCode)
        {
            if (string.IsNullOrWhiteSpace(player0Dir) || string.IsNullOrWhiteSpace(loopbackPlayerCode))
            {
                return;
            }

            string playerJsonPath = Path.Combine(player0Dir, "Player.json");
            PlayerData playerData = ReadPlayerData(playerJsonPath);
            if (playerData == null)
            {
                return;
            }

            bool changed = false;
            if (!string.Equals(playerData.PlayerCode, loopbackPlayerCode, StringComparison.Ordinal))
            {
                playerData.PlayerCode = loopbackPlayerCode;
                changed = true;
            }

            if (!playerData.IntroCompleted)
            {
                playerData.IntroCompleted = true;
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            File.WriteAllText(playerJsonPath, playerData.GetJson());
            DebugLog.StartupDebug($"Normalized Player_0 identity to loopback PlayerCode '{loopbackPlayerCode}'.");
        }

        private static PlayerData ReadPlayerData(string playerJsonPath)
        {
            try
            {
                if (!File.Exists(playerJsonPath))
                {
                    return null;
                }

                string json = File.ReadAllText(playerJsonPath);
                return string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<PlayerData>(json);
            }
            catch
            {
                return null;
            }
        }

        private static void CopyDirectoryContents(string sourceDir, string destinationDir, bool overwrite)
        {
            Directory.CreateDirectory(destinationDir);

            foreach (string directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(sourceDir, directory);
                Directory.CreateDirectory(Path.Combine(destinationDir, relativePath));
            }

            foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(sourceDir, file);
                string destinationPath = Path.Combine(destinationDir, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? destinationDir);
                File.Copy(file, destinationPath, overwrite);
            }
        }

        /// <summary>
        /// Ensure quest state for a newly initialized client matches the server.
        /// </summary>
        public static IEnumerator EnsureQuestInitializationForNewClient(ScheduleOne.PlayerScripts.Player player)
        {
            yield return new WaitForSeconds(1f);
            try
            {
                if (player == null || player.gameObject == null) yield break;
                var qm = NetworkSingleton<QuestManager>.Instance;
                if (qm == null) yield break;

                if (qm.DefaultQuests != null)
                {
                    foreach (var quest in qm.DefaultQuests)
                    {
                        if (quest == null) continue;
                        // Sync main quest state to the specific client
                        if (quest.State != EQuestState.Inactive)
                        {
                            qm.ReceiveQuestState(player.Owner, quest.GUID.ToString(), quest.State);
                        }
                        for (int i = 0; i < quest.Entries.Count; i++)
                        {
                            if (quest.Entries[i].State != EQuestState.Inactive)
                                qm.ReceiveQuestEntryState(player.Owner, quest.GUID.ToString(), i, quest.Entries[i].State);
                        }
                        if (quest.IsTracked)
                            qm.SetQuestTracked(player.Owner, quest.GUID.ToString(), true);
                    }
                }
                DebugLog.StartupDebug($"Quest synchronization completed for new client: {player.PlayerName}");
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error ensuring quest sync for new client: {ex}");
            }
        }
    }
}


