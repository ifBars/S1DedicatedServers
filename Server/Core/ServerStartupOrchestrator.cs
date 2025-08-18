using MelonLoader;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using FishNet;
using FishNet.Component.Scenes;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using ScheduleOne.DevUtilities;
using ScheduleOne.GameTime;
using ScheduleOne.Persistence;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Quests;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DedicatedServerMod.Server.Core
{
    /// <summary>
    /// Orchestrates the full dedicated server start sequence to mirror the legacy flow.
    /// </summary>
    public static class ServerStartupOrchestrator
    {
        private static readonly MelonLogger.Instance logger = new MelonLogger.Instance("ServerStartup");
        private static bool _timeLoopsStarted = false;
        private static bool _loopbackHandled = false;

        public static IEnumerator StartDedicatedServer(string savePathOverride = null)
        {
            logger.Msg("Starting dedicated server loading sequence (orchestrator)");

            // Step 1: Ensure Multipass/Tugboat and configure transport
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
                tugboat = multipass.gameObject.AddComponent<Tugboat>();
                if (tugboat == null)
                {
                    logger.Error("Failed to add Tugboat component");
                    yield break;
                }
            }

            tugboat.SetPort((ushort)ServerConfig.Instance.ServerPort);

            // Step 2: Prepare SaveInfo and LoadManager state
            var loadManager = Singleton<LoadManager>.Instance;
            if (loadManager == null)
            {
                logger.Error("LoadManager not found");
                yield break;
            }

            // Prefer explicit argument first. If none provided, require ServerConfig.SaveGamePath to be set.
            SaveInfo actualSaveInfo = null;
            if (!string.IsNullOrEmpty(savePathOverride))
            {
                if (!LoadManager.TryLoadSaveInfo(savePathOverride, 0, out actualSaveInfo))
                {
                    logger.Error($"Failed to load save info from: {savePathOverride}");
                    yield break;
                }
            }
            else
            {
                string configuredPath = ServerConfig.Instance.SaveGamePath;
                if (string.IsNullOrEmpty(configuredPath))
                {
                    logger.Error("Server startup aborted: 'saveGamePath' is not configured in server_config.json.");
                    logger.Msg($"Please edit the config and set 'saveGamePath' to your save folder. Make sure to use double backslashes not single, otherwise your server will not load the config.");
                    logger.Msg($"Config file location: {ServerConfig.ConfigFilePath}");
                    yield break;
                }

                // Prepare/seed the save folder similar to host flow (DefaultSave + Player_0 + metadata)
                try
                {
                    var orgName = new DirectoryInfo(configuredPath).Name;
                    DedicatedServerMod.Server.Persistence.SaveInitializer.EnsureSavePrepared(
                        configuredPath,
                        orgName,
                        "DedicatedServerHost",
                        logger
                    );
                }
                catch (Exception ex)
                {
                    logger.Warning($"Save preparation encountered an issue: {ex.Message}");
                }

                if (!LoadManager.TryLoadSaveInfo(configuredPath, 0, out actualSaveInfo))
                {
                    logger.Error($"Failed to load save info from: {configuredPath}");
                    yield break;
                }
            }

            logger.Msg($"Loading save game: {actualSaveInfo.SavePath} ({actualSaveInfo.OrganisationName})");
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
            logger.Msg("Loading Main scene");
            loadManager.LoadStatus = LoadManager.ELoadStatus.LoadingScene;
            var sceneLoad = SceneManager.LoadSceneAsync("Main");
            while (!sceneLoad.isDone)
                yield return new WaitForEndOfFrame();

            logger.Msg("Main scene loaded");
            loadManager.LoadStatus = LoadManager.ELoadStatus.Initializing;

            // Ensure default quests are registered before save data loads
            TryRegisterDefaultQuestsWithGuidManager();

            // Reset loaded save path after scene load to prevent LoadManager.Start() debug override to DevSave
            loadManager.ActiveSaveInfo = actualSaveInfo;
            loadManager.LoadedGameFolderPath = actualSaveInfo.SavePath;
            logger.Msg($"Restored loaded save path: {loadManager.LoadedGameFolderPath}");

            // Step 4: Start FishNet server via tugboat
            logger.Msg("Starting FishNet server (Tugboat)");
            bool serverStarted = tugboat.StartConnection(true);
            if (!serverStarted)
            {
                logger.Error("Failed to start Tugboat server");
                yield break;
            }
            float timeout = 10f, elapsed = 0f;
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

            // Step 5: Start loopback client on server (mirror host flow)
            logger.Msg("Starting loopback client on server");
            TrySetClientTransport(multipass, tugboat);
            tugboat.SetClientAddress("127.0.0.1");
            bool clientStarted = tugboat.StartConnection(false);
            if (!clientStarted)
            {
                logger.Warning("Failed to start loopback client");
            }
            else
            {
                float cTimeout = 10f, cElapsed = 0f;
                while (!networkManager.IsClient && cElapsed < cTimeout)
                {
                    yield return new WaitForSeconds(0.1f);
                    cElapsed += 0.1f;
                }
                if (!networkManager.IsClient)
                    logger.Warning("Loopback client did not initialize within timeout");

                // Hide/teleport loopback player on spawn
                ScheduleOne.PlayerScripts.Player.onPlayerSpawned = (Action<ScheduleOne.PlayerScripts.Player>)Delegate.Remove(ScheduleOne.PlayerScripts.Player.onPlayerSpawned, new Action<ScheduleOne.PlayerScripts.Player>(OnLoopbackSpawned));
                ScheduleOne.PlayerScripts.Player.onPlayerSpawned = (Action<ScheduleOne.PlayerScripts.Player>)Delegate.Combine(ScheduleOne.PlayerScripts.Player.onPlayerSpawned, new Action<ScheduleOne.PlayerScripts.Player>(OnLoopbackSpawned));
                TryHandleExistingLoopbackPlayer();
            }

            // Step 6: Load save data
            logger.Msg("Loading save data");
            loadManager.LoadStatus = LoadManager.ELoadStatus.LoadingData;
            yield return MelonCoroutines.Start(LoadSaveData(loadManager));

            // Step 7: Finalize
            logger.Msg("Finalizing server initialization");
            loadManager.LoadStatus = LoadManager.ELoadStatus.None;
            loadManager.IsLoading = false;
            loadManager.IsGameLoaded = true;

            // Ensure TimeManager loops are active server-side
            if (NetworkSingleton<TimeManager>.Instance != null && !_timeLoopsStarted)
            {
                var tm = NetworkSingleton<TimeManager>.Instance;
                tm.TimeProgressionMultiplier = 1f;
                TryStartTimeLoops(tm);
                _timeLoopsStarted = true;
            }

            if (NetworkSingleton<GameManager>.Instance.IsTutorial)
                NetworkSingleton<GameManager>.Instance.EndTutorial(true);

            logger.Msg("=== DEDICATED SERVER READY (orchestrator) ===");
            logger.Msg($"Server running on port {ServerConfig.Instance.ServerPort}");
            logger.Msg($"Loaded save: {Path.GetFileName(actualSaveInfo.SavePath)}");
            logger.Msg("Waiting for client connections...");
        }

        private static void TrySetClientTransport(Multipass multipass, Transport transport)
        {
            var clientField = typeof(Multipass).GetField("_clientTransport", BindingFlags.NonPublic | BindingFlags.Instance);
            if (clientField != null)
            {
                clientField.SetValue(multipass, transport);
                logger.Msg("Set client transport via _clientTransport field");
                return;
            }
            var clientProp = typeof(Multipass).GetProperty("ClientTransport", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (clientProp != null && clientProp.CanWrite)
            {
                clientProp.SetValue(multipass, transport);
                logger.Msg("Set client transport via ClientTransport property");
                return;
            }
            foreach (var m in typeof(Multipass).GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
            {
                if (!m.Name.ToLowerInvariant().Contains("client") || m.GetParameters().Length != 1) continue;
                if (!typeof(Transport).IsAssignableFrom(m.GetParameters()[0].ParameterType)) continue;
                m.Invoke(multipass, new object[] { transport });
                logger.Msg($"Set client transport via method {m.Name}");
                return;
            }
            logger.Warning("Could not set client transport via reflection");
        }

        private static void TryRegisterDefaultQuestsWithGuidManager()
        {
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
                    logger.Msg($"Pre-registered {registered}/{total} default quests with GUIDManager");
            }
            catch (Exception ex)
            {
                logger.Warning($"Quest pre-registration failed: {ex.Message}");
            }
        }

        private static void TryStartTimeLoops(TimeManager tm)
        {
            try
            {
                var timeLoop = typeof(TimeManager).GetMethod("TimeLoop", BindingFlags.NonPublic | BindingFlags.Instance);
                var tickLoop = typeof(TimeManager).GetMethod("TickLoop", BindingFlags.NonPublic | BindingFlags.Instance);
                var a = timeLoop?.Invoke(tm, null) as IEnumerator;
                var b = tickLoop?.Invoke(tm, null) as IEnumerator;
                if (a != null) tm.StartCoroutine(a);
                if (b != null) tm.StartCoroutine(b);
                logger.Msg("Time loops started on server");
            }
            catch (Exception ex)
            {
                logger.Warning($"Failed to start time loops: {ex.Message}");
            }
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
                logger.Error($"Error finding most recent save: {ex}");
                return null;
            }
        }

        private static IEnumerator LoadSaveData(LoadManager loadManager)
        {
            loadManager.onPreLoad?.Invoke();

            logger.Msg("Creating load requests for save data");
            foreach (var baseSaveable in Singleton<SaveManager>.Instance.BaseSaveables)
            {
                // The game's loaders expect per saveable subfolders; preserve per-loader path
                string loadPath = Path.Combine(loadManager.LoadedGameFolderPath, baseSaveable.SaveFolderName);
                new LoadRequest(loadPath, baseSaveable.Loader);
            }

            var loadRequestsField = typeof(LoadManager).GetField("loadRequests", BindingFlags.NonPublic | BindingFlags.Instance);
            var loadRequests = loadRequestsField?.GetValue(loadManager) as System.Collections.Generic.List<LoadRequest>;
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

            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            loadManager.onLoadComplete?.Invoke();
            logger.Msg("Save data loaded successfully");
        }

        private static void OnLoopbackSpawned(ScheduleOne.PlayerScripts.Player p)
        {
            if (_loopbackHandled) return;
            try
            {
                if (p != null && p.Owner != null && p.Owner.IsLocalClient)
                {
                    p.gameObject.name = "[DedicatedServerHostLoopback]";
                    p.SetVisible(false, network: true);
                    p.SetVisibleToLocalPlayer(false);
                    var mv = p.GetComponent<PlayerMovement>();
                    if (mv != null) mv.Teleport(new Vector3(16.456f, 31.176f, -165.366f));
                    else p.transform.position = new Vector3(16.456f, 31.176f, -165.366f);
                    // Ensure server-side quests are initialized once the ghost exists
                    MelonCoroutines.Start(InitializeServerQuests());
                    _loopbackHandled = true;
                    ScheduleOne.PlayerScripts.Player.onPlayerSpawned = (Action<ScheduleOne.PlayerScripts.Player>)Delegate.Remove(ScheduleOne.PlayerScripts.Player.onPlayerSpawned, new Action<ScheduleOne.PlayerScripts.Player>(OnLoopbackSpawned));
                    logger.Msg("Loopback player hidden/teleported");
                }
            }
            catch (Exception ex)
            {
                logger.Warning($"Error handling loopback spawn: {ex.Message}");
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
                    logger.Warning("QuestManager not found during server quest initialization");
                    yield break;
                }

                if (questManager.DefaultQuests != null && questManager.DefaultQuests.Length > 0)
                {
                    // Prefer the welcome quest if present
                    ScheduleOne.Quests.Quest welcomeQuest = null;
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
                    if (firstQuest != null && firstQuest.QuestState == EQuestState.Inactive)
                    {
                        firstQuest.Begin(network: true);
                        logger.Msg($"Initialized quest: {firstQuest.GetQuestTitle()}");
                    }
                }

                // Save after initializing quests so the state persists
                var saveMgr = Singleton<SaveManager>.Instance;
                if (saveMgr != null)
                {
                    saveMgr.Save();
                    logger.Msg("Server quest initialization completed and saved");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error initializing server quests: {ex}");
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
                        if (quest.QuestState != EQuestState.Inactive)
                        {
                            qm.ReceiveQuestState(player.Owner, quest.GUID.ToString(), quest.QuestState);
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
                logger.Msg($"Quest synchronization completed for new client: {player.PlayerName}");
            }
            catch (Exception ex)
            {
                logger.Error($"Error ensuring quest sync for new client: {ex}");
            }
        }
    }
}


