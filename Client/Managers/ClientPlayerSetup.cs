using FishNet;
using HarmonyLib;
using MelonLoader;
using ScheduleOne.AvatarFramework.Customization;
using ScheduleOne.Intro;
using ScheduleOne.PlayerScripts;
using ScheduleOne.UI;
using System;
using System.Collections;
using System.Reflection;
using ScheduleOne.DevUtilities;
using UnityEngine;
using DedicatedServerMod.API;

namespace DedicatedServerMod.Client.Managers
{
    /// <summary>
    /// Handles player setup, character creation, and intro sequence bypassing for dedicated server clients.
    /// Manages the complex process of initializing players who connect to dedicated servers.
    /// </summary>
    public class ClientPlayerSetup
    {
        private readonly MelonLogger.Instance logger;
        private HarmonyLib.Harmony harmony;

        public ClientPlayerSetup(MelonLogger.Instance logger)
        {
            this.logger = logger;
        }

        public void Initialize()
        {
            try
            {
                logger.Msg("Initializing ClientPlayerSetup");
                
                harmony = new HarmonyLib.Harmony("DedicatedServerMod.ClientPlayerSetup");
                ApplyPlayerPatches();
                
                logger.Msg("ClientPlayerSetup initialized");
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to initialize ClientPlayerSetup: {ex}");
            }
        }

        /// <summary>
        /// Apply patches for player setup handling
        /// </summary>
        private void ApplyPlayerPatches()
        {
            try
            {
                // Patch Player.PlayerLoaded to handle intro skip for dedicated server clients
                var playerType = typeof(Player);
                var playerLoadedMethod = playerType.GetMethod("PlayerLoaded", BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (playerLoadedMethod != null)
                {
                    var skipIntroPrefix = typeof(ClientPlayerSetup).GetMethod(nameof(PlayerLoadedPrefix_HandleDedicatedServer), 
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(playerLoadedMethod, new HarmonyMethod(skipIntroPrefix));
                    logger.Msg("Patched Player.PlayerLoaded for dedicated server setup");
                }
                else
                {
                    logger.Error("Could not find Player.PlayerLoaded method");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to apply player patches: {ex}");
            }
        }

        /// <summary>
        /// Harmony patch for Player.PlayerLoaded
        /// Handles special setup for dedicated server clients
        /// </summary>
        public static bool PlayerLoadedPrefix_HandleDedicatedServer(Player __instance)
        {
            // Check current scene
            var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            
            // NEVER allow PlayerLoaded to run in Menu scene - this prevents ghost avatars
            if (currentScene == "Menu")
            {
                var logger = new MelonLogger.Instance("ClientPlayerSetup");
                logger.Msg("Skipping PlayerLoaded in Menu scene to prevent ghost avatar");
                return false; // Block PlayerLoaded entirely in Menu
            }
            
            // Only handle dedicated server setup in the Main scene
            if (ClientConnectionManager.IsTugboatMode && !InstanceFinder.IsServer && currentScene == "Main")
            {
                var logger = new MelonLogger.Instance("ClientPlayerSetup");
                logger.Msg("Dedicated server client detected - starting custom player setup");
                
                // Don't modify HasCompletedIntro here - let the setup coroutine handle it
                MelonCoroutines.Start(HandleDedicatedServerPlayerSetup(__instance));
                
                // Return false to prevent the original PlayerLoaded method from running
                return false;
            }
            
            // Return true to allow normal execution for non-dedicated server clients in Main scene
            return true;
        }

        /// <summary>
        /// Main coroutine for setting up dedicated server players
        /// </summary>
        private static IEnumerator HandleDedicatedServerPlayerSetup(Player player)
        {
            var logger = new MelonLogger.Instance("ClientPlayerSetup");
            logger.Msg("Setting up dedicated server player - bypassing frozen intro sequence");
            
            // NOTE: If password authentication is required, the game will be frozen (timeScale=0)
            // by the password dialog until authentication completes. This naturally prevents
            // player setup from progressing until the user is authenticated.
            // No explicit wait needed - the Time.timeScale=0 blocks everything!
            
            // Wait for initialization
            yield return new WaitForSeconds(1f);
            
            // Perform initial player setup that normally happens in PlayerLoaded
            InitializePlayerBasics(player);
            
            bool setupCompleted = false;
            bool needsCharacterCreation = false;
            CharacterCreator characterCreator = null;
            bool hadError = false;
            
            // Check if player needs character creation
            if (player.CurrentAvatarSettings == null)
            {
                logger.Msg("No avatar settings found - triggering character creation");
                needsCharacterCreation = true;
                
                // Wait for CharacterCreator to be available
                characterCreator = Singleton<CharacterCreator>.Instance;
                float waitTime = 0f;
                while (characterCreator == null && waitTime < 5f)
                {
                    waitTime += 0.1f;
                    yield return new WaitForSeconds(0.1f);
                    characterCreator = Singleton<CharacterCreator>.Instance;
                }
                
                if (characterCreator?.DefaultSettings != null)
                {
                    logger.Msg("CharacterCreator ready - will open with default settings");
                }
                else
                {
                    logger.Error("CharacterCreator or DefaultSettings not found");
                    yield return MelonCoroutines.Start(HandleFallbackCharacterCreation(player));
                    setupCompleted = true;
                }
            }
            else
            {
                logger.Msg("Avatar settings already exist - marking intro as completed and activating player");
                player.HasCompletedIntro = true;
                
                // Activate player controls since character creation is being bypassed
                Player.Activate();
                setupCompleted = true;
            }
            
            // Handle character creation if needed
            if (needsCharacterCreation && characterCreator?.DefaultSettings != null)
            {
                yield return MelonCoroutines.Start(HandleCharacterCreation(player, characterCreator));
                setupCompleted = true;
            }
            
            // Finalize setup
            if (setupCompleted)
            {
                yield return MelonCoroutines.Start(FinalizePlayerSetup(player));
            }
        }

        /// <summary>
        /// Handle character creation process
        /// </summary>
        private static IEnumerator HandleCharacterCreation(Player player, CharacterCreator characterCreator)
        {
            var logger = new MelonLogger.Instance("ClientPlayerSetup");
            logger.Msg("Opening character creator with default settings");
            
            var introManager = Singleton<IntroManager>.Instance;
            if (introManager?.Container != null)
            {
                // Hide intro UI
                var canvas = introManager.Container.transform.Find("Canvas");
                if (canvas != null)
                {
                    canvas.gameObject.SetActive(false);
                }
            }
            
            // Set up completion callback
            SetupCharacterCreationCallback(player, characterCreator, introManager);
            
            // Open character creator
            characterCreator.Open(characterCreator.DefaultSettings);
            
            yield return null; // Allow character creator to process
        }

        /// <summary>
        /// Set up the character creation completion callback
        /// </summary>
        private static void SetupCharacterCreationCallback(Player player, CharacterCreator characterCreator, IntroManager introManager)
        {
            var logger = new MelonLogger.Instance("ClientPlayerSetup");
            
            characterCreator.onCompleteWithClothing.RemoveAllListeners();
            characterCreator.onCompleteWithClothing.AddListener((appearance, clothing) => {
                logger.Msg("Character creation completed for dedicated server client");
                
                try
                {
                    // Apply clothing first
                    ApplyClothingToPlayer(player, clothing);
                    
                    // Mark intro as completed before setting appearance
                    player.HasCompletedIntro = true;
                    
                    // Send appearance via proper networking
                    logger.Msg("Sending appearance via proper networking flow");
                    player.SendAppearance(appearance);
                    
                    // Handle intro completion
                    CompleteIntroSequence(player, introManager, characterCreator);
                    
                    // Request save after setup
                    MelonCoroutines.Start(DelayedSaveRequest(player));
                    
                    logger.Msg("Dedicated server player setup complete");
                }
                catch (Exception ex)
                {
                    logger.Error($"Error in character creation completion: {ex}");
                }
            });
        }

        /// <summary>
        /// Apply clothing items to player inventory
        /// </summary>
        private static void ApplyClothingToPlayer(Player player, System.Collections.Generic.List<ScheduleOne.Clothing.ClothingInstance> clothing)
        {
            var logger = new MelonLogger.Instance("ClientPlayerSetup");
            
            if (clothing != null && player.Clothing != null)
            {
                logger.Msg($"Applying {clothing.Count} clothing items to inventory");
                foreach (var clothe in clothing)
                {
                    player.Clothing.InsertClothing(clothe);
                    logger.Msg($"Added clothing: {clothe.Name}");
                }
            }
        }

        /// <summary>
        /// Complete the intro sequence and enable player controls
        /// </summary>
        private static void CompleteIntroSequence(Player player, IntroManager introManager, CharacterCreator characterCreator)
        {
            var logger = new MelonLogger.Instance("ClientPlayerSetup");
            logger.Msg("Completing intro sequence");
            
            try
            {
                // Position player correctly
                PositionPlayer(player, introManager);
                
                // Enable all player controls, interaction, and punching
                logger.Msg("Activating player controls and interactions after character creation");
                PlayerSingleton<PlayerCamera>.Instance.StopTransformOverride(0f, reenableCameraLook: false);
                PlayerSingleton<PlayerCamera>.Instance.StopFOVOverride(0f);
                PlayerSingleton<PlayerCamera>.Instance.RemoveActiveUIElement("IntroManager");
                PlayerSingleton<PlayerCamera>.Instance.SetCanLook(true);
                PlayerSingleton<PlayerMovement>.Instance.CanMove = true;
                PlayerSingleton<PlayerInventory>.Instance.SetInventoryEnabled(true);
                Singleton<HUD>.Instance.canvas.enabled = true;
                Player.Activate();
                
                // Clean up UI
                CleanupIntroUI(characterCreator, introManager);
                
                // Invoke completion events
                introManager?.onIntroDone?.Invoke();
            }
            catch (Exception ex)
            {
                logger.Error($"Error completing intro sequence: {ex}");
            }
        }

        /// <summary>
        /// Position player at the correct spawn location
        /// </summary>
        private static void PositionPlayer(Player player, IntroManager introManager)
        {
            if (introManager != null)
            {
                if (!introManager.rv._exploded)
                {
                    player.transform.position = introManager.PlayerInitialPosition.position;
                    player.transform.rotation = introManager.PlayerInitialPosition.rotation;
                }
                else
                {
                    player.transform.position = introManager.PlayerInitialPosition_AfterRVExplosion.position;
                    player.transform.rotation = introManager.PlayerInitialPosition_AfterRVExplosion.rotation;
                }
            }
        }

        /// <summary>
        /// Clean up intro UI elements
        /// </summary>
        private static void CleanupIntroUI(CharacterCreator characterCreator, IntroManager introManager)
        {
            try
            {
                characterCreator?.Close();
                characterCreator?.DisableStuff();
                if (introManager != null)
                {
                    introManager.gameObject.SetActive(false);
                }
            }
            catch (Exception ex)
            {
                var logger = new MelonLogger.Instance("ClientPlayerSetup");
                logger.Error($"Error cleaning up intro UI: {ex}");
            }
        }

        /// <summary>
        /// Handle fallback character creation when normal flow fails
        /// </summary>
        private static IEnumerator HandleFallbackCharacterCreation(Player player)
        {
            var logger = new MelonLogger.Instance("ClientPlayerSetup");
            logger.Msg("Using fallback character creation");
            
            try
            {
                var defaultSettings = ScriptableObject.CreateInstance<BasicAvatarSettings>();
                if (defaultSettings != null)
                {
                    player.SetAppearance(defaultSettings, false);
                    logger.Msg("Applied fallback avatar settings");
                }
                
                player.HasCompletedIntro = true;
                
                // Activate player controls for fallback character creation
                Player.Activate();
                logger.Msg("Activated player controls after fallback character creation");
            }
            catch (Exception ex)
            {
                logger.Error($"Error in fallback character creation: {ex}");
                // Ensure intro is marked complete even if fallback fails
                player.HasCompletedIntro = true;
                // Still try to activate even if fallback fails
                try
                {
                    Player.Activate();
                }
                catch (Exception activateEx)
                {
                    logger.Error($"Error activating player in fallback: {activateEx}");
                }
            }
            
            yield return null;
        }

        /// <summary>
        /// Initialize basic player setup that normally happens in PlayerLoaded
        /// </summary>
        private static void InitializePlayerBasics(Player player)
        {
            var logger = new MelonLogger.Instance("ClientPlayerSetup");
            
            try
            {
                // Set up PoI (Point of Interest) text that normally happens in PlayerLoaded
                if (player.PoI != null)
                {
                    player.PoI.SetMainText("You");
                    if (player.PoI.UI != null)
                    {
                        player.PoI.UI.GetComponentInChildren<UnityEngine.Animation>().Play();
                    }
                }
                
                // Mark player as initialized
                logger.Msg("Basic player initialization completed");
            }
            catch (Exception ex)
            {
                logger.Error($"Error in basic player initialization: {ex}");
            }
        }

        /// <summary>
        /// Finalize player setup with quest system and save
        /// </summary>
        private static IEnumerator FinalizePlayerSetup(Player player)
        {
            var logger = new MelonLogger.Instance("ClientPlayerSetup");
            
            // Wait a moment for everything to settle
            yield return new WaitForSeconds(0.5f);
            
            try
            {
                // Request player save to persist everything
                player.RequestSavePlayer();
                logger.Msg("Player save requested to finalize setup");

                // Notify API mods that the local player is ready and messaging/UI are initialized
                try
                {
                    ModManager.NotifyClientPlayerReady();
                    logger.Msg("Notified mods: OnClientPlayerReady");
                }
                catch (Exception ex)
                {
                    logger.Error($"Error notifying OnClientPlayerReady: {ex}");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error finalizing player setup: {ex}");
            }
        }

        /// <summary>
        /// Delayed save request to ensure appearance is applied
        /// </summary>
        private static IEnumerator DelayedSaveRequest(Player player)
        {
            yield return new WaitForSeconds(1f);
            
            try
            {
                var logger = new MelonLogger.Instance("ClientPlayerSetup");
                logger.Msg("Requesting delayed player save after character creation");
                player.RequestSavePlayer();
                
                // Force clothing refresh
                if (player.Clothing != null)
                {
                    logger.Msg("Forcing clothing refresh for proper synchronization");
                    player.Clothing.RefreshAppearance();
                }
            }
            catch (Exception ex)
            {
                var logger = new MelonLogger.Instance("ClientPlayerSetup");
                logger.Error($"Error in delayed save request: {ex}");
            }
        }
    }
}
