using MelonLoader;
using DedicatedServerMod.Client;
using HarmonyLib;
using ScheduleOne.PlayerScripts;
using System.Reflection;
using System;
using DedicatedServerMod.Shared;
using DedicatedServerMod.API;
using ScheduleOne.UI;

[assembly: MelonInfo(typeof(DedicatedServerMod.Client.Core), "DedicatedServerClient", "1.0.0", "Bars")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace DedicatedServerMod.Client
{
    /// <summary>
    /// Main entry point for the Dedicated Server Client mod.
    /// Minimal responsibilities - delegates to specialized managers.
    /// </summary>
    public class Core : MelonMod
    {
        private static MelonLogger.Instance logger;
        private static bool _ignoreGhostHostForSleep = true; // Configurable option
        
        // Client managers
        private ClientConnectionManager connectionManager;
        private ClientTransportPatcher transportPatcher;
        private ClientPlayerSetup playerSetup;
        private ClientUIManager uiManager;
        private ClientQuestManager questManager;
        private ClientLoopbackHandler loopbackHandler;
        private ClientConsoleManager consoleManager;

        /// <summary>
        /// Gets or sets whether to ignore the ghost host when checking if all players are ready to sleep.
        /// This allows sleep cycling to work properly when connected to dedicated servers.
        /// </summary>
        public static bool IgnoreGhostHostForSleep
        {
            get => _ignoreGhostHostForSleep;
            set
            {
                _ignoreGhostHostForSleep = value;
                logger?.Msg($"Client: Ignore ghost host for sleep set to: {_ignoreGhostHostForSleep}");
            }
        }

        public override void OnInitializeMelon()
        {
            logger = LoggerInstance;
            logger.Msg("DedicatedServerClient mod initializing...");
            
            try
            {
                // Initialize API mod discovery so client mods can receive init event
                ModManager.Initialize();
                // Apply client-side patches
                ApplyClientPatches();
                
                // Initialize client time management patches
                ClientTimeManager.Initialize(logger);
                
                // Initialize all client managers
                InitializeManagers();
                
                logger.Msg("DedicatedServerClient mod initialized successfully");

                // Notify API mods: client initialized
                ModManager.NotifyClientInitialize();
            }
            catch (System.Exception ex)
            {
                logger.Error($"Failed to initialize DedicatedServerClient: {ex}");
            }
        }

        private void ApplyClientPatches()
        {
            try
            {
                var harmony = HarmonyInstance;
                
                // Patch AreAllPlayersReadyToSleep to ignore ghost host for sleep cycling
                var playerType = typeof(Player);
                var areAllPlayersReadyMethod = playerType.GetMethod("AreAllPlayersReadyToSleep", 
                    BindingFlags.Public | BindingFlags.Static);
                if (areAllPlayersReadyMethod != null)
                {
                    var prefixMethod = typeof(Core).GetMethod(nameof(AreAllPlayersReadyToSleepPrefix), 
                        BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(areAllPlayersReadyMethod, new HarmonyMethod(prefixMethod));
                    logger.Msg("Client: Patched Player.AreAllPlayersReadyToSleep to ignore ghost host");
                }

                // Patch DailySummary.Awake (postfix) to register custom messaging RPCs on client too
                var dailySummaryType = typeof(DailySummary);
                var awakeMethod = dailySummaryType.GetMethod("Awake", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (awakeMethod != null)
                {
                    var dsPostfix = typeof(Core).GetMethod(nameof(DailySummaryAwakePostfix), BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(awakeMethod, postfix: new HarmonyMethod(dsPostfix));
                    logger.Msg("Client: Patched DailySummary.Awake to register custom messaging RPCs");
                }

                // Patch SleepCanvas.SetIsOpen to respect server AllowSleeping
                var sleepCanvasType = typeof(SleepCanvas);
                var setIsOpen = sleepCanvasType.GetMethod("SetIsOpen", BindingFlags.Public | BindingFlags.Instance);
                if (setIsOpen != null)
                {
                    var prefix = typeof(Core).GetMethod(nameof(SleepCanvas_SetIsOpen_Prefix), BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(setIsOpen, new HarmonyMethod(prefix));
                    logger.Msg("Client: Patched SleepCanvas.SetIsOpen to respect server AllowSleeping");
                }

                // Apply client network RPC guards to stop client-side Observers sends for police systems
                PoliceAuthorityPatches.ApplyClient(harmony, logger);
                logger.Msg("Client: Applied police observer guards");
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to apply client patches: {ex}");
            }
        }

        // Delegate to shared messaging hub
        private static void DailySummaryAwakePostfix(DailySummary __instance)
        {
            CustomMessaging.DailySummaryAwakePostfix(__instance);
        }

        private void InitializeManagers()
        {
            // Initialize transport patcher first (needed for connection patching)
            transportPatcher = new ClientTransportPatcher(logger);
            transportPatcher.Initialize();

            // Initialize connection manager
            connectionManager = new ClientConnectionManager(logger);
            connectionManager.Initialize();

            // Subscribe for server_data delivery
            try
            {
                CustomMessaging.ClientMessageReceived += (cmd, data) =>
                {
                    if (cmd != "server_data") return;
                    try
                    {
                        var sd = Newtonsoft.Json.JsonConvert.DeserializeObject<DedicatedServerMod.Shared.ServerData>(data);
                        if (sd != null) DedicatedServerMod.Client.ServerDataStore.Update(sd);
                    }
                    catch {}
                };
            }
            catch {}

            // Initialize console manager (for admin console access)
            consoleManager = new ClientConsoleManager(logger);
            consoleManager.Initialize();

            // Initialize player setup handler
            playerSetup = new ClientPlayerSetup(logger);
            playerSetup.Initialize();

            // Initialize quest manager
            questManager = new ClientQuestManager(logger);
            questManager.Initialize();

            // Initialize loopback handler
            loopbackHandler = new ClientLoopbackHandler(logger);
            loopbackHandler.Initialize();

            // Initialize UI manager (depends on connection manager)
            uiManager = new ClientUIManager(logger, connectionManager);
            uiManager.Initialize();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            try
            {
                // Delegate scene handling to appropriate managers
                uiManager?.OnSceneLoaded(sceneName);
                questManager?.OnSceneLoaded(sceneName);
            }
            catch (System.Exception ex)
            {
                logger.Error($"Error handling scene load ({sceneName}): {ex}");
            }
        }

        public override void OnUpdate()
        {
            try
            {
                // Handle debug input
                HandleDebugInput();
            }
            catch (System.Exception ex)
            {
                logger.Error($"Error in OnUpdate: {ex}");
            }
        }

        private void HandleDebugInput()
        {
            // Debug key for testing (F9)
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F9))
            {
                logger.Msg("F9 pressed - triggering prototype connection");
                connectionManager?.StartDedicatedConnection();
            }
        }
        
        /// <summary>
        /// Access to the Core instance
        /// </summary>
        public static Core Instance { get; private set; }
        
        /// <summary>
        /// Public access to connection manager for other components
        /// </summary>
        public static ClientConnectionManager ConnectionManager => Instance?.connectionManager;

        public ClientTransportPatcher TransportPatcher => transportPatcher;
        public ClientPlayerSetup PlayerSetupManager => playerSetup;
        public ClientUIManager UIManager => uiManager;
        public ClientQuestManager QuestManager => questManager;
        public ClientLoopbackHandler LoopbackManager => loopbackHandler;
        public ClientConsoleManager ConsoleManager => consoleManager;

        public override void OnApplicationStart()
        {
            Instance = this;
        }

        /// <summary>
        /// Harmony prefix patch for Player.AreAllPlayersReadyToSleep to ignore ghost host players
        /// </summary>
        private static bool AreAllPlayersReadyToSleepPrefix(ref bool __result)
        {
            // Only apply our custom logic if the feature is enabled and we're connected to a server
            if (!_ignoreGhostHostForSleep || !FishNet.InstanceFinder.IsClient)
            {
                return true; // Let the original method run
            }

            try
            {
                // Replicate the original logic but exclude ghost loopback players
                var playerList = Player.PlayerList;
                if (playerList.Count == 0)
                {
                    __result = false;
                    return false; // Skip original method
                }

                int realPlayerCount = 0;
                int readyPlayerCount = 0;

                for (int i = 0; i < playerList.Count; i++)
                {
                    var player = playerList[i];
                    if (player == null) continue;

                    // Skip ghost loopback players (detect using similar logic to ClientLoopbackHandler)
                    if (IsGhostLoopbackPlayer(player))
                    {
                        logger?.Msg($"Client sleep check: Ignoring ghost host player: {player.PlayerName}");
                        continue;
                    }

                    realPlayerCount++;
                    
                    // Check if this real player is ready to sleep
                    if (player.IsReadyToSleep)
                    {
                        readyPlayerCount++;
                    }
                    else
                    {
                        __result = false;
                        return false; // Skip original method
                    }
                }

                logger?.Msg($"Client sleep check: All {realPlayerCount} real players are ready to sleep ({readyPlayerCount}/{realPlayerCount})");

                __result = true;
                return false; // Skip original method
            }
            catch (Exception ex)
            {
                logger?.Error($"Error in client AreAllPlayersReadyToSleep patch: {ex}");
                return true; // Let original method run as fallback
            }
        }



        /// <summary>
        /// Detect if a player is a ghost loopback player from the server
        /// </summary>
        private static bool IsGhostLoopbackPlayer(Player player)
        {
            try
            {
                // Method 1: Check by game object name (set by server)
                if (player.gameObject.name == "[DedicatedServerHostLoopback]")
                {
                    return true;
                }

                // Method 2: Check by network characteristics (similar to ClientLoopbackHandler)
                var networkObject = player.GetComponent<FishNet.Object.NetworkObject>();
                if (networkObject?.Owner != null)
                {
                    // Server loopback player characteristics:
                    // - Owner ClientId is 0 (server)
                    // - IsOwner is false (not owned by this client)
                    bool isServerLoopback = (networkObject.Owner.ClientId == 0 && !networkObject.IsOwner);
                    return isServerLoopback;
                }

                return false;
            }
            catch (Exception ex)
            {
                logger?.Error($"Error checking for ghost loopback player: {ex}");
                return false;
            }
        }

        // Prevent opening Sleep UI if server disallows sleeping
        private static bool SleepCanvas_SetIsOpen_Prefix(SleepCanvas __instance, bool open)
        {
            try
            {
                if (!open) return true; // allow closing

                // Only enforce when connected to a server (remote)
                if (FishNet.InstanceFinder.IsClient && !FishNet.InstanceFinder.IsHost)
                {
                    if (!DedicatedServerMod.Client.ServerDataStore.AllowSleeping)
                    {
                        logger?.Msg("Server has disabled sleeping; suppressing SleepCanvas open");
                        // Optionally show a hint/toast here if desired
                        return false; // skip original, do not open
                    }
                }
                return true;
            }
            catch (System.Exception ex)
            {
                logger?.Warning($"SleepCanvas_SetIsOpen_Prefix error: {ex.Message}");
                return true;
            }
        }
    }
}
