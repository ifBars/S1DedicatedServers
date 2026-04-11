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
using Il2CppScheduleOne.Audio;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.MainMenu;
#else
using ScheduleOne.Audio;
using ScheduleOne.DevUtilities;
using ScheduleOne.Networking;
using ScheduleOne.Persistence;
using ScheduleOne.PlayerScripts;
using ScheduleOne.UI;
using ScheduleOne.UI.MainMenu;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DedicatedServerMod.API;
using DedicatedServerMod.API.Client;
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
        private static string _targetServerIP = "localhost";
        private static int _targetServerPort = 38465;
        private static bool _isTugboatMode = false;

        private static readonly MethodInfo CleanUpMethod =
            typeof(LoadManager).GetMethod("CleanUp", BindingFlags.NonPublic | BindingFlags.Instance);

        private bool _worldLoadCompleted;
        private bool _authSucceeded;
        private bool _modVerificationSucceeded;
        private bool _joinCompletionNotified;
        private bool _isReturningToMenu;
        private bool _isConnectionStateHooked;
        private string _pendingDisconnectTitle;
        private string _pendingDisconnectReason;
        private readonly List<JoinPreparationRegistrationEntry> _joinPreparationRegistrations = new List<JoinPreparationRegistrationEntry>();
        private long _nextJoinPreparationOrder;

        public bool IsConnecting { get; private set; }
        public bool IsConnectedToDedicatedServer { get; private set; }
        public string LastConnectionError { get; private set; }
        public bool ShouldBlockLoadingScreenClose => IsConnecting && !_isReturningToMenu && _worldLoadCompleted && (!_authSucceeded || !_modVerificationSucceeded);

        public event Action<string, int> DedicatedServerConnected;

        internal ClientConnectionManager()
        {
        }

        internal void Initialize()
        {
            try
            {
                DebugLog.Info("Initializing ClientConnectionManager");
                ParseCommandLineArguments();
                CustomMessaging.ClientMessageReceived += OnClientMessageReceived;
                TryHookConnectionState();
                DebugLog.Info("ClientConnectionManager initialized");
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Failed to initialize ClientConnectionManager: {ex}");
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
                    DebugLog.Info($"Target server IP set to: {_targetServerIP}");
                }
                if (args[i] == "--server-port" && i + 1 < args.Length)
                {
                    if (int.TryParse(args[i + 1], out int port))
                    {
                        _targetServerPort = port;
                        DebugLog.Info($"Target server port set to: {_targetServerPort}");
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
                DebugLog.Warning("Connection already in progress");
                return;
            }

            TryHookConnectionState();
            DebugLog.Info($"Starting dedicated server connection to {_targetServerIP}:{_targetServerPort}");
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
            var timeline = new JoinTimeline();
            timeline.Mark("Begin");

            var loadManager = Singleton<LoadManager>.Instance;
            if (loadManager == null)
            {
                HandleConnectionError("LoadManager not found");
                yield break;
            }

            yield return RunConnectionPreparationRegistrations();
            if (TryGetConnectionPreparationError(out string preparationError))
            {
                HandleConnectionError(preparationError);
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
                Player.onLocalPlayerSpawned, new Action(OnLocalPlayerSpawned));
#else
            Player.onLocalPlayerSpawned -= new Action(OnLocalPlayerSpawned);
            Player.onLocalPlayerSpawned += new Action(OnLocalPlayerSpawned);
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

            if (!FinalizeConnectionPreparationRegistrations(out string finalizeError))
            {
                timeline.MarkError("ConnectionPreparationFinalize", finalizeError ?? "finalization failed");
                HandleConnectionError(finalizeError ?? "Failed to finalize prepared connection content.");
                yield break;
            }

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
            yield return new WaitUntil((System.Func<bool>)(() =>
                Player.Local == null || Player.Local.playerDataRetrieveReturned));
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
            yield return new WaitUntil((System.Func<bool>)(() =>
                NetworkSingleton<ReplicationQueue>.InstanceExists &&
                (NetworkSingleton<ReplicationQueue>.Instance.ReplicationDoneForLocalPlayer ||
                 NetworkSingleton<ReplicationQueue>.Instance.LocalPlayerReplicationTimedOut)));

            var replicationQueue = NetworkSingleton<ReplicationQueue>.Instance;
            if (replicationQueue.LocalPlayerReplicationTimedOut)
            {
                var task = replicationQueue.CurrentReplicationTask;
                timeline.Mark("ReplicationTimeout", task);
                DebugLog.Warning($"Replication timed out. Current task: {task}");
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
                DebugLog.Error("NetworkManager not found");
                return false;
            }

            var multipass = networkManager.TransportManager.Transport as Multipass;
            if (multipass == null)
            {
                DebugLog.Error("Multipass transport not found");
                return false;
            }

            var tugboat = multipass.gameObject.GetComponent<Tugboat>();
            if (tugboat == null)
            {
                DebugLog.Error("Tugboat component not found");
                return false;
            }

            // Set Tugboat as Multipass client transport (native: SetClientTransport<Tugboat>())
            if (!ClientTransportPatcher.SetMultipassClientTransport(multipass, tugboat))
                DebugLog.Warning("Could not set client transport via reflection");

            // Set transport timeout (native: SetTimeout(30f, asServer: false))
            try
            {
                networkManager.TransportManager.Transport.SetTimeout(
                    NativeInvariants.TRANSPORT_TIMEOUT_SECONDS, asServer: false);
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"Failed to set transport timeout: {ex.Message}");
            }

            // Configure Tugboat address and port
            tugboat.SetClientAddress(_targetServerIP);
            tugboat.SetPort((ushort)_targetServerPort);

            // Start connection via ClientManager (ensures Multipass TransportIdData registration)
            DebugLog.Info($"Starting client connection to {_targetServerIP}:{_targetServerPort} via ClientManager");
            bool started = networkManager.ClientManager.StartConnection();
            if (!started)
                DebugLog.Error("ClientManager.StartConnection() returned false");

            return started;
        }

        private static void OnLocalPlayerSpawned()
        {
#if MONO
            Player.onLocalPlayerSpawned = (Action)Delegate.Remove(
                Player.onLocalPlayerSpawned, new Action(OnLocalPlayerSpawned));
#else
            Player.onLocalPlayerSpawned -= new Action(OnLocalPlayerSpawned);
#endif
        }

        private void HandleConnectionError(string errorMessage)
        {
            DebugLog.Error($"Connection failed: {errorMessage}");
            LastConnectionError = errorMessage;
            ReturnToMenu(errorMessage, isFailure: true, requestPlayerSave: false);
        }

        public void DisconnectFromDedicatedServer()
        {
            try
            {
                DebugLog.Info("Disconnecting from dedicated server");
                ReturnToMenu("Disconnected from dedicated server", isFailure: false, requestPlayerSave: false);
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error disconnecting: {ex}");
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
                DebugLog.Info("Authentication succeeded; waiting for mod verification and world load completion");
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
                DebugLog.Info("Client mod verification succeeded before world load finalized; waiting to complete join");
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
            ResetConnectionPreparationRegistrations();
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

            DebugLog.Info("Dedicated server join fully completed after authentication and client mod verification");

            try
            {
                DedicatedServerConnected?.Invoke(_targetServerIP, _targetServerPort);
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"DedicatedServerConnected callback error: {ex.Message}");
            }

            try
            {
                ModManager.NotifyConnectedToServer();
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"NotifyConnectedToServer error: {ex.Message}");
            }

            try
            {
                ModManager.NotifyClientPlayerReady();
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"NotifyClientPlayerReady error: {ex.Message}");
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

        private string GetUnexpectedDisconnectReason()
        {
            if (IsConnecting)
            {
                if (!_worldLoadCompleted)
                {
                    if (!_authSucceeded)
                    {
                        return "Lost connection while joining the dedicated server before authentication completed.";
                    }

                    if (!_modVerificationSucceeded)
                    {
                        return "Lost connection while joining the dedicated server before client mod verification completed.";
                    }

                    return "Lost connection while joining the dedicated server before world load completed.";
                }

                if (!_authSucceeded)
                {
                    return "Lost connection during authentication.";
                }

                if (!_modVerificationSucceeded)
                {
                    return "Lost connection during client mod verification.";
                }

                return "Lost connection while finalizing the dedicated server join.";
            }

            if (IsConnectedToDedicatedServer)
            {
                return "Connection to the dedicated server was lost unexpectedly.";
            }

            return "Disconnected from dedicated server.";
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
                    DebugLog.Warning($"Failed to request player save before returning to menu: {ex.Message}");
                }

                if (waitForSave)
                {
                    yield return new WaitUntil((System.Func<bool>)(() =>
                        Player.Local == null ||
                        Player.Local.playerSaveRequestReturned ||
                        Time.realtimeSinceStartup - saveWaitStart > 2f));
                }
            }

            try
            {
                var loadManager = Singleton<LoadManager>.Instance;
                if (loadManager != null)
                {
                    if (Singleton<MusicManager>.InstanceExists)
                    {
                        Singleton<MusicManager>.Instance.StopAndDisableTracks();
                    }

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
                DebugLog.Warning($"Error invoking pre-scene change during return to menu: {ex.Message}");
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
                DebugLog.Warning($"Error stopping client connection during return to menu: {ex.Message}");
            }

            ClientBootstrap.Instance?.AuthManager?.OnDisconnected();
            ClientBootstrap.Instance?.ModVerificationManager?.OnDisconnected();

            IsConnecting = false;
            IsConnectedToDedicatedServer = false;
            ServerDataStore.Reset();
            PermissionSnapshotStore.Reset();
            ResetConnectionPreparationRegistrations();
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
                DebugLog.Warning($"Error finalizing return to menu: {ex.Message}");
            }

            if (wasConnected)
            {
                try
                {
                    ModManager.NotifyDisconnectedFromServer();
                }
                catch (Exception ex)
                {
                    DebugLog.Warning($"NotifyDisconnectedFromServer error: {ex.Message}");
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
                DebugLog.Warning($"Error restoring menu cursor state: {ex.Message}");
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
                DebugLog.Info($"Received disconnect notice: {_pendingDisconnectTitle} - {_pendingDisconnectReason}");
            }
            catch (JsonException ex)
            {
                DebugLog.Warning($"Failed to parse disconnect notice: {ex.Message}");
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
            InstanceFinder.ClientManager.OnClientConnectionState -= new Action<ClientConnectionStateArgs>(OnClientConnectionState);
            InstanceFinder.ClientManager.OnClientConnectionState += new Action<ClientConnectionStateArgs>(OnClientConnectionState);
            _isConnectionStateHooked = true;
#else
            InstanceFinder.ClientManager.OnClientConnectionState -= OnClientConnectionState;
            InstanceFinder.ClientManager.OnClientConnectionState += OnClientConnectionState;
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
                DebugLog.Warning("Dedicated server connection stopped unexpectedly; returning to menu");
                ReturnToMenu(GetUnexpectedDisconnectReason(), isFailure: true, requestPlayerSave: false);
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
            DebugLog.Info($"Target server updated to {_targetServerIP}:{_targetServerPort}");
        }

        /// <summary>
        /// Registers callbacks that participate in the dedicated-server join preparation pipeline.
        /// </summary>
        /// <param name="registrationId">Stable identifier for this registration.</param>
        /// <param name="configure">Fluent registration builder.</param>
        /// <returns>A disposable registration handle.</returns>
        public ClientJoinPreparationRegistration RegisterJoinPreparation(string registrationId, Action<ClientJoinPreparationBuilder> configure)
        {
            if (string.IsNullOrWhiteSpace(registrationId))
                throw new ArgumentException("Registration id cannot be empty.", nameof(registrationId));
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            ClientJoinPreparationBuilder builder = new ClientJoinPreparationBuilder(registrationId);
            configure(builder);

            JoinPreparationRegistrationEntry entry = new JoinPreparationRegistrationEntry(
                Guid.NewGuid(),
                builder.RegistrationId,
                builder.Priority,
                _nextJoinPreparationOrder++,
                builder.PrepareCallback,
                builder.FinalizeCallback,
                builder.ResetCallback);

            lock (_joinPreparationRegistrations)
            {
                _joinPreparationRegistrations.RemoveAll(existing =>
                    string.Equals(existing.RegistrationId, builder.RegistrationId, StringComparison.OrdinalIgnoreCase));
                _joinPreparationRegistrations.Add(entry);
            }

            return new ClientJoinPreparationRegistration(this, entry.Token, entry.RegistrationId);
        }

        internal void UnregisterJoinPreparation(Guid token)
        {
            lock (_joinPreparationRegistrations)
            {
                _joinPreparationRegistrations.RemoveAll(entry => entry.Token == token);
            }
        }

        private IEnumerator RunConnectionPreparationRegistrations()
        {
            foreach (JoinPreparationRegistrationEntry entry in GetJoinPreparationRegistrationsSnapshot())
            {
                IEnumerator routine = null;
                ClientJoinPreparationContext context = entry.BeginAttempt(_targetServerIP, _targetServerPort);

                try
                {
                    routine = entry.PrepareCallback?.Invoke(context);
                }
                catch (Exception ex)
                {
                    HandleConnectionError($"Client join preparation '{entry.RegistrationId}' failed during prepare: {ex.Message}");
                    yield break;
                }

                if (routine != null)
                {
                    yield return routine;
                }

                if (context.HasFailed)
                {
                    yield break;
                }
            }
        }

        private bool FinalizeConnectionPreparationRegistrations(out string errorMessage)
        {
            foreach (JoinPreparationRegistrationEntry entry in GetJoinPreparationRegistrationsSnapshot())
            {
                ClientJoinPreparationContext context = entry.GetOrCreateContext(_targetServerIP, _targetServerPort);
                context.ClearFailure();

                try
                {
                    entry.FinalizeCallback?.Invoke(context);
                    if (!context.HasFailed)
                    {
                        continue;
                    }

                    errorMessage = string.IsNullOrWhiteSpace(context.FailureReason)
                        ? $"Client join preparation '{entry.RegistrationId}' failed during finalize."
                        : context.FailureReason;
                    return false;
                }
                catch (Exception ex)
                {
                    errorMessage = $"Client join preparation '{entry.RegistrationId}' failed during finalize: {ex.Message}";
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }

        private void ResetConnectionPreparationRegistrations()
        {
            foreach (JoinPreparationRegistrationEntry entry in GetJoinPreparationRegistrationsSnapshot())
            {
                ClientJoinPreparationContext context = entry.GetOrCreateContext(_targetServerIP, _targetServerPort);

                try
                {
                    entry.ResetCallback?.Invoke(context);
                }
                catch (Exception ex)
                {
                    DebugLog.Warning($"Client join preparation '{entry.RegistrationId}' failed during reset: {ex.Message}");
                }

                entry.ClearAttempt();
            }
        }

        private bool TryGetConnectionPreparationError(out string errorMessage)
        {
            foreach (JoinPreparationRegistrationEntry entry in GetJoinPreparationRegistrationsSnapshot())
            {
                string failureReason = entry.ActiveContext?.FailureReason;
                if (string.IsNullOrWhiteSpace(failureReason))
                {
                    continue;
                }

                errorMessage = failureReason;
                return true;
            }

            errorMessage = null;
            return false;
        }

        private List<JoinPreparationRegistrationEntry> GetJoinPreparationRegistrationsSnapshot()
        {
            lock (_joinPreparationRegistrations)
            {
                return _joinPreparationRegistrations
                    .OrderByDescending(entry => entry.Priority)
                    .ThenBy(entry => entry.Order)
                    .ToList();
            }
        }

        private sealed class JoinPreparationRegistrationEntry
        {
            public JoinPreparationRegistrationEntry(
                Guid token,
                string registrationId,
                int priority,
                long order,
                Func<ClientJoinPreparationContext, IEnumerator> prepareCallback,
                Action<ClientJoinPreparationContext> finalizeCallback,
                Action<ClientJoinPreparationContext> resetCallback)
            {
                Token = token;
                RegistrationId = registrationId;
                Priority = priority;
                Order = order;
                PrepareCallback = prepareCallback;
                FinalizeCallback = finalizeCallback;
                ResetCallback = resetCallback;
            }

            public Guid Token { get; }

            public string RegistrationId { get; }

            public int Priority { get; }

            public long Order { get; }

            public Func<ClientJoinPreparationContext, IEnumerator> PrepareCallback { get; }

            public Action<ClientJoinPreparationContext> FinalizeCallback { get; }

            public Action<ClientJoinPreparationContext> ResetCallback { get; }

            public ClientJoinPreparationContext ActiveContext { get; private set; }

            public ClientJoinPreparationContext BeginAttempt(string host, int port)
            {
                ActiveContext = new ClientJoinPreparationContext(host, port);
                return ActiveContext;
            }

            public ClientJoinPreparationContext GetOrCreateContext(string host, int port)
            {
                if (ActiveContext == null)
                {
                    ActiveContext = new ClientJoinPreparationContext(host, port);
                }

                return ActiveContext;
            }

            public void ClearAttempt()
            {
                ActiveContext = null;
            }
        }
    }
}
