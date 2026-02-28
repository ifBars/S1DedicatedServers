#if IL2CPP
using Il2CppFishNet;
using Il2CppFishNet.Transporting;
using Il2CppFishNet.Transporting.Multipass;
using Il2CppFishNet.Transporting.Tugboat;
#else
using FishNet;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
#endif
using MelonLoader;
#if IL2CPP
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Networking;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI;
#else
using ScheduleOne.DevUtilities;
using ScheduleOne.Networking;
using ScheduleOne.Persistence;
using ScheduleOne.PlayerScripts;
using ScheduleOne.UI;
#endif
using System;
using System.Collections;
using System.Reflection;
using DedicatedServerMod.API;
using DedicatedServerMod.Client.Core;
using DedicatedServerMod.Client.Patchers;
using DedicatedServerMod.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DedicatedServerMod.Client.Managers
{
    /// <summary>
    /// Manages the dedicated server client join sequence, mirroring the native
    /// LoadManager.LoadAsClient flow step-by-step. See <see cref="NativeInvariants"/>
    /// for the full contract this class must satisfy.
    /// </summary>
    public class ClientConnectionManager
    {
        private readonly MelonLogger.Instance logger;

        private static string _targetServerIP = "localhost";
        private static int _targetServerPort = 38465;
        private static bool _isTugboatMode = false;

        private static readonly MethodInfo CleanUpMethod =
            typeof(LoadManager).GetMethod("CleanUp", BindingFlags.NonPublic | BindingFlags.Instance);
#if MONO
        private static readonly Action LocalPlayerSpawnedHandler = OnLocalPlayerSpawned;
#endif

        public bool IsConnecting { get; private set; }
        public bool IsConnectedToDedicatedServer { get; private set; }
        public string LastConnectionError { get; private set; }

        public ClientConnectionManager(MelonLogger.Instance logger)
        {
            this.logger = logger;
        }

        public void Initialize()
        {
            try
            {
                logger.Msg("Initializing ClientConnectionManager");
                ParseCommandLineArguments();
                logger.Msg("ClientConnectionManager initialized");
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to initialize ClientConnectionManager: {ex}");
            }
        }

        private void ParseCommandLineArguments()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--server-ip" && i + 1 < args.Length)
                {
                    _targetServerIP = args[i + 1];
                    logger.Msg($"Target server IP set to: {_targetServerIP}");
                }
                if (args[i] == "--server-port" && i + 1 < args.Length)
                {
                    if (int.TryParse(args[i + 1], out int port))
                    {
                        _targetServerPort = port;
                        logger.Msg($"Target server port set to: {_targetServerPort}");
                    }
                }
            }
        }

        /// <summary>
        /// Entry point: begins the full dedicated server join sequence.
        /// Mirrors native LoadManager.LoadAsClient steps 1-16.
        /// </summary>
        public void StartDedicatedConnection()
        {
            if (IsConnecting)
            {
                logger.Warning("Connection already in progress");
                return;
            }

            logger.Msg($"Starting dedicated server connection to {_targetServerIP}:{_targetServerPort}");
            IsConnecting = true;
            _isTugboatMode = true;
            LastConnectionError = null;

            MelonCoroutines.Start(ClientLoadSequence());
        }

        /// <summary>
        /// Full client join coroutine mirroring LoadManager.LoadAsClient (lines 642-727).
        /// Each step is annotated with the corresponding native source line.
        /// </summary>
        private IEnumerator ClientLoadSequence()
        {
            var timeline = new JoinTimeline(logger);
            timeline.Mark("Begin");

            var loadManager = Singleton<LoadManager>.Instance;
            if (loadManager == null)
            {
                HandleConnectionError("LoadManager not found");
                yield break;
            }

            // --- Step 1 (native 644-656): If game already loaded, exit to menu first ---
            if (loadManager.IsGameLoaded)
            {
                timeline.Mark("ExitToMenu");
                loadManager.ExitToMenu();
                float exitTimeout = 15f;
                float exitElapsed = 0f;
                while (exitElapsed < exitTimeout &&
                       (loadManager.IsLoading || SceneManager.GetActiveScene().name != "Menu"))
                {
                    yield return new WaitForSeconds(0.1f);
                    exitElapsed += 0.1f;
                }
            }

            // --- Step 2 (native 659-664): Initialize load state ---
            timeline.Mark("InitLoadState");
            loadManager.ActiveSaveInfo = null;
            loadManager.IsLoading = true;
            loadManager.TimeSinceGameLoaded = 0f;
            loadManager.LoadedGameFolderPath = string.Empty;
            loadManager.LoadStatus = LoadManager.ELoadStatus.LoadingScene;

            // --- Step 3 (native 665): Open loading screen ---
            if (Singleton<LoadingScreen>.InstanceExists)
                Singleton<LoadingScreen>.Instance.Open();

            // --- Step 5 (native 667-669): Pre-scene change event ---
            // Fires SaveManager.Clean, ProductManager.Clean, NPCManager, etc.
            timeline.Mark("onPreSceneChange");
            loadManager.onPreSceneChange?.Invoke();

            // --- Step 6 (native 671): CleanUp ---
            // Clears GUIDManager, Quest lists, PlayerList, staggeredReplicators, etc.
            timeline.Mark("CleanUp");
            CleanUpMethod?.Invoke(loadManager, null);

            // --- Step 7 (native 672-680): Configure transport and set timeout ---
            timeline.Mark("ConfigureTransport");
            if (!ConfigureAndStartTransport())
            {
                HandleConnectionError("Transport configuration or connection start failed");
                yield break;
            }

            // --- Step 9 (native 692): Register player spawn handler ---
            // Must be AFTER CleanUp which clears Player.onLocalPlayerSpawned.
#if MONO
            Player.onLocalPlayerSpawned = (Action)Delegate.Combine(
                Player.onLocalPlayerSpawned, LocalPlayerSpawnedHandler);
#else
            logger.Msg("Skipping local player spawned delegate hook on IL2CPP runtime");
#endif

            // --- Step 10 (native 693): Wait for Main scene ---
            // FishNet syncs the scene from the server as part of connection.
            timeline.Mark("WaitForMainScene");
            float sceneTimeout = 30f;
            float sceneElapsed = 0f;
            while (SceneManager.GetActiveScene().name != "Main" && sceneElapsed < sceneTimeout)
            {
                yield return new WaitForSeconds(0.1f);
                sceneElapsed += 0.1f;
            }
            if (SceneManager.GetActiveScene().name != "Main")
            {
                timeline.MarkError("MainSceneLoad", $"timeout after {sceneTimeout}s");
                HandleConnectionError($"Main scene load timeout ({sceneTimeout}s)");
                yield break;
            }
            timeline.Mark("MainSceneLoaded", $"{sceneElapsed:F1}s");

            // --- Step 11 (native 695-697): Pre-load event ---
            timeline.Mark("onPreLoad");
            loadManager.onPreLoad?.Invoke();

            // --- Step 12 (native 699-700): Wait for Player.Local ---
            loadManager.LoadStatus = LoadManager.ELoadStatus.SpawningPlayer;
            timeline.Mark("WaitForPlayerLocal");
            float playerTimeout = 30f;
            float playerElapsed = 0f;
            while (Player.Local == null && playerElapsed < playerTimeout)
            {
                yield return new WaitForSeconds(0.1f);
                playerElapsed += 0.1f;
            }
            if (Player.Local == null)
            {
                timeline.MarkError("PlayerLocalSpawn", $"timeout after {playerTimeout}s");
                HandleConnectionError($"Player.Local spawn timeout ({playerTimeout}s)");
                yield break;
            }
            timeline.Mark("PlayerLocalSpawned", $"{playerElapsed:F1}s");

            // --- Step 13 (native 702-703): Wait for player data ---
            // Server sends ReceivePlayerData RPC which sets playerDataRetrieveReturned.
            loadManager.LoadStatus = LoadManager.ELoadStatus.LoadingData;
            timeline.Mark("WaitForPlayerData");
            float dataTimeout = 30f;
            float dataElapsed = 0f;
            while (Player.Local != null && !Player.Local.playerDataRetrieveReturned && dataElapsed < dataTimeout)
            {
                yield return new WaitForSeconds(0.1f);
                dataElapsed += 0.1f;
            }
            if (Player.Local == null || !Player.Local.playerDataRetrieveReturned)
            {
                timeline.Mark("PlayerDataTimeout", $"{dataElapsed:F1}s");
                logger.Warning("Player data retrieve timed out - continuing");
            }
            else
            {
                timeline.Mark("PlayerDataReceived", $"{dataElapsed:F1}s");
            }

            // --- Step 14 (native 705-709): Wait for replication ---
            // ReplicationQueue.ReplicationDoneForLocalPlayer set by TargetRpc from server,
            // or LocalPlayerReplicationTimedOut after 45s.
            loadManager.LoadStatus = LoadManager.ELoadStatus.Initializing;
            timeline.Mark("WaitForReplication");
            float repTimeout = 50f;
            float repElapsed = 0f;
            while (repElapsed < repTimeout)
            {
                if (NetworkSingleton<ReplicationQueue>.InstanceExists)
                {
                    var repQueue = NetworkSingleton<ReplicationQueue>.Instance;
                    if (repQueue.ReplicationDoneForLocalPlayer || repQueue.LocalPlayerReplicationTimedOut)
                        break;
                }
                yield return new WaitForSeconds(0.1f);
                repElapsed += 0.1f;
            }
            if (NetworkSingleton<ReplicationQueue>.InstanceExists &&
                NetworkSingleton<ReplicationQueue>.Instance.LocalPlayerReplicationTimedOut)
            {
                var task = NetworkSingleton<ReplicationQueue>.Instance.CurrentReplicationTask;
                timeline.Mark("ReplicationTimeout", task);
                logger.Warning($"Replication timed out. Current task: {task}");
            }
            else
            {
                timeline.Mark("ReplicationComplete", $"{repElapsed:F1}s");
            }

            // --- Step 15 (native 711-713): Load complete ---
            // Fires ~20 Persistence loaders, ConfigurationReplicator, NPC handlers, etc.
            timeline.Mark("onLoadComplete");
            loadManager.onLoadComplete?.Invoke();

            // --- Step 16 (native 715-720): Finalize ---
            yield return new WaitForSeconds(NativeInvariants.POST_LOAD_DELAY_SECONDS);
            loadManager.LoadStatus = LoadManager.ELoadStatus.None;
            if (Singleton<LoadingScreen>.InstanceExists)
                Singleton<LoadingScreen>.Instance.Close();
            loadManager.IsLoading = false;
            loadManager.IsGameLoaded = true;

            IsConnectedToDedicatedServer = true;
            IsConnecting = false;

            timeline.Mark("ClientLoadComplete");
            timeline.PrintSummary();

            try { ModManager.NotifyConnectedToServer(); }
            catch (Exception ex) { logger.Warning($"NotifyConnectedToServer error: {ex.Message}"); }
        }

        /// <summary>
        /// Configures Tugboat transport and starts the client connection.
        /// Matches native LoadAsClient transport setup (lines 672-691).
        /// </summary>
        private bool ConfigureAndStartTransport()
        {
            var networkManager = InstanceFinder.NetworkManager;
            if (networkManager == null)
            {
                logger.Error("NetworkManager not found");
                return false;
            }

            var multipass = networkManager.TransportManager.Transport as Multipass;
            if (multipass == null)
            {
                logger.Error("Multipass transport not found");
                return false;
            }

            var tugboat = multipass.gameObject.GetComponent<Tugboat>();
            if (tugboat == null)
            {
                logger.Error("Tugboat component not found");
                return false;
            }

            // Set Tugboat as Multipass client transport (native: SetClientTransport<Tugboat>())
            if (!ClientTransportPatcher.SetMultipassClientTransport(multipass, tugboat))
                logger.Warning("Could not set client transport via reflection");

            // Set transport timeout (native: SetTimeout(30f, asServer: false))
            try
            {
                networkManager.TransportManager.Transport.SetTimeout(
                    NativeInvariants.TRANSPORT_TIMEOUT_SECONDS, asServer: false);
            }
            catch (Exception ex)
            {
                logger.Warning($"Failed to set transport timeout: {ex.Message}");
            }

            // Configure Tugboat address and port
            tugboat.SetClientAddress(_targetServerIP);
            tugboat.SetPort((ushort)_targetServerPort);

            // Start connection via ClientManager (ensures Multipass TransportIdData registration)
            logger.Msg($"Starting client connection to {_targetServerIP}:{_targetServerPort} via ClientManager");
            bool started = networkManager.ClientManager.StartConnection();
            if (!started)
                logger.Error("ClientManager.StartConnection() returned false");

            return started;
        }

        private static void OnLocalPlayerSpawned()
        {
#if MONO
            Player.onLocalPlayerSpawned = (Action)Delegate.Remove(
                Player.onLocalPlayerSpawned, LocalPlayerSpawnedHandler);
#endif
        }

        private void HandleConnectionError(string errorMessage)
        {
            logger.Error($"Connection failed: {errorMessage}");
            LastConnectionError = errorMessage;
            IsConnecting = false;
            IsConnectedToDedicatedServer = false;
            _isTugboatMode = false;

            try
            {
                if (InstanceFinder.IsClient)
                    InstanceFinder.ClientManager?.StopConnection();

                ClientBootstrap.Instance?.AuthManager?.OnDisconnected();

                var loadManager = Singleton<LoadManager>.Instance;
                if (loadManager != null)
                {
                    loadManager.LoadStatus = LoadManager.ELoadStatus.None;
                    loadManager.IsLoading = false;
                }

                if (Singleton<LoadingScreen>.InstanceExists)
                    Singleton<LoadingScreen>.Instance.Close();
            }
            catch (Exception cleanupEx)
            {
                logger.Error($"Error during connection error cleanup: {cleanupEx}");
            }
        }

        public void DisconnectFromDedicatedServer()
        {
            try
            {
                logger.Msg("Disconnecting from dedicated server");

                if (InstanceFinder.IsClient)
                    InstanceFinder.ClientManager?.StopConnection();

                ClientBootstrap.Instance?.AuthManager?.OnDisconnected();

                IsConnectedToDedicatedServer = false;
                _isTugboatMode = false;

                logger.Msg("Disconnected from dedicated server");
                try { ModManager.NotifyDisconnectedFromServer(); }
                catch (Exception ex) { logger.Warning($"NotifyDisconnectedFromServer error: {ex.Message}"); }
            }
            catch (Exception ex)
            {
                logger.Error($"Error disconnecting: {ex}");
            }
        }

        public string GetConnectionStatus()
        {
            try
            {
                var status = "=== Connection Status ===\n";
                status += $"Is Connecting: {IsConnecting}\n";
                status += $"Connected to Dedicated Server: {IsConnectedToDedicatedServer}\n";
                status += $"Tugboat Mode: {_isTugboatMode}\n";
                status += $"Target Server: {_targetServerIP}:{_targetServerPort}\n";
                status += $"FishNet Is Client: {InstanceFinder.IsClient}\n";
                status += $"FishNet Is Server: {InstanceFinder.IsServer}\n";
                status += $"Current Scene: {SceneManager.GetActiveScene().name}\n";
                status += $"Player Local: {(Player.Local != null ? "Spawned" : "Not Spawned")}\n";

                if (!string.IsNullOrEmpty(LastConnectionError))
                    status += $"Last Error: {LastConnectionError}\n";

                var loadManager = Singleton<LoadManager>.Instance;
                if (loadManager != null)
                {
                    status += $"LoadManager Status: {loadManager.LoadStatus}\n";
                    status += $"Is Loading: {loadManager.IsLoading}\n";
                    status += $"Is Game Loaded: {loadManager.IsGameLoaded}\n";
                }

                return status;
            }
            catch (Exception ex)
            {
                return $"Error getting status: {ex.Message}";
            }
        }

        public static bool IsTugboatMode => _isTugboatMode;

        public static (string ip, int port) GetTargetServer() => (_targetServerIP, _targetServerPort);

        public void SetTargetServer(string ip, int port)
        {
            if (string.IsNullOrWhiteSpace(ip))
                throw new ArgumentException("IP cannot be empty");
            if (port <= 0 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535");

            _targetServerIP = ip.Trim();
            _targetServerPort = port;
            logger.Msg($"Target server updated to {_targetServerIP}:{_targetServerPort}");
        }
    }
}
