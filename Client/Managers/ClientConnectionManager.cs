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
using Il2CppScheduleOne.UI.MainMenu;
#else
using ScheduleOne.DevUtilities;
using ScheduleOne.Networking;
using ScheduleOne.Persistence;
using ScheduleOne.PlayerScripts;
using ScheduleOne.UI;
using ScheduleOne.UI.MainMenu;
#endif
using System;
using System.Collections;
using System.Reflection;
using DedicatedServerMod.API;
using DedicatedServerMod.Client.Core;
using DedicatedServerMod.Client.Permissions;
using DedicatedServerMod.Client.Patchers;
using DedicatedServerMod.Shared.Networking;
using DedicatedServerMod.Utils;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DedicatedServerMod.Client.Managers
{
    /// <summary>
    /// Manages the dedicated server client join sequence, mirroring the native
    /// LoadManager.LoadAsClient flow step-by-step. See <see cref="NativeInvariants"/>
    /// for the full contract this class must satisfy.
    /// </summary>
    public sealed class ClientConnectionManager
    {
        private readonly MelonLogger.Instance logger;

        private static string _targetServerIP = "localhost";
        private static int _targetServerPort = 38465;
        private static bool _isTugboatMode = false;

        private static readonly MethodInfo CleanUpMethod =
            typeof(LoadManager).GetMethod("CleanUp", BindingFlags.NonPublic | BindingFlags.Instance);
#if MONO
        private static readonly Action LocalPlayerSpawnedHandler = OnLocalPlayerSpawned;
        private readonly Action<ClientConnectionStateArgs> _clientConnectionStateHandler;
#endif

        private bool _worldLoadCompleted;
        private bool _authSucceeded;
        private bool _modVerificationSucceeded;
        private bool _joinCompletionNotified;
        private bool _isReturningToMenu;
        private bool _isConnectionStateHooked;
        private string _pendingDisconnectTitle;
        private string _pendingDisconnectReason;

        public bool IsConnecting { get; private set; }
        public bool IsConnectedToDedicatedServer { get; private set; }
        public string LastConnectionError { get; private set; }
        public bool ShouldBlockLoadingScreenClose => IsConnecting && !_isReturningToMenu && _worldLoadCompleted && (!_authSucceeded || !_modVerificationSucceeded);

        public event Action<string, int> DedicatedServerConnected;

        internal ClientConnectionManager(MelonLogger.Instance logger)
        {
            this.logger = logger;
#if MONO
            _clientConnectionStateHandler = OnClientConnectionState;
#endif
        }

        internal void Initialize()
        {
            try
            {
                logger.Msg("Initializing ClientConnectionManager");
                ParseCommandLineArguments();
                CustomMessaging.ClientMessageReceived += OnClientMessageReceived;
                TryHookConnectionState();
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

            TryHookConnectionState();
            logger.Msg($"Starting dedicated server connection to {_targetServerIP}:{_targetServerPort}");
            ServerDataStore.Reset();
            PermissionSnapshotStore.Reset();
            _worldLoadCompleted = false;
            _authSucceeded = false;
            _modVerificationSucceeded = false;
            _joinCompletionNotified = false;
            _isReturningToMenu = false;
            IsConnecting = true;
            IsConnectedToDedicatedServer = false;
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
            float dataWaitStart = Time.realtimeSinceStartup;
            yield return new WaitUntil(() => Player.Local == null || Player.Local.playerDataRetrieveReturned);
            if (Player.Local == null)
            {
                timeline.MarkError("PlayerDataReceive", "Player.Local was lost before data retrieval completed");
                HandleConnectionError("Local player was destroyed while waiting for player data");
                yield break;
            }
            timeline.Mark("PlayerDataReceived", $"{(Time.realtimeSinceStartup - dataWaitStart):F1}s");

            // --- Step 14 (native 705-709): Wait for replication ---
            // ReplicationQueue.ReplicationDoneForLocalPlayer set by TargetRpc from server,
            // or LocalPlayerReplicationTimedOut after 45s.
            loadManager.LoadStatus = LoadManager.ELoadStatus.Initializing;
            timeline.Mark("WaitForReplication");
            float replicationWaitStart = Time.realtimeSinceStartup;
            yield return new WaitUntil(() =>
                NetworkSingleton<ReplicationQueue>.InstanceExists &&
                (NetworkSingleton<ReplicationQueue>.Instance.ReplicationDoneForLocalPlayer ||
                 NetworkSingleton<ReplicationQueue>.Instance.LocalPlayerReplicationTimedOut));

            var replicationQueue = NetworkSingleton<ReplicationQueue>.Instance;
            if (replicationQueue.LocalPlayerReplicationTimedOut)
            {
                var task = replicationQueue.CurrentReplicationTask;
                timeline.Mark("ReplicationTimeout", task);
                logger.Warning($"Replication timed out. Current task: {task}");
            }
            else
            {
                timeline.Mark("ReplicationComplete", $"{(Time.realtimeSinceStartup - replicationWaitStart):F1}s");
            }

            // --- Step 15 (native 711-713): Load complete ---
            // Fires ~20 Persistence loaders, ConfigurationReplicator, NPC handlers, etc.
            timeline.Mark("onLoadComplete");
            loadManager.onLoadComplete?.Invoke();

            // --- Step 16 (native 715-720): Finalize ---
            yield return new WaitForSeconds(NativeInvariants.POST_LOAD_DELAY_SECONDS);
            loadManager.LoadStatus = LoadManager.ELoadStatus.None;
            loadManager.IsLoading = false;
            loadManager.IsGameLoaded = true;
            _worldLoadCompleted = true;

            timeline.Mark("ClientLoadComplete");
            timeline.PrintSummary();

            if (_authSucceeded && _modVerificationSucceeded)
                CompleteJoinAfterVerification();
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
            ReturnToMenu(errorMessage, isFailure: true, requestPlayerSave: false);
        }

        public void DisconnectFromDedicatedServer()
        {
            try
            {
                logger.Msg("Disconnecting from dedicated server");
                ReturnToMenu("Disconnected from dedicated server", isFailure: false, requestPlayerSave: false);
            }
            catch (Exception ex)
            {
                logger.Error($"Error disconnecting: {ex}");
            }
        }

        internal void OnAuthenticationSucceeded(string message)
        {
            _authSucceeded = true;

            if (_worldLoadCompleted && _modVerificationSucceeded)
            {
                CompleteJoinAfterVerification();
            }
            else
            {
                logger.Msg("Authentication succeeded; waiting for mod verification and world load completion");
            }
        }

        internal void OnAuthenticationFailed(string reason)
        {
            ReturnToMenu(reason, isFailure: true, requestPlayerSave: false);
        }

        internal void OnModVerificationSucceeded(string message)
        {
            _modVerificationSucceeded = true;

            if (_worldLoadCompleted && _authSucceeded)
            {
                CompleteJoinAfterVerification();
            }
            else
            {
                logger.Msg("Client mod verification succeeded before world load finalized; waiting to complete join");
            }
        }

        internal void OnModVerificationFailed(string reason)
        {
            ReturnToMenu(reason, isFailure: true, requestPlayerSave: false);
        }

        internal void Shutdown()
        {
            CustomMessaging.ClientMessageReceived -= OnClientMessageReceived;
            PermissionSnapshotStore.Reset();
        }

        private void CompleteJoinAfterVerification()
        {
            if (_joinCompletionNotified || !_worldLoadCompleted || !_authSucceeded || !_modVerificationSucceeded)
            {
                return;
            }

            _joinCompletionNotified = true;
            IsConnectedToDedicatedServer = true;
            IsConnecting = false;

            if (Singleton<LoadingScreen>.InstanceExists)
            {
                Singleton<LoadingScreen>.Instance.Close();
            }

            logger.Msg("Dedicated server join fully completed after authentication and client mod verification");

            try
            {
                DedicatedServerConnected?.Invoke(_targetServerIP, _targetServerPort);
            }
            catch (Exception ex)
            {
                logger.Warning($"DedicatedServerConnected callback error: {ex.Message}");
            }

            try
            {
                ModManager.NotifyConnectedToServer();
            }
            catch (Exception ex)
            {
                logger.Warning($"NotifyConnectedToServer error: {ex.Message}");
            }

            try
            {
                ModManager.NotifyClientPlayerReady();
            }
            catch (Exception ex)
            {
                logger.Warning($"NotifyClientPlayerReady error: {ex.Message}");
            }
        }

        private void ReturnToMenu(string reason, bool isFailure, bool requestPlayerSave)
        {
            if (_isReturningToMenu)
            {
                return;
            }

            MelonCoroutines.Start(ReturnToMenuCoroutine(reason, isFailure, requestPlayerSave));
        }

        private IEnumerator ReturnToMenuCoroutine(string reason, bool isFailure, bool requestPlayerSave)
        {
            _isReturningToMenu = true;
            bool wasConnected = IsConnectedToDedicatedServer;
            string popupTitle = isFailure ? "Disconnected" : string.Empty;
            string popupReason = reason ?? string.Empty;

            if (isFailure && !string.IsNullOrWhiteSpace(_pendingDisconnectReason))
            {
                popupTitle = string.IsNullOrWhiteSpace(_pendingDisconnectTitle) ? "Disconnected" : _pendingDisconnectTitle;
                popupReason = _pendingDisconnectReason;
            }

            if (isFailure && !string.IsNullOrWhiteSpace(popupReason))
            {
                LastConnectionError = popupReason;
            }

            if (Singleton<LoadingScreen>.InstanceExists)
            {
                Singleton<LoadingScreen>.Instance.Open();
            }

            if (requestPlayerSave && Player.Local != null)
            {
                bool waitForSave = false;
                float saveWaitStart = 0f;

                try
                {
                    Player.Local.RequestSavePlayer();
                    saveWaitStart = Time.realtimeSinceStartup;
                    waitForSave = true;
                }
                catch (Exception ex)
                {
                    logger.Warning($"Failed to request player save before returning to menu: {ex.Message}");
                }

                if (waitForSave)
                {
                    yield return new WaitUntil(() => Player.Local == null || Player.Local.playerSaveRequestReturned || Time.realtimeSinceStartup - saveWaitStart > 2f);
                }
            }

            try
            {
                var loadManager = Singleton<LoadManager>.Instance;
                if (loadManager != null)
                {
                    loadManager.LoadStatus = LoadManager.ELoadStatus.None;
                    loadManager.IsLoading = true;
                    loadManager.IsGameLoaded = false;
                    loadManager.ActiveSaveInfo = null;
                    loadManager.onPreSceneChange?.Invoke();
                }

                RestoreMenuCursorState();
            }
            catch (Exception ex)
            {
                logger.Warning($"Error invoking pre-scene change during return to menu: {ex.Message}");
            }

            try
            {
                if (InstanceFinder.IsClient)
                {
                    InstanceFinder.ClientManager?.StopConnection();
                }
            }
            catch (Exception ex)
            {
                logger.Warning($"Error stopping client connection during return to menu: {ex.Message}");
            }

            ClientBootstrap.Instance?.AuthManager?.OnDisconnected();
            ClientBootstrap.Instance?.ModVerificationManager?.OnDisconnected();

            IsConnecting = false;
            IsConnectedToDedicatedServer = false;
            ServerDataStore.Reset();
            PermissionSnapshotStore.Reset();
            _worldLoadCompleted = false;
            _authSucceeded = false;
            _modVerificationSucceeded = false;
            _joinCompletionNotified = false;
            _isTugboatMode = false;

            if (SceneManager.GetActiveScene().name != "Menu")
            {
                AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("Menu");
                while (asyncLoad != null && !asyncLoad.isDone)
                {
                    yield return null;
                }
            }

            yield return null;

            try
            {
                var loadManager = Singleton<LoadManager>.Instance;
                loadManager?.RefreshSaveInfo();

                if (Singleton<LoadingScreen>.InstanceExists)
                {
                    Singleton<LoadingScreen>.Instance.Close();
                }

                if (loadManager != null)
                {
                    loadManager.IsLoading = false;
                    loadManager.LoadStatus = LoadManager.ELoadStatus.None;
                }

                if (isFailure && !string.IsNullOrWhiteSpace(popupReason) && Singleton<MainMenuPopup>.InstanceExists)
                {
                    Singleton<MainMenuPopup>.Instance.Open(new MainMenuPopup.Data(string.IsNullOrWhiteSpace(popupTitle) ? "Disconnected" : popupTitle, popupReason, true));
                }
            }
            catch (Exception ex)
            {
                logger.Warning($"Error finalizing return to menu: {ex.Message}");
            }

            if (wasConnected)
            {
                try
                {
                    ModManager.NotifyDisconnectedFromServer();
                }
                catch (Exception ex)
                {
                    logger.Warning($"NotifyDisconnectedFromServer error: {ex.Message}");
                }
            }

            ClearPendingDisconnectNotice();
            _isReturningToMenu = false;
        }

        internal void RestoreMenuCursorState()
        {
            try
            {
                if (PlayerSingleton<PlayerCamera>.InstanceExists)
                {
                    PlayerSingleton<PlayerCamera>.Instance.FreeMouse();
                }

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                if (Singleton<CursorManager>.InstanceExists)
                {
                    Singleton<CursorManager>.Instance.SetCursorAppearance(CursorManager.ECursorType.Default);
                }
            }
            catch (Exception ex)
            {
                logger.Warning($"Error restoring menu cursor state: {ex.Message}");
            }
        }

        private void OnClientMessageReceived(string command, string data)
        {
            if (!string.Equals(command, Constants.Messages.DisconnectNotice, StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                DisconnectNoticeMessage notice = JsonConvert.DeserializeObject<DisconnectNoticeMessage>(data ?? string.Empty);
                if (notice == null || string.IsNullOrWhiteSpace(notice.Message))
                {
                    return;
                }

                _pendingDisconnectTitle = string.IsNullOrWhiteSpace(notice.Title) ? "Disconnected" : notice.Title;
                _pendingDisconnectReason = notice.Message;
                logger.Msg($"Received disconnect notice: {_pendingDisconnectTitle} - {_pendingDisconnectReason}");
            }
            catch (JsonException ex)
            {
                logger.Warning($"Failed to parse disconnect notice: {ex.Message}");
            }
        }

        private void ClearPendingDisconnectNotice()
        {
            _pendingDisconnectTitle = null;
            _pendingDisconnectReason = null;
        }

        private void TryHookConnectionState()
        {
            if (_isConnectionStateHooked || InstanceFinder.ClientManager == null)
            {
                return;
            }

#if IL2CPP
            logger.Msg("Skipping direct client connection state hook on IL2CPP runtime");
            _isConnectionStateHooked = true;
#else
            InstanceFinder.ClientManager.OnClientConnectionState += _clientConnectionStateHandler;
            _isConnectionStateHooked = true;
#endif
        }

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Stopped || _isReturningToMenu)
            {
                return;
            }

            if (IsConnecting || IsConnectedToDedicatedServer)
            {
                logger.Warning("Dedicated server connection stopped unexpectedly; returning to menu");
                ReturnToMenu("Disconnected from dedicated server", isFailure: true, requestPlayerSave: false);
            }
        }

        internal string GetConnectionStatus()
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
                status += $"Auth Succeeded: {_authSucceeded}\n";
                status += $"Mod Verification Succeeded: {_modVerificationSucceeded}\n";

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

        internal static bool IsTugboatMode => _isTugboatMode;

        internal static (string ip, int port) GetTargetServer() => (_targetServerIP, _targetServerPort);

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
