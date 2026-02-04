using System;
using System.Collections;
using MelonLoader;
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

        private readonly MelonLogger.Instance logger;

        // Main menu (left side - Bank buttons)
        private GameObject mainMenuHome;
        private RectTransform mainMenuHomeRect;
        private CanvasGroup mainMenuHomeCanvasGroup;
        private Vector2 mainMenuOriginalPosition;

        // Socials menu (right side - Discord, Twitter, etc.)
        private GameObject mainMenuSocials;
        private RectTransform mainMenuSocialsRect;
        private CanvasGroup mainMenuSocialsCanvasGroup;
        private Vector2 socialsOriginalPosition;

        // Coroutine handles to prevent overlapping animations
        private object mainMenuHomeCoroutine;
        private object mainMenuSocialsCoroutine;

        #endregion

        #region Animation Constants

        private const float ANIMATION_DURATION = 0.15f;
        private const float SLIDE_X_OFFSET = -300f;
        private const float SLIDE_Y_OFFSET = -50f;
        private const float SOCIALS_SLIDE_X_OFFSET = 300f;  // Slide right
        private const float SOCIALS_SLIDE_Y_OFFSET = -50f;  // Slide down

        #endregion

        #region Constructor

        public MenuAnimationController(MelonLogger.Instance logger)
        {
            this.logger = logger;
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
                if (mainMenuHome == null)
                {
                    InitializeMenuReferences();
                }

                // Animate main menu (left side)
                if (mainMenuHome != null && mainMenuHomeRect != null)
                {
                    // Stop any existing main menu animation to prevent overlaps
                    if (mainMenuHomeCoroutine != null)
                    {
                        MelonCoroutines.Stop(mainMenuHomeCoroutine);
                        mainMenuHomeCoroutine = null;
                    }
                    
                    mainMenuHomeCoroutine = MelonCoroutines.Start(AnimateMainMenu(show));
                }

                // Animate socials (right side)
                if (mainMenuSocials != null && mainMenuSocialsRect != null)
                {
                    // Stop any existing socials animation to prevent overlaps
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
                logger.Error($"Error toggling menu visibility: {ex}");
            }
        }

        /// <summary>
        /// Resets the animation controller state.
        /// Call this when returning to the menu scene.
        /// </summary>
        public void Reset()
        {
            // Stop any in-flight coroutines
            if (mainMenuHomeCoroutine != null)
            {
                MelonCoroutines.Stop(mainMenuHomeCoroutine);
                mainMenuHomeCoroutine = null;
            }
            
            if (mainMenuSocialsCoroutine != null)
            {
                MelonCoroutines.Stop(mainMenuSocialsCoroutine);
                mainMenuSocialsCoroutine = null;
            }
            
            mainMenuHome = null;
            mainMenuHomeRect = null;
            mainMenuHomeCanvasGroup = null;
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
                logger.Warning("MenuAnimationController: MainMenu not found");
                return;
            }

            var home = mainMenu.transform.Find("Home");
            if (home != null)
            {
                // Initialize main menu (left side)
                mainMenuHome = home.gameObject;
                mainMenuHomeRect = mainMenuHome.GetComponent<RectTransform>();

                mainMenuHomeCanvasGroup = mainMenuHome.GetComponent<CanvasGroup>();
                if (mainMenuHomeCanvasGroup == null)
                {
                    mainMenuHomeCanvasGroup = mainMenuHome.AddComponent<CanvasGroup>();
                }

                if (mainMenuHomeRect != null)
                {
                    mainMenuOriginalPosition = mainMenuHomeRect.anchoredPosition;
                }

                // Initialize socials menu (right side)
                var socials = home.Find("Socials");
                if (socials != null)
                {
                    mainMenuSocials = socials.gameObject;
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
            Vector2 startPosition = mainMenuHomeRect.anchoredPosition;
            float startAlpha = mainMenuHomeCanvasGroup.alpha;

            Vector2 targetPosition;
            float targetAlpha;

            if (show)
            {
                // Animating back to visible
                targetPosition = mainMenuOriginalPosition;
                targetAlpha = 1f;

                // Ensure it's active before animating in
                mainMenuHome.SetActive(true);
            }
            else
            {
                // Animating away (slide left and down)
                targetPosition = mainMenuOriginalPosition + new Vector2(SLIDE_X_OFFSET, SLIDE_Y_OFFSET);
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
                mainMenuHomeRect.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, smoothT);

                // Interpolate alpha
                mainMenuHomeCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, smoothT);

                yield return null;
            }

            // Ensure final values are set
            mainMenuHomeRect.anchoredPosition = targetPosition;
            mainMenuHomeCanvasGroup.alpha = targetAlpha;

            // Disable after animating out
            if (!show)
            {
                mainMenuHome.SetActive(false);
            }

            // Clear the coroutine handle
            mainMenuHomeCoroutine = null;
        }

        /// <summary>
        /// Animates the socials menu sliding right and fading.
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
                // Animating back to visible
                targetPosition = socialsOriginalPosition;
                targetAlpha = 1f;

                // Ensure it's active before animating in
                mainMenuSocials.SetActive(true);
            }
            else
            {
                // Animating away (slide RIGHT and down)
                // Since Socials is a child of Home, we need to counteract the Home's leftward movement
                // Home moves left by SLIDE_X_OFFSET (-300), so we need to move right by MORE than that
                // to get an overall rightward movement
                float rightwardOffset = SOCIALS_SLIDE_X_OFFSET - SLIDE_X_OFFSET; // 300 - (-300) = 600
                targetPosition = socialsOriginalPosition + new Vector2(rightwardOffset, SOCIALS_SLIDE_Y_OFFSET);
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
                mainMenuSocialsRect.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, smoothT);

                // Interpolate alpha
                mainMenuSocialsCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, smoothT);

                yield return null;
            }

            // Ensure final values are set
            mainMenuSocialsRect.anchoredPosition = targetPosition;
            mainMenuSocialsCanvasGroup.alpha = targetAlpha;

            // Disable after animating out
            if (!show)
            {
                mainMenuSocials.SetActive(false);
            }

            // Clear the coroutine handle
            mainMenuSocialsCoroutine = null;
        }

        #endregion
    }
}
