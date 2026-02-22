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
    /// Handles player setup, character creation, and intro sequence bypassing
    /// for dedicated server clients. The primary entry point is
    /// <see cref="HandlePostLoadPlayerSetup"/> which is called from
    /// <see cref="ClientConnectionManager"/> after onLoadComplete fires.
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

        private void ApplyPlayerPatches()
        {
            try
            {
                var playerLoadedMethod = typeof(Player).GetMethod("PlayerLoaded",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (playerLoadedMethod != null)
                {
                    var prefixMethod = typeof(ClientPlayerSetup).GetMethod(
                        nameof(PlayerLoadedPrefix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(playerLoadedMethod, new HarmonyMethod(prefixMethod));
                    logger.Msg("Patched Player.PlayerLoaded (safety-net prefix)");
                }
                else
                {
                    logger.Warning("Could not find Player.PlayerLoaded method - patch skipped");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to apply player patches: {ex}");
            }
        }

        /// <summary>
        /// Safety-net prefix for Player.PlayerLoaded. In the native flow, PlayerLoaded
        /// is only called for host players (subscribed via onLoadComplete in OnStartClient).
        /// For dedicated server clients, the primary setup path is
        /// <see cref="HandlePostLoadPlayerSetup"/> called from ClientConnectionManager.
        ///
        /// This prefix exists only as a fallback: if PlayerLoaded is somehow invoked on a
        /// dedicated server client, it prevents the intro from playing and runs our setup.
        /// </summary>
        public static bool PlayerLoadedPrefix(Player __instance)
        {
            if (!ClientConnectionManager.IsTugboatMode || InstanceFinder.IsServer)
                return true;

            if (!__instance.IsOwner)
                return true;

            var logger = new MelonLogger.Instance("ClientPlayerSetup");
            logger.Msg("PlayerLoaded prefix fallback: skipping native intro, deferring to post-load setup");

            __instance.HasCompletedIntro = true;

            if (__instance.PoI != null)
            {
                __instance.PoI.SetMainText("You");
                if (__instance.PoI.UI != null)
                    __instance.PoI.UI.GetComponentInChildren<Animation>().Play();
            }

            return false;
        }

        /// <summary>
        /// Primary dedicated server player setup, called from ClientConnectionManager
        /// after onLoadComplete fires. Handles intro bypass and character creation
        /// for first-time players.
        /// </summary>
        public static IEnumerator HandlePostLoadPlayerSetup(Player player)
        {
            var logger = new MelonLogger.Instance("ClientPlayerSetup");
            logger.Msg("Starting post-load player setup for dedicated server");

            yield return new WaitForSeconds(0.5f);

            if (player == null)
            {
                logger.Error("Player reference lost during post-load setup");
                yield break;
            }

            InitializePlayerBasics(player);

            if (player.CurrentAvatarSettings != null)
            {
                logger.Msg("Avatar settings already exist - activating player");
                player.HasCompletedIntro = true;
                Player.Activate();
            }
            else
            {
                logger.Msg("No avatar settings - entering character creation");
                yield return MelonCoroutines.Start(HandleCharacterCreation(player));
            }

            yield return MelonCoroutines.Start(FinalizePlayerSetup(player));
        }

        private static void InitializePlayerBasics(Player player)
        {
            var logger = new MelonLogger.Instance("ClientPlayerSetup");
            try
            {
                if (player.PoI != null)
                {
                    player.PoI.SetMainText("You");
                    if (player.PoI.UI != null)
                        player.PoI.UI.GetComponentInChildren<Animation>().Play();
                }
                logger.Msg("Basic player initialization completed");
            }
            catch (Exception ex)
            {
                logger.Error($"Error in basic player initialization: {ex}");
            }
        }

        private static IEnumerator HandleCharacterCreation(Player player)
        {
            var logger = new MelonLogger.Instance("ClientPlayerSetup");

            CharacterCreator characterCreator = null;
            float waitTime = 0f;
            while (characterCreator == null && waitTime < 5f)
            {
                characterCreator = Singleton<CharacterCreator>.InstanceExists
                    ? Singleton<CharacterCreator>.Instance
                    : null;
                if (characterCreator != null) break;
                yield return new WaitForSeconds(0.1f);
                waitTime += 0.1f;
            }

            if (characterCreator?.DefaultSettings == null)
            {
                logger.Warning("CharacterCreator unavailable - using fallback avatar");
                yield return MelonCoroutines.Start(HandleFallbackCharacterCreation(player));
                yield break;
            }

            var introManager = Singleton<IntroManager>.InstanceExists
                ? Singleton<IntroManager>.Instance
                : null;

            if (introManager?.Container != null)
            {
                var canvas = introManager.Container.transform.Find("Canvas");
                if (canvas != null)
                    canvas.gameObject.SetActive(false);
            }

            SetupCharacterCreationCallback(player, characterCreator, introManager);
            characterCreator.Open(characterCreator.DefaultSettings);

            yield return null;
        }

        private static void SetupCharacterCreationCallback(
            Player player, CharacterCreator characterCreator, IntroManager introManager)
        {
            var logger = new MelonLogger.Instance("ClientPlayerSetup");

            characterCreator.onCompleteWithClothing.RemoveAllListeners();
            characterCreator.onCompleteWithClothing.AddListener((appearance, clothing) =>
            {
                logger.Msg("Character creation completed");
                try
                {
                    ApplyClothingToPlayer(player, clothing);
                    player.HasCompletedIntro = true;
                    player.SendAppearance(appearance);
                    CompleteIntroSequence(player, introManager, characterCreator);
                    MelonCoroutines.Start(DelayedSaveRequest(player));
                }
                catch (Exception ex)
                {
                    logger.Error($"Error in character creation completion: {ex}");
                }
            });
        }

        private static void ApplyClothingToPlayer(
            Player player, System.Collections.Generic.List<ScheduleOne.Clothing.ClothingInstance> clothing)
        {
            if (clothing == null || player.Clothing == null) return;

            var logger = new MelonLogger.Instance("ClientPlayerSetup");
            foreach (var clothe in clothing)
            {
                player.Clothing.InsertClothing(clothe);
                logger.Msg($"Added clothing: {clothe.Name}");
            }
        }

        private static void CompleteIntroSequence(
            Player player, IntroManager introManager, CharacterCreator characterCreator)
        {
            var logger = new MelonLogger.Instance("ClientPlayerSetup");
            try
            {
                PositionPlayer(player, introManager);

                PlayerSingleton<PlayerCamera>.Instance.StopTransformOverride(0f, reenableCameraLook: false);
                PlayerSingleton<PlayerCamera>.Instance.StopFOVOverride(0f);
                PlayerSingleton<PlayerCamera>.Instance.RemoveActiveUIElement("IntroManager");
                PlayerSingleton<PlayerCamera>.Instance.SetCanLook(true);
                PlayerSingleton<PlayerMovement>.Instance.CanMove = true;
                PlayerSingleton<PlayerInventory>.Instance.SetInventoryEnabled(true);
                Singleton<HUD>.Instance.canvas.enabled = true;
                Player.Activate();

                characterCreator?.Close();
                characterCreator?.DisableStuff();
                if (introManager != null)
                    introManager.gameObject.SetActive(false);

                introManager?.onIntroDone?.Invoke();
            }
            catch (Exception ex)
            {
                logger.Error($"Error completing intro sequence: {ex}");
            }
        }

        private static void PositionPlayer(Player player, IntroManager introManager)
        {
            if (introManager == null) return;

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

        private static IEnumerator HandleFallbackCharacterCreation(Player player)
        {
            var logger = new MelonLogger.Instance("ClientPlayerSetup");
            try
            {
                var defaultSettings = ScriptableObject.CreateInstance<BasicAvatarSettings>();
                if (defaultSettings != null)
                {
                    player.SetAppearance(defaultSettings, false);
                    logger.Msg("Applied fallback avatar settings");
                }

                player.HasCompletedIntro = true;
                Player.Activate();
            }
            catch (Exception ex)
            {
                logger.Error($"Error in fallback character creation: {ex}");
                player.HasCompletedIntro = true;
                try { Player.Activate(); }
                catch (Exception activateEx) { logger.Error($"Activate failed: {activateEx}"); }
            }

            yield return null;
        }

        private static IEnumerator FinalizePlayerSetup(Player player)
        {
            var logger = new MelonLogger.Instance("ClientPlayerSetup");
            yield return new WaitForSeconds(0.5f);

            try
            {
                player.RequestSavePlayer();
                logger.Msg("Player save requested");

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

        private static IEnumerator DelayedSaveRequest(Player player)
        {
            yield return new WaitForSeconds(1f);
            try
            {
                player.RequestSavePlayer();
                if (player.Clothing != null)
                    player.Clothing.RefreshAppearance();
            }
            catch (Exception ex)
            {
                var logger = new MelonLogger.Instance("ClientPlayerSetup");
                logger.Error($"Error in delayed save request: {ex}");
            }
        }
    }
}
