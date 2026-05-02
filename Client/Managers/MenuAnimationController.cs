using System;
using System.Collections;
using MelonLoader;
using DedicatedServerMod.Utils;
#if IL2CPP
using Il2CppScheduleOne.UI.Polling;
#else
using ScheduleOne.UI.Polling;
#endif
using UnityEngine;

namespace DedicatedServerMod.Client.Managers
{
    /// <summary>
    /// Manages main menu animations when transitioning to/from the server browser.
    /// Handles sliding and fading animations for menu elements.
    /// </summary>
    public class MenuAnimationController
    {
        #region Private Fields

        // Main menu (left side - Bank buttons)
        private GameObject mainMenuBank;
        private RectTransform mainMenuBankRect;
        private CanvasGroup mainMenuBankCanvasGroup;
        private Vector2 bankOriginalPosition;

        // Right-side panel (main menu poll/community column)
        private GameObject mainMenuRightPanel;
        private RectTransform mainMenuRightPanelRect;
        private CanvasGroup mainMenuRightPanelCanvasGroup;
        private Vector2 rightPanelOriginalPosition;

        // Social links under the poll/community column
        private GameObject mainMenuSocials;
        private RectTransform mainMenuSocialsRect;
        private CanvasGroup mainMenuSocialsCanvasGroup;
        private Vector2 socialsOriginalPosition;

        // Coroutine handles to prevent overlapping animations
        private object mainMenuBankCoroutine;
        private object mainMenuRightPanelCoroutine;
        private object mainMenuSocialsCoroutine;

        #endregion

        #region Animation Constants

        private const float ANIMATION_DURATION = 0.15f;
        private const float SLIDE_X_OFFSET = -300f;
        private const float SLIDE_Y_OFFSET = -50f;
        private const float RIGHT_PANEL_SLIDE_X_OFFSET = 300f;  // Slide right
        private const float RIGHT_PANEL_SLIDE_Y_OFFSET = 50f;   // Slide up for RightBank in this layout
        private const float SOCIALS_SLIDE_X_OFFSET = 160f;      // Slide a bit right
        private const float SOCIALS_SLIDE_Y_OFFSET = -140f;     // Slide mostly down for Socials in this layout

        #endregion

        #region Constructor

        public MenuAnimationController()
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Toggles the main menu visibility with animations.
        /// </summary>
        /// <param name="show">True to show the menu, false to hide it</param>
        public void ToggleMenuVisibility(bool show)
        {
            try
            {
                // Initialize menu references on first use
                if (mainMenuBank == null)
                {
                    InitializeMenuReferences();
                }

                // Animate main menu (left side)
                if (mainMenuBank != null && mainMenuBankRect != null)
                {
                    // Stop any existing main menu animation to prevent overlaps
                    if (mainMenuBankCoroutine != null)
                    {
                        MelonCoroutines.Stop(mainMenuBankCoroutine);
                        mainMenuBankCoroutine = null;
                    }
                    
                    mainMenuBankCoroutine = MelonCoroutines.Start(AnimateMainMenu(show));
                }

                // Animate right-side poll/community panel
                if (mainMenuRightPanel != null && mainMenuRightPanelRect != null)
                {
                    // Stop any existing right-panel animation to prevent overlaps
                    if (mainMenuRightPanelCoroutine != null)
                    {
                        MelonCoroutines.Stop(mainMenuRightPanelCoroutine);
                        mainMenuRightPanelCoroutine = null;
                    }
                    
                    mainMenuRightPanelCoroutine = MelonCoroutines.Start(AnimateRightPanel(show));
                }

                // Animate socials separately so they continue to fly out toward the lower-right.
                if (mainMenuSocials != null && mainMenuSocialsRect != null)
                {
                    if (mainMenuSocialsCoroutine != null)
                    {
                        MelonCoroutines.Stop(mainMenuSocialsCoroutine);
                        mainMenuSocialsCoroutine = null;
                    }

                    mainMenuSocialsCoroutine = MelonCoroutines.Start(AnimateSocials(show));
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error toggling menu visibility: {ex}");
            }
        }

        /// <summary>
        /// Resets the animation controller state.
        /// Call this when returning to the menu scene.
        /// </summary>
        public void Reset()
        {
            // Stop any in-flight coroutines
            if (mainMenuBankCoroutine != null)
            {
                MelonCoroutines.Stop(mainMenuBankCoroutine);
                mainMenuBankCoroutine = null;
            }
            
            if (mainMenuRightPanelCoroutine != null)
            {
                MelonCoroutines.Stop(mainMenuRightPanelCoroutine);
                mainMenuRightPanelCoroutine = null;
            }

            if (mainMenuSocialsCoroutine != null)
            {
                MelonCoroutines.Stop(mainMenuSocialsCoroutine);
                mainMenuSocialsCoroutine = null;
            }
            
            mainMenuBank = null;
            mainMenuBankRect = null;
            mainMenuBankCanvasGroup = null;
            mainMenuRightPanel = null;
            mainMenuRightPanelRect = null;
            mainMenuRightPanelCanvasGroup = null;
            mainMenuSocials = null;
            mainMenuSocialsRect = null;
            mainMenuSocialsCanvasGroup = null;
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes references to menu GameObjects and their components.
        /// </summary>
        private void InitializeMenuReferences()
        {
            var mainMenu = GameObject.Find("MainMenu");
            if (mainMenu == null)
            {
                DebugLog.Warning("MenuAnimationController: MainMenu not found");
                return;
            }

            var home = mainMenu.transform.Find("Home");
            if (home != null)
            {
                // Initialize main menu (left side) as the Bank panel itself.
                // Animating Home causes the right-side Socials panel to inherit the left motion.
                var bank = home.Find("Bank");
                if (bank != null)
                {
                    mainMenuBank = bank.gameObject;
                    mainMenuBankRect = mainMenuBank.GetComponent<RectTransform>();

                    mainMenuBankCanvasGroup = mainMenuBank.GetComponent<CanvasGroup>();
                    if (mainMenuBankCanvasGroup == null)
                    {
                        mainMenuBankCanvasGroup = mainMenuBank.AddComponent<CanvasGroup>();
                    }

                    if (mainMenuBankRect != null)
                    {
                        bankOriginalPosition = mainMenuBankRect.anchoredPosition;
                    }
                }

                // The real main-menu right column is Home/RightBank.
                // CommunityVote must stay enabled, so we animate the container rather than disabling the poll object.
                Transform rightPanelTransform = home.Find("RightBank");
                if (rightPanelTransform == null)
                {
                    Transform communityVote = home.Find("RightBank/CommunityVote");
                    if (communityVote != null)
                    {
                        rightPanelTransform = communityVote.parent != null ? communityVote.parent : communityVote;
                    }
                }

                if (rightPanelTransform == null)
                {
                    PollPanel pollPanel = UnityEngine.Object.FindObjectOfType<PollPanel>(true);
                    if (pollPanel != null)
                    {
                        rightPanelTransform = pollPanel.transform.parent != null ? pollPanel.transform.parent : pollPanel.transform;
                    }
                }

                if (rightPanelTransform != null)
                {
                    mainMenuRightPanel = rightPanelTransform.gameObject;
                    mainMenuRightPanelRect = mainMenuRightPanel.GetComponent<RectTransform>();

                    mainMenuRightPanelCanvasGroup = mainMenuRightPanel.GetComponent<CanvasGroup>();
                    if (mainMenuRightPanelCanvasGroup == null)
                    {
                        mainMenuRightPanelCanvasGroup = mainMenuRightPanel.AddComponent<CanvasGroup>();
                    }

                    if (mainMenuRightPanelRect != null)
                    {
                        rightPanelOriginalPosition = mainMenuRightPanelRect.anchoredPosition;
                    }

                    DebugLog.StartupDebug($"MenuAnimationController: right panel target = {GetTransformPath(rightPanelTransform)}");
                }

                Transform socialsTransform = home.Find("Socials");
                if (socialsTransform != null)
                {
                    mainMenuSocials = socialsTransform.gameObject;
                    mainMenuSocialsRect = mainMenuSocials.GetComponent<RectTransform>();

                    mainMenuSocialsCanvasGroup = mainMenuSocials.GetComponent<CanvasGroup>();
                    if (mainMenuSocialsCanvasGroup == null)
                    {
                        mainMenuSocialsCanvasGroup = mainMenuSocials.AddComponent<CanvasGroup>();
                    }

                    if (mainMenuSocialsRect != null)
                    {
                        socialsOriginalPosition = mainMenuSocialsRect.anchoredPosition;
                    }

                    DebugLog.StartupDebug($"MenuAnimationController: socials target = {GetTransformPath(socialsTransform)}");
                }
            }
        }

        #endregion

        #region Animation Coroutines

        /// <summary>
        /// Animates the main menu (Home/Bank) sliding left and fading.
        /// </summary>
        private IEnumerator AnimateMainMenu(bool show)
        {
            float elapsed = 0f;
            Vector2 startPosition = mainMenuBankRect.anchoredPosition;
            float startAlpha = mainMenuBankCanvasGroup.alpha;

            Vector2 targetPosition;
            float targetAlpha;

            if (show)
            {
                // Animating back to visible
                targetPosition = bankOriginalPosition;
                targetAlpha = 1f;

                // Ensure it's active before animating in
                mainMenuBank.SetActive(true);
            }
            else
            {
                // Animating away (slide left and down)
                targetPosition = bankOriginalPosition + new Vector2(SLIDE_X_OFFSET, SLIDE_Y_OFFSET);
                targetAlpha = 0f;
            }

            // Animate position and alpha
            while (elapsed < ANIMATION_DURATION)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / ANIMATION_DURATION;

                // Use ease-in-out curve for smooth animation
                float smoothT = t * t * (3f - 2f * t);

                // Interpolate position
                mainMenuBankRect.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, smoothT);

                // Interpolate alpha
                mainMenuBankCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, smoothT);

                yield return null;
            }

            // Ensure final values are set
            mainMenuBankRect.anchoredPosition = targetPosition;
            mainMenuBankCanvasGroup.alpha = targetAlpha;

            // Disable after animating out
            if (!show)
            {
                mainMenuBank.SetActive(false);
            }

            // Clear the coroutine handle
            mainMenuBankCoroutine = null;
        }

        /// <summary>
        /// Animates the right-side menu panel sliding right and fading.
        /// </summary>
        private IEnumerator AnimateRightPanel(bool show)
        {
            float elapsed = 0f;
            Vector2 startPosition = mainMenuRightPanelRect.anchoredPosition;
            float startAlpha = mainMenuRightPanelCanvasGroup.alpha;

            Vector2 targetPosition;
            float targetAlpha;

            if (show)
            {
                // Animating back to visible
                targetPosition = rightPanelOriginalPosition;
                targetAlpha = 1f;
            }
            else
            {
                // Animating away (slide RIGHT and slightly up)
                targetPosition = rightPanelOriginalPosition + new Vector2(RIGHT_PANEL_SLIDE_X_OFFSET, RIGHT_PANEL_SLIDE_Y_OFFSET);
                targetAlpha = 0f;
            }

            // Animate position and alpha
            while (elapsed < ANIMATION_DURATION)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / ANIMATION_DURATION;

                // Use ease-in-out curve for smooth animation
                float smoothT = t * t * (3f - 2f * t);

                // Interpolate position
                mainMenuRightPanelRect.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, smoothT);

                // Interpolate alpha
                mainMenuRightPanelCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, smoothT);

                yield return null;
            }

            // Ensure final values are set
            mainMenuRightPanelRect.anchoredPosition = targetPosition;
            mainMenuRightPanelCanvasGroup.alpha = targetAlpha;

            // Clear the coroutine handle
            mainMenuRightPanelCoroutine = null;
        }

        /// <summary>
        /// Animates the social links sliding to the lower-right and fading.
        /// </summary>
        private IEnumerator AnimateSocials(bool show)
        {
            float elapsed = 0f;
            Vector2 startPosition = mainMenuSocialsRect.anchoredPosition;
            float startAlpha = mainMenuSocialsCanvasGroup.alpha;

            Vector2 targetPosition;
            float targetAlpha;

            if (show)
            {
                targetPosition = socialsOriginalPosition;
                targetAlpha = 1f;
                mainMenuSocials.SetActive(true);
            }
            else
            {
                targetPosition = socialsOriginalPosition + new Vector2(SOCIALS_SLIDE_X_OFFSET, SOCIALS_SLIDE_Y_OFFSET);
                targetAlpha = 0f;
            }

            while (elapsed < ANIMATION_DURATION)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / ANIMATION_DURATION;
                float smoothT = t * t * (3f - 2f * t);

                mainMenuSocialsRect.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, smoothT);
                mainMenuSocialsCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, smoothT);

                yield return null;
            }

            mainMenuSocialsRect.anchoredPosition = targetPosition;
            mainMenuSocialsCanvasGroup.alpha = targetAlpha;

            if (!show)
            {
                mainMenuSocials.SetActive(false);
            }

            mainMenuSocialsCoroutine = null;
        }

        private static string GetTransformPath(Transform target)
        {
            if (target == null)
            {
                return string.Empty;
            }

            string path = target.name;
            Transform current = target.parent;

            while (current != null)
            {
                path = $"{current.name}/{path}";
                current = current.parent;
            }

            return path;
        }

        #endregion
    }
}
