using MelonLoader;
using UnityEngine;
using FishNet;
using ScheduleOne.Persistence;
using ScheduleOne.DevUtilities;
using System.Reflection;
using HarmonyLib;
using System;
using FishNet.Managing.Client;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using ScheduleOne.PlayerScripts;
using ScheduleOne.UI;
using Steamworks;

[assembly: MelonInfo(typeof(DedicatedServerMod.Core), "DedicatedServerClient", "1.0.0", "Bars")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace DedicatedServerMod
{
    /// <summary>
    /// Minimal MelonLoader POC for dedicated server connection using Tugboat transport
    /// </summary>
    public class Core : MelonMod
    {
        private static MelonLogger.Instance logger;
        private static bool _isTugboatMode = false;
        private static string _targetServerIP = "localhost";
        private static int _targetServerPort = 38465;

        public override void OnInitializeMelon()
        {
            logger = LoggerInstance;
            logger.Msg("DedicatedServerPrototype initialized");
            
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

            // Apply transport patches using MelonLoader's Harmony
            ApplyTransportPatches();
            
            // Set up client-side loopback player hiding
            SetupClientSideLoopbackHiding();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (sceneName == "Menu")
            {
                logger.Msg("Menu scene loaded - setting up prototype UI");
                MelonCoroutines.Start(AddPrototypeButton());
            }
            else if (sceneName == "Main" && _isTugboatMode)
            {
                logger.Msg("Main scene loaded in dedicated server mode - ensuring quest initialization");
                MelonCoroutines.Start(EnsureQuestInitialization());
            }
        }

        private static System.Collections.IEnumerator EnsureQuestInitialization()
        {
            // Wait for the quest system to be ready
            yield return new WaitForSeconds(2f);
            
            try
            {
                var questManager = NetworkSingleton<ScheduleOne.Quests.QuestManager>.Instance;
                if (questManager != null)
                {
                    logger.Msg("QuestManager found - ensuring quests are properly initialized");
                    
                    // The QuestManager should automatically handle quest initialization for new clients
                    // but we can trigger a save to ensure everything is persisted
                    if (Player.Local != null)
                    {
                        Player.Local.RequestSavePlayer();
                    }
                }
                else
                {
                    logger.Warning("QuestManager not found - quest initialization may be delayed");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error ensuring quest initialization: {ex}");
            }
        }

        private static System.Collections.IEnumerator HandleExistingPlayerSetup(Player player)
        {
            try
            {
                logger.Msg("Handling existing player setup for dedicated server client");
                
                var questManager = NetworkSingleton<ScheduleOne.Quests.QuestManager>.Instance;
                if (questManager != null)
                {
                    logger.Msg("QuestManager found - ensuring existing player gets quest data");
                    
                    // For existing players, the QuestManager should automatically sync quest data
                    // but we can trigger a save to ensure everything is persisted
                    player.RequestSavePlayer();
                }
                else
                {
                    logger.Warning("QuestManager not found for existing player setup");
                }
                
                // Ensure player is properly initialized
                if (!player.PlayerInitializedOverNetwork)
                {
                    logger.Msg("Player not yet initialized over network - marking as initialized");
                    player.MarkPlayerInitialized();
                }
                
                logger.Msg("Existing player setup complete");
            }
            catch (Exception ex)
            {
                logger.Error($"Error in existing player setup: {ex}");
            }
            
            // Wait a bit before completing
            yield return new WaitForSeconds(0.5f);
        }

        private void ApplyTransportPatches()
        {
            try
            {
                var harmony = HarmonyInstance;
                
                // Patch ClientManager.StartConnection to intercept connection attempts
                var clientManagerType = typeof(ClientManager);
                var startConnectionMethod = clientManagerType.GetMethod("StartConnection", 
                    BindingFlags.Public | BindingFlags.Instance, null, new Type[0], null);
                
                if (startConnectionMethod != null)
                {
                    var prefixMethod = typeof(Core).GetMethod(nameof(StartConnectionPrefix), 
                        BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(startConnectionMethod, new HarmonyMethod(prefixMethod));
                    logger.Msg("Patched ClientManager.StartConnection");
                }

                // Patch Multipass.Initialize to add Tugboat transport
                var multipassType = typeof(Multipass);
                var initializeMethod = multipassType.GetMethod("Initialize");
                
                if (initializeMethod != null)
                {
                    var prefixMethod = typeof(Core).GetMethod(nameof(MultipassInitializePrefix), 
                        BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(initializeMethod, new HarmonyMethod(prefixMethod));
                    logger.Msg("Patched Multipass.Initialize");
                }

                // Patch Player.PlayerLoaded to skip intro when using dedicated server mode
                var playerType = typeof(ScheduleOne.PlayerScripts.Player);
                var playerLoadedMethod = playerType.GetMethod("PlayerLoaded", BindingFlags.NonPublic | BindingFlags.Instance);
                if (playerLoadedMethod != null)
                {
                    var skipIntroPrefix = typeof(Core).GetMethod(nameof(PlayerLoadedPrefix_SkipIntro), BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(playerLoadedMethod, new HarmonyMethod(skipIntroPrefix));
                    logger.Msg("Patched Player.PlayerLoaded to skip intro in dedicated mode");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to apply transport patches: {ex}");
            }
        }

        private static bool StartConnectionPrefix(FishNet.Managing.Client.ClientManager __instance, ref bool __result)
        {
            if (!_isTugboatMode)
            {
                logger.Msg("StartConnection called - Tugboat mode disabled, using original");
                return true; // Execute original method
            }

            try
            {
                logger.Msg($"Intercepting StartConnection for Tugboat connection to {_targetServerIP}:{_targetServerPort}");
                
                // Get transport manager and multipass
                var networkManager = InstanceFinder.NetworkManager;
                if (networkManager == null)
                {
                    logger.Error("NetworkManager not found");
                    return true;
                }

                var transportManager = networkManager.TransportManager;
                var transport = transportManager.Transport;
                var multipass = transport as Multipass;
                
                if (multipass == null)
                {
                    logger.Error("Multipass transport not found");
                    return true;
                }

                // Get or add Tugboat component
                var tugboat = multipass.gameObject.GetComponent<Tugboat>();
                if (tugboat == null)
                {
                    logger.Error("Tugboat component not found - should have been added in Initialize patch");
                    return true;
                }

                tugboat.SetClientAddress(_targetServerIP);
                tugboat.SetPort((ushort)_targetServerPort);
                SetClientTransport(multipass, tugboat);
                
                // Start Tugboat connection
                __result = tugboat.StartConnection(false);
                
                logger.Msg($"Tugboat connection started: {__result}, IP: {_targetServerIP}, Port: {_targetServerPort}");
                
                return false; // Skip original method
            }
            catch (Exception ex)
            {
                logger.Error($"Error in StartConnection patch: {ex}");
                return true; // Fallback to original method
            }
        }

        private static void PlayerLoadedPrefix_SkipIntro(Player __instance)
        {
            // For dedicated server clients, we need to handle intro differently
            // Instead of skipping entirely, we'll trigger character creation and quest setup
            if (_isTugboatMode && !InstanceFinder.IsServer)
            {
                // Don't set HasCompletedIntro = true here - let the normal flow handle it
                // Instead, we'll trigger character creation and quest setup manually
                MelonCoroutines.Start(HandleDedicatedServerPlayerSetup(__instance));
            }
        }

        private static System.Collections.IEnumerator HandleDedicatedServerPlayerSetup(Player player)
        {
            logger.Msg("Setting up dedicated server player - bypassing frozen intro sequence");
            
            // Wait a bit for everything to be ready
            yield return new WaitForSeconds(1f);
            
            bool setupCompleted = false;
            bool needsCharacterCreation = false;
            var characterCreator = (ScheduleOne.AvatarFramework.Customization.CharacterCreator)null;
            
            try
            {
                // Check if we need to create a character (if no appearance settings exist)
                if (player.CurrentAvatarSettings == null)
                {
                    logger.Msg("No avatar settings found - triggering character creation");
                    needsCharacterCreation = true;
                    
                    // Wait for CharacterCreator to be available
                    characterCreator = Singleton<ScheduleOne.AvatarFramework.Customization.CharacterCreator>.Instance;
                    float waitTime = 0f;
                    while (characterCreator == null && waitTime < 5f)
                    {
                        waitTime += 0.1f;
                        characterCreator = Singleton<ScheduleOne.AvatarFramework.Customization.CharacterCreator>.Instance;
                    }
                    
                    if (characterCreator != null && characterCreator.DefaultSettings != null)
                    {
                        logger.Msg("CharacterCreator ready - will open with default settings");
                    }
                    else
                    {
                        logger.Error("CharacterCreator or DefaultSettings not found - cannot create character");
                        logger.Msg("Falling back to default appearance setup");
                        
                        // Fallback: create basic avatar settings and mark intro as completed
                        try
                        {
                            var defaultSettings = ScriptableObject.CreateInstance<ScheduleOne.AvatarFramework.Customization.BasicAvatarSettings>();
                            if (defaultSettings != null)
                            {
                                player.SetAppearance(defaultSettings, false);
                                logger.Msg("Applied fallback avatar settings");
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.Error($"Error creating fallback avatar settings: {ex}");
                        }
                        
                        player.HasCompletedIntro = true;
                        setupCompleted = true;
                    }
                }
                else
                {
                    logger.Msg("Avatar settings already exist - marking intro as completed");
                    player.HasCompletedIntro = true;
                    setupCompleted = true;
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error in dedicated server player setup: {ex}");
                // Fallback: mark intro as completed
                try
                {
                    player.HasCompletedIntro = true;
                    logger.Msg("Applied fallback intro completion due to error");
                    setupCompleted = true;
                }
                catch (Exception fallbackEx)
                {
                    logger.Error($"Error in fallback intro completion: {fallbackEx}");
                }
            }
            
            // Handle character creation if needed (outside try-catch to allow yielding)
            if (needsCharacterCreation && characterCreator != null && characterCreator.DefaultSettings != null)
            {
                logger.Msg("Opening character creator with default settings");
                
                // Set up the completion callback to handle intro completion
                characterCreator.onCompleteWithClothing.RemoveAllListeners();
                characterCreator.onCompleteWithClothing.AddListener((appearance, clothing) => {
                    logger.Msg("Character creation completed for dedicated server client");
                    
                    try
                    {
                        // Mark intro as completed
                        player.HasCompletedIntro = true;
                        
                        // Set the appearance
                        player.SetAppearance(appearance, true);
                        
                        // Apply clothing
                        if (clothing != null)
                        {
                            foreach (var clothe in clothing)
                            {
                                player.Clothing.InsertClothing(clothe);
                            }
                        }
                        
                        // Request save
                        player.RequestSavePlayer();
                        
                        // Enable player movement and UI
                        if (PlayerSingleton<PlayerMovement>.Instance != null)
                            PlayerSingleton<PlayerMovement>.Instance.canMove = true;
                        
                        if (PlayerSingleton<PlayerInventory>.Instance != null)
                            PlayerSingleton<PlayerInventory>.Instance.SetInventoryEnabled(true);
                        
                        if (Singleton<HUD>.Instance != null)
                            Singleton<HUD>.Instance.canvas.enabled = true;
                        
                        // Close character creator
                        characterCreator.Close();
                        
                        logger.Msg("Dedicated server player setup complete");
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"Error in character creation completion callback: {ex}");
                    }
                });
                
                // Open character creator
                characterCreator.Open(characterCreator.DefaultSettings);
                setupCompleted = true;
            }
            
            // Handle existing player setup if needed
            if (setupCompleted && player.CurrentAvatarSettings != null)
            {
                // Handle setup for existing players
                MelonCoroutines.Start(HandleExistingPlayerSetup(player));
            }
            
            // Ensure quest system is properly initialized
            // The QuestManager should handle this automatically when the player connects
            // but we can trigger a save to ensure everything is persisted
            if (setupCompleted)
            {
                yield return new WaitForSeconds(0.5f);
                
                try
                {
                    player.RequestSavePlayer();
                    logger.Msg("Player save requested to ensure quest persistence");
                }
                catch (Exception ex)
                {
                    logger.Error($"Error requesting player save: {ex}");
                }
            }
        }

        private static void MultipassInitializePrefix(Multipass __instance)
        {
            try
            {
                var tugboat = __instance.gameObject.GetComponent<Tugboat>();
                if (tugboat == null)
                {
                    tugboat = __instance.gameObject.AddComponent<Tugboat>();
                    logger.Msg("Added Tugboat component to Multipass");
                    
                    // Add to transports list using reflection
                    var transportsField = typeof(Multipass).GetField("_transports", 
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (transportsField != null)
                    {
                        var transports = transportsField.GetValue(__instance) as System.Collections.Generic.List<Transport>;
                        if (transports != null)
                        {
                            transports.Add(tugboat);
                            logger.Msg("Added Tugboat to transports list");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error in Multipass Initialize patch: {ex}");
            }
        }

        private static void SetClientTransport(Multipass multipass, Transport transport)
        {
            try
            {
                var clientTransportField = typeof(Multipass).GetField("_clientTransport", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (clientTransportField != null)
                {
                    clientTransportField.SetValue(multipass, transport);
                    logger.Msg("Set Tugboat as client transport");
                }
                else
                {
                    logger.Warning("Could not find _clientTransport field");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error setting client transport: {ex}");
            }
        }

        private System.Collections.IEnumerator AddPrototypeButton()
        {
            // Wait for main menu to be fully loaded
            yield return new WaitForSeconds(0.5f);
            
            try
            {
                var mainMenu = GameObject.Find("MainMenu");
                if (mainMenu == null)
                {
                    logger.Warning("MainMenu not found");
                    yield break;
                }

                var home = mainMenu.transform.Find("Home");
                if (home == null)
                {
                    logger.Warning("Home not found in MainMenu");
                    yield break;
                }

                var bank = home.Find("Bank");
                if (bank == null)
                {
                    logger.Warning("Bank not found in Home");
                    yield break;
                }

                var continueButton = bank.Find("Continue");
                if (continueButton == null)
                {
                    logger.Warning("Continue button not found");
                    yield break;
                }

                var prototypeButton = GameObject.Instantiate(continueButton.gameObject, bank);
                prototypeButton.name = "PrototypeServerButton";
                
                // Position it below continue button
                var rectTransform = prototypeButton.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    var pos = rectTransform.anchoredPosition;
                    rectTransform.anchoredPosition = new Vector2(pos.x, pos.y - 60f);
                }

                // Update button text
                var textComponent = prototypeButton.GetComponentInChildren<UnityEngine.UI.Text>();
                
                if (textComponent != null)
                {
                    textComponent.text = "Connect to Dedicated Server (Prototype)";
                }

                // Add click handler
                var button = prototypeButton.GetComponent<UnityEngine.UI.Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => {
                        logger.Msg("Prototype server button clicked");
                        StartDedicatedConnection();
                    });
                }

                logger.Msg("Prototype button added to main menu");
            }
            catch (Exception ex)
            {
                logger.Error($"Error adding prototype button: {ex}");
            }
        }

        public static void StartDedicatedConnection()
        {
            try
            {
                logger.Msg($"Starting dedicated server connection to {_targetServerIP}:{_targetServerPort}");
                logger.Msg("This will bypass the frozen intro sequence and handle character creation manually");
                

                _isTugboatMode = true;
                var loadManager = Singleton<LoadManager>.Instance;
                if (loadManager != null)
                {
                    loadManager.ActiveSaveInfo = null;
                    loadManager.IsLoading = true;
                    logger.Msg("Set LoadManager.ActiveSaveInfo = null (join mode)");
                }

                // Start connection using game's normal client loading process
                MelonCoroutines.Start(ConnectToDedicatedServer());
            }
            catch (Exception ex)
            {
                logger.Error($"Error starting dedicated connection: {ex}");
                _isTugboatMode = false;
            }
        }

        private static System.Collections.IEnumerator ConnectToDedicatedServer()
        {
            logger.Msg("Starting connection coroutine");
            
            // Trigger connection using ClientManager
            var clientManager = InstanceFinder.ClientManager;
            if (clientManager == null)
            {
                logger.Error("ClientManager not found");
                _isTugboatMode = false;
                yield break;
            }

            logger.Msg("Calling ClientManager.StartConnection (will be intercepted by our patch)");
            bool result = clientManager.StartConnection();
            
            logger.Msg($"StartConnection returned: {result}");
            
            if (!result)
            {
                logger.Error("StartConnection failed");
                _isTugboatMode = false;
                yield break;
            }

            // Wait for connection establishment (with timeout)
            float timeout = 15f; // Increased timeout for dedicated server connections
            float elapsed = 0f;
            
            while (elapsed < timeout && !InstanceFinder.IsClient)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (InstanceFinder.IsClient)
            {
                logger.Msg("Successfully connected to dedicated server!");
                logger.Msg($"Client connection established in {elapsed:F1}s");
                
                // Wait for player to spawn and initialize
                yield return new WaitForSeconds(2f);
                
                if (Player.Local != null)
                {
                    logger.Msg("Local player found - ensuring proper initialization");
                    
                    // The PlayerLoaded method should be called automatically
                    // Our patch will handle the intro bypass and character creation
                }
                else
                {
                    logger.Warning("Local player not found after connection");
                }
            }
            else
            {
                logger.Error($"Connection timeout after {timeout}s");
                _isTugboatMode = false;
            }
        }

        private static void SetupClientSideLoopbackHiding()
        {
            try
            {
                // Hook player spawn events to detect and hide loopback players
                ScheduleOne.PlayerScripts.Player.onPlayerSpawned = (Action<ScheduleOne.PlayerScripts.Player>)Delegate.Remove(
                    ScheduleOne.PlayerScripts.Player.onPlayerSpawned, new Action<ScheduleOne.PlayerScripts.Player>(OnPlayerSpawned_ClientHideLoopback));
                ScheduleOne.PlayerScripts.Player.onPlayerSpawned = (Action<ScheduleOne.PlayerScripts.Player>)Delegate.Combine(
                    ScheduleOne.PlayerScripts.Player.onPlayerSpawned, new Action<ScheduleOne.PlayerScripts.Player>(OnPlayerSpawned_ClientHideLoopback));
                
                logger.Msg("Client-side loopback hiding setup complete");
            }
            catch (Exception ex)
            {
                logger.Error($"Error setting up client-side loopback hiding: {ex}");
            }
        }

        private static void OnPlayerSpawned_ClientHideLoopback(Player player)
        {
            try
            {
                if (player == null || InstanceFinder.IsServer)
                    return;

                // Check if this is the server's loopback player using NetworkObject ownership
                var networkObject = player.GetComponent<FishNet.Object.NetworkObject>();
                if (networkObject != null)
                {
                    // NetworkObject.Owner = 0 means it's the host/server player that should be hidden
                    // NetworkObject.IsOwner = true means it's the local player
                    // Any other case should be other connected clients
                    
                    bool isServerLoopback = (networkObject.Owner != null && networkObject.OwnerId == 0 && !networkObject.IsOwner);
                    
                    if (isServerLoopback)
                    {
                        logger.Msg($"Detected server loopback player on client (Owner.ClientId = {networkObject.Owner.ClientId}, IsOwner = {networkObject.IsOwner}) - hiding it");
                        player.SetVisibleToLocalPlayer(false);
                        
                        // Also try to hide the avatar if it exists
                        if (player.Avatar != null)
                        {
                            player.Avatar.SetVisible(false);
                        }
                        return;
                    }
                    
                    // logger.Msg($"Player spawn detected - Owner.ClientId: {networkObject.Owner?.ClientId}, IsOwner: {networkObject.IsOwner}, PlayerName: {player.PlayerName}");
                }

                // Fallback: Check for players that have the server's connection characteristics
                MelonCoroutines.Start(DelayedLoopbackCheck(player));
            }
            catch (Exception ex)
            {
                logger.Error($"Error in client loopback player hiding: {ex}");
            }
        }

        private static System.Collections.IEnumerator DelayedLoopbackCheck(Player player)
        {
            // Wait a short time for NetworkObject ownership data to be fully synchronized
            yield return new WaitForSeconds(0.5f);
            
            try
            {
                if (player == null || player.gameObject == null)
                    yield break;

                // Re-check using NetworkObject ownership (in case it wasn't available immediately)
                var networkObject = player.GetComponent<FishNet.Object.NetworkObject>();
                if (networkObject != null && networkObject.Owner != null)
                {
                    bool isServerLoopback = (networkObject.Owner.ClientId == 0 && !networkObject.IsOwner);
                    
                    if (isServerLoopback)
                    {
                        logger.Msg($"Delayed check: hiding server loopback player (Owner.ClientId = {networkObject.Owner.ClientId}, IsOwner = {networkObject.IsOwner})");
                        player.SetVisibleToLocalPlayer(false);
                        
                        if (player.Avatar != null)
                        {
                            player.Avatar.SetVisible(false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error in delayed loopback check: {ex}");
            }
        }

        public override void OnUpdate()
        {
            // Debug key for testing
            if (Input.GetKeyDown(KeyCode.F9))
            {
                logger.Msg("F9 pressed - triggering prototype connection");
                StartDedicatedConnection();
            }
        }
    }
}
