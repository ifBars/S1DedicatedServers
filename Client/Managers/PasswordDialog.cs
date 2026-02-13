using MelonLoader;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using ScheduleOne.PlayerScripts;

namespace DedicatedServerMod.Client.Managers
{
    /// <summary>
    /// Manages the password authentication dialog UI.
    /// </summary>
    public class PasswordDialog
    {
        private readonly MelonLogger.Instance logger;
        private readonly ClientConnectionManager connectionManager;

        // UI Components
        private GameObject passwordPromptOverlay;
        private GameObject passwordPromptPanel;
        private TMP_InputField passwordInput;
        private TMP_Text passwordPromptTitle;
        private TMP_Text passwordErrorText;
        private Button passwordSubmitButton;
        private Button passwordCancelButton;

        // Captured fonts for UI consistency
        private TMP_FontAsset capturedTmpFont;
        
        // Cursor lock enforcement
        private bool isPasswordDialogActive = false;
        
        // Authentication retry tracking
        private int authRetryCount = 0;
        private const int MAX_AUTH_RETRIES = 1; // Changed to 1 - disconnect on first failure

        // Theme colors (matching main UI)
        private static readonly Color PANEL_BG = new Color(0.08f, 0.09f, 0.12f, 0.96f);
        private static readonly Color OVERLAY_DIM = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color INPUT_BG = new Color(0.12f, 0.13f, 0.16f, 1f);
        private static readonly Color BTN_BG = new Color(0.18f, 0.20f, 0.25f, 0.95f);

        /// <summary>
        /// Gets whether the password dialog is currently visible.
        /// </summary>
        public bool IsVisible => passwordPromptOverlay != null && passwordPromptOverlay.activeSelf;

        public PasswordDialog(MelonLogger.Instance logger, ClientConnectionManager connectionManager)
        {
            this.logger = logger;
            this.connectionManager = connectionManager;
        }

        /// <summary>
        /// Sets the captured TMP font for UI consistency.
        /// </summary>
        public void SetCapturedFont(TMP_FontAsset font)
        {
            capturedTmpFont = font;
        }

        /// <summary>
        /// Shows the password prompt dialog.
        /// </summary>
        /// <param name="serverName">The name of the server requesting authentication</param>
        public void ShowPasswordPrompt(string serverName)
        {
            try
            {
                logger.Msg($"Showing password prompt for server: {serverName}");

                // Create password prompt if it doesn't exist
                if (passwordPromptOverlay == null)
                {
                    CreatePasswordPromptUI();
                }

                if (passwordPromptOverlay == null)
                {
                    logger.Error("Failed to create password prompt UI");
                    return;
                }

                // Update title
                if (passwordPromptTitle != null)
                {
                    passwordPromptTitle.text = $"Password Required\n{serverName}";
                }

                // Clear previous input and error
                if (passwordInput != null)
                {
                    passwordInput.text = "";
                }
                if (passwordErrorText != null)
                {
                    passwordErrorText.text = "";
                    passwordErrorText.gameObject.SetActive(false);
                }
                
                // CRITICAL: Always reset and enable submit button when showing dialog
                if (passwordSubmitButton != null)
                {
                    passwordSubmitButton.interactable = true;
                    logger.Msg("Submit button enabled for password dialog");
                }
                
                // Reset retry count when showing dialog
                authRetryCount = 0;

                // CRITICAL: PAUSE THE ENTIRE GAME - This stops ALL game logic including player movement
                UnityEngine.Time.timeScale = 0f;
                logger.Msg("Game paused (timeScale = 0) for password dialog");

                // Unlock cursor and make it visible for UI interaction FIRST
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
                logger.Msg("Cursor unlocked for password dialog");

                // Show the overlay
                passwordPromptOverlay.SetActive(true);
                
                // Mark dialog as active for cursor enforcement
                isPasswordDialogActive = true;

                // Focus the input field after a frame to ensure it works
                MelonLoader.MelonCoroutines.Start(DelayedFocusInput());
                
                // Start cursor enforcement coroutine (use unscaled time!)
                MelonLoader.MelonCoroutines.Start(EnforceCursorUnlock());
                
                logger.Msg("Password dialog shown - game paused and cursor enforcement enabled");
            }
            catch (Exception ex)
            {
                logger.Error($"Error showing password prompt: {ex}");
            }
        }

        /// <summary>
        /// Triggers the game's pause menu by simulating ESC key press.
        /// This naturally blocks world interaction using the game's own pause system.
        /// </summary>
        private void TriggerGamePauseMenu()
        {
            try
            {
                // Find the SleepCanvas and use it as a pause mechanism
                var sleepCanvas = UnityEngine.Object.FindObjectOfType<ScheduleOne.UI.SleepCanvas>();
                if (sleepCanvas != null)
                {
                    // Try to open the sleep canvas using reflection
                    var setIsOpenMethod = sleepCanvas.GetType().GetMethod("SetIsOpen", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    
                    if (setIsOpenMethod != null)
                    {
                        setIsOpenMethod.Invoke(sleepCanvas, new object[] { true });
                        logger.Msg("Opened sleep canvas to block world interaction");
                    }
                    else
                    {
                        logger.Warning("Could not find SetIsOpen method on SleepCanvas");
                    }
                }
                else
                {
                    logger.Warning("SleepCanvas not found - password dialog will rely on timeScale alone");
                }
            }
            catch (Exception ex)
            {
                logger.Warning($"Failed to trigger pause menu: {ex.Message}");
            }
        }

        /// <summary>
        /// Delayed input field focus to ensure it works properly.
        /// </summary>
        private System.Collections.IEnumerator DelayedFocusInput()
        {
            yield return null; // Wait one frame
            
            if (passwordInput != null)
            {
                passwordInput.Select();
                passwordInput.ActivateInputField();
            }
        }

        /// <summary>
        /// Continuously enforces cursor unlock while password dialog is visible.
        /// Fights against the game's attempts to re-lock the cursor.
        /// Uses UNSCALED time so it works even when game is paused.
        /// </summary>
        private System.Collections.IEnumerator EnforceCursorUnlock()
        {
            while (isPasswordDialogActive)
            {
                // Force cursor to stay unlocked and visible
                if (UnityEngine.Cursor.lockState != CursorLockMode.None)
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.None;
                }
                
                if (!UnityEngine.Cursor.visible)
                {
                    UnityEngine.Cursor.visible = true;
                }
                
                yield return new UnityEngine.WaitForSecondsRealtime(0.1f); // UNSCALED time - works when paused
            }
        }

        /// <summary>
        /// Hides the password prompt dialog.
        /// </summary>
        public void HidePasswordPrompt()
        {
            try
            {
                logger.Msg("Hiding password prompt dialog");
                
                // Mark dialog as inactive to stop cursor enforcement
                isPasswordDialogActive = false;
                
                if (passwordPromptOverlay != null)
                {
                    passwordPromptOverlay.SetActive(false);
                    logger.Msg("Password overlay hidden");
                }

                // CRITICAL: Re-enable the submit button when hiding
                if (passwordSubmitButton != null)
                {
                    passwordSubmitButton.interactable = true;
                    logger.Msg("Submit button re-enabled");
                }
                
                // Clear input field and error message for next time
                if (passwordInput != null)
                {
                    passwordInput.text = "";
                }
                if (passwordErrorText != null)
                {
                    passwordErrorText.text = "";
                    passwordErrorText.gameObject.SetActive(false);
                }
                
                // Reset retry count
                authRetryCount = 0;

                // CRITICAL: RESUME THE GAME - Always restore timeScale!
                UnityEngine.Time.timeScale = 1f;
                logger.Msg("Game resumed (timeScale = 1)");

                // Only restore cursor lock if we're still in-game (not in menu)
                var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                logger.Msg($"Current scene: {currentScene}");
                
                if (currentScene != "Menu")
                {
                    // In-game: lock cursor
                    UnityEngine.Cursor.lockState = CursorLockMode.None; // Keep unlocked temporarily
                    UnityEngine.Cursor.visible = true;
                    logger.Msg("Cursor unlocked (will be locked by game when appropriate)");
                }
                else
                {
                    // In menu: keep cursor unlocked
                    UnityEngine.Cursor.lockState = CursorLockMode.None;
                    UnityEngine.Cursor.visible = true;
                    logger.Msg("Cursor unlocked for menu");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error hiding password prompt: {ex}");
                // ALWAYS restore timeScale even if there's an error
                UnityEngine.Time.timeScale = 1f;
                
                // ALWAYS re-enable button on error
                if (passwordSubmitButton != null)
                {
                    passwordSubmitButton.interactable = true;
                }
            }
        }

        /// <summary>
        /// Shows an authentication error message.
        /// </summary>
        /// <param name="errorMessage">The error message to display</param>
        public void ShowAuthenticationError(string errorMessage)
        {
            try
            {
                authRetryCount++;
                logger.Warning($"Authentication failed: {errorMessage}");
                
                // Re-enable submit button before disconnecting so it's ready for reconnect
                if (passwordSubmitButton != null)
                {
                    passwordSubmitButton.interactable = true;
                    logger.Msg("Submit button re-enabled after auth failure");
                }

                // Show error message
                if (passwordErrorText != null)
                {
                    passwordErrorText.text = $"{errorMessage}\nDisconnecting...";
                    passwordErrorText.gameObject.SetActive(true);
                }
                
                logger.Warning("Authentication failed - disconnecting immediately");
                
                // Disconnect immediately after brief delay to show message
                MelonLoader.MelonCoroutines.Start(DelayedDisconnect());
            }
            catch (Exception ex)
            {
                logger.Error($"Error showing authentication error: {ex}");
                // Ensure button is re-enabled even on error
                if (passwordSubmitButton != null)
                {
                    passwordSubmitButton.interactable = true;
                }
            }
        }
        
        /// <summary>
        /// Called when authentication succeeds.
        /// </summary>
        public void OnAuthenticationSuccess()
        {
            try
            {
                logger.Msg("Authentication successful!");
                authRetryCount = 0; // Reset retry count
                
                // Re-enable submit button
                if (passwordSubmitButton != null)
                {
                    passwordSubmitButton.interactable = true;
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error handling authentication success: {ex}");
            }
        }
        
        /// <summary>
        /// Delayed disconnect after authentication failure.
        /// </summary>
        private System.Collections.IEnumerator DelayedDisconnect()
        {
            yield return new UnityEngine.WaitForSecondsRealtime(1.5f); // Show message for 1.5 seconds
            
            HidePasswordPrompt();
            connectionManager?.DisconnectFromDedicatedServer();
        }

        /// <summary>
        /// Creates the password prompt UI overlay.
        /// </summary>
        private void CreatePasswordPromptUI()
        {
            try
            {
                // Ensure EventSystem exists for UI interaction
                EnsureEventSystemExists();
                
                // Create a standalone canvas for the password prompt that persists across scenes
                passwordPromptOverlay = new GameObject("PasswordPromptOverlay");
                
                // Add a Canvas component to make it self-contained
                var canvas = passwordPromptOverlay.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32767; // Maximum sorting order - render on top of EVERYTHING
                canvas.overrideSorting = true; // Override any parent canvas sorting

                // Add CanvasScaler for proper UI scaling
                var scaler = passwordPromptOverlay.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                
                // CRITICAL: Add GraphicRaycaster for UI button clicks to work!
                passwordPromptOverlay.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                logger.Msg("Added GraphicRaycaster to password dialog canvas");

                // Make the overlay persist across scene changes
                UnityEngine.Object.DontDestroyOnLoad(passwordPromptOverlay);

                // Add canvas group for fading and blocking interaction
                var canvasGroup = passwordPromptOverlay.AddComponent<CanvasGroup>();
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true; // CRITICAL: Block ALL raycasts including world clicks
                canvasGroup.alpha = 1f;

                // Add full-screen BLACK blocker with 100% opacity FIRST (renders behind everything else)
                var solidBlockerObj = new GameObject("SolidBlocker");
                solidBlockerObj.transform.SetParent(passwordPromptOverlay.transform, false);
                solidBlockerObj.transform.SetAsFirstSibling(); // Render first = behind everything
                var solidBlockerImage = solidBlockerObj.AddComponent<Image>();
                solidBlockerImage.color = new Color(0f, 0f, 0f, 1f); // Solid black with 100% opacity
                solidBlockerImage.raycastTarget = true; // CRITICAL: Block clicks to world
                
                var solidBlockerRect = solidBlockerObj.GetComponent<RectTransform>();
                solidBlockerRect.anchorMin = Vector2.zero;
                solidBlockerRect.anchorMax = Vector2.one;
                solidBlockerRect.sizeDelta = Vector2.zero;

                // Add full-screen blocker image (dimmed overlay on top of solid black)
                var blockerObj = new GameObject("Blocker");
                blockerObj.transform.SetParent(passwordPromptOverlay.transform, false);
                var blockerImage = blockerObj.AddComponent<Image>();
                blockerImage.color = OVERLAY_DIM;
                blockerImage.raycastTarget = true; // CRITICAL: Block clicks to world
                
                var blockerRect = blockerObj.GetComponent<RectTransform>();
                blockerRect.anchorMin = Vector2.zero;
                blockerRect.anchorMax = Vector2.one;
                blockerRect.sizeDelta = Vector2.zero;

                // Create panel
                passwordPromptPanel = new GameObject("PasswordPromptPanel");
                passwordPromptPanel.transform.SetParent(passwordPromptOverlay.transform, false);

                var panelImage = passwordPromptPanel.AddComponent<Image>();
                panelImage.color = PANEL_BG;
                panelImage.raycastTarget = true; // Ensure panel blocks clicks too

                var panelRect = passwordPromptPanel.GetComponent<RectTransform>();
                panelRect.anchorMin = new Vector2(0.5f, 0.5f);
                panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                panelRect.sizeDelta = new Vector2(500, 300);
                panelRect.anchoredPosition = Vector2.zero;

                // Add vertical layout group
                var layout = passwordPromptPanel.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(30, 30, 30, 30);
                layout.spacing = 20;
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.childControlHeight = false;
                layout.childControlWidth = true;
                layout.childForceExpandHeight = false;
                layout.childForceExpandWidth = true;

                // Title text
                var titleObj = new GameObject("TitleText");
                titleObj.transform.SetParent(passwordPromptPanel.transform, false);
                passwordPromptTitle = titleObj.AddComponent<TextMeshProUGUI>();
                passwordPromptTitle.text = "Password Required";
                passwordPromptTitle.fontSize = 24;
                passwordPromptTitle.fontStyle = FontStyles.Bold;
                passwordPromptTitle.alignment = TextAlignmentOptions.Center;
                passwordPromptTitle.color = Color.white;
                if (capturedTmpFont != null)
                {
                    passwordPromptTitle.font = capturedTmpFont;
                }
                var titleRect = titleObj.GetComponent<RectTransform>();
                titleRect.sizeDelta = new Vector2(0, 70);

                // Password input
                var inputObj = new GameObject("PasswordInput");
                inputObj.transform.SetParent(passwordPromptPanel.transform, false);
                var inputImage = inputObj.AddComponent<Image>();
                inputImage.color = INPUT_BG;

                passwordInput = inputObj.AddComponent<TMP_InputField>();
                passwordInput.inputType = TMP_InputField.InputType.Password;
                passwordInput.asteriskChar = '•';
                
                // Enable Enter key to submit
                passwordInput.onSubmit.AddListener((text) => OnPasswordSubmit());

                var inputText = new GameObject("Text");
                inputText.transform.SetParent(inputObj.transform, false);
                var tmpText = inputText.AddComponent<TextMeshProUGUI>();
                tmpText.fontSize = 18;
                tmpText.color = Color.white;
                if (capturedTmpFont != null)
                {
                    tmpText.font = capturedTmpFont;
                }

                var placeholder = new GameObject("Placeholder");
                placeholder.transform.SetParent(inputObj.transform, false);
                var tmpPlaceholder = placeholder.AddComponent<TextMeshProUGUI>();
                tmpPlaceholder.text = "Enter server password...";
                tmpPlaceholder.fontSize = 18;
                tmpPlaceholder.color = new Color(1, 1, 1, 0.4f);
                tmpPlaceholder.fontStyle = FontStyles.Italic;
                if (capturedTmpFont != null)
                {
                    tmpPlaceholder.font = capturedTmpFont;
                }

                passwordInput.textComponent = tmpText;
                passwordInput.placeholder = tmpPlaceholder;

                var inputRect = inputObj.GetComponent<RectTransform>();
                inputRect.sizeDelta = new Vector2(0, 50);

                // Text rect setup with proper padding
                var textRect = inputText.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(15, 0);
                textRect.offsetMax = new Vector2(-15, 0);

                var placeholderRect = placeholder.GetComponent<RectTransform>();
                placeholderRect.anchorMin = Vector2.zero;
                placeholderRect.anchorMax = Vector2.one;
                placeholderRect.offsetMin = new Vector2(15, 0);
                placeholderRect.offsetMax = new Vector2(-15, 0);

                // Error text
                var errorObj = new GameObject("ErrorText");
                errorObj.transform.SetParent(passwordPromptPanel.transform, false);
                passwordErrorText = errorObj.AddComponent<TextMeshProUGUI>();
                passwordErrorText.text = "";
                passwordErrorText.fontSize = 14;
                passwordErrorText.alignment = TextAlignmentOptions.Center;
                passwordErrorText.color = Color.red;
                if (capturedTmpFont != null)
                {
                    passwordErrorText.font = capturedTmpFont;
                }
                var errorRect = errorObj.GetComponent<RectTransform>();
                errorRect.sizeDelta = new Vector2(0, 30);
                errorObj.SetActive(false);

                // Button container
                var buttonContainer = new GameObject("ButtonContainer");
                buttonContainer.transform.SetParent(passwordPromptPanel.transform, false);
                var buttonLayout = buttonContainer.AddComponent<HorizontalLayoutGroup>();
                buttonLayout.spacing = 15;
                buttonLayout.childControlWidth = true;
                buttonLayout.childControlHeight = true;
                buttonLayout.childForceExpandWidth = true;
                buttonLayout.childForceExpandHeight = false;
                var buttonContainerRect = buttonContainer.GetComponent<RectTransform>();
                buttonContainerRect.sizeDelta = new Vector2(0, 50);

                // Submit button
                passwordSubmitButton = CreateButton(buttonContainer.transform, "Submit", () => OnPasswordSubmit());
                logger.Msg($"Submit button created: {passwordSubmitButton != null}, interactable: {passwordSubmitButton?.interactable}");

                // Cancel button
                passwordCancelButton = CreateButton(buttonContainer.transform, "Cancel", () => OnPasswordCancel());
                logger.Msg($"Cancel button created: {passwordCancelButton != null}, interactable: {passwordCancelButton?.interactable}");

                // Initially hidden
                passwordPromptOverlay.SetActive(false);

                logger.Msg("Password prompt UI created successfully (standalone canvas with Don'tDestroyOnLoad)");
            }
            catch (Exception ex)
            {
                logger.Error($"Error creating password prompt UI: {ex}");
            }
        }

        /// <summary>
        /// Helper method to create a button.
        /// </summary>
        private Button CreateButton(Transform parent, string text, UnityAction onClick)
        {
            var buttonObj = new GameObject($"Button_{text}");
            buttonObj.transform.SetParent(parent, false);

            // Add LayoutElement for proper sizing in HorizontalLayoutGroup
            var layoutElement = buttonObj.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 50;
            layoutElement.flexibleWidth = 1;

            var buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = BTN_BG;
            buttonImage.raycastTarget = true; // CRITICAL for button clicks

            var button = buttonObj.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.onClick.AddListener(() => {
                logger.Msg($"Button '{text}' onClick triggered!");
                onClick?.Invoke();
            });

            // Better color transitions
            var colorBlock = button.colors;
            colorBlock.normalColor = new Color(1f, 1f, 1f, 1f);
            colorBlock.highlightedColor = new Color(0.8f, 0.8f, 1f, 1f);
            colorBlock.pressedColor = new Color(0.6f, 0.6f, 0.8f, 1f);
            colorBlock.selectedColor = new Color(0.9f, 0.9f, 1f, 1f);
            colorBlock.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            colorBlock.colorMultiplier = 1f;
            colorBlock.fadeDuration = 0.1f;
            button.colors = colorBlock;

            // Add navigation for keyboard/gamepad
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.Automatic;
            button.navigation = navigation;

            var buttonText = new GameObject("Text");
            buttonText.transform.SetParent(buttonObj.transform, false);
            var tmpButtonText = buttonText.AddComponent<TextMeshProUGUI>();
            tmpButtonText.text = text;
            tmpButtonText.fontSize = 18;
            tmpButtonText.fontStyle = FontStyles.Bold;
            tmpButtonText.alignment = TextAlignmentOptions.Center;
            tmpButtonText.color = Color.white;
            tmpButtonText.raycastTarget = false; // Don't block raycasts on text
            if (capturedTmpFont != null)
            {
                tmpButtonText.font = capturedTmpFont;
            }

            var textRect = buttonText.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            logger.Msg($"Created button '{text}' successfully");
            return button;
        }

        /// <summary>
        /// Handles password submit button click.
        /// </summary>
        private void OnPasswordSubmit()
        {
            try
            {
                logger.Msg("Submit button clicked!");
                
                // Check if messaging system is available FIRST
                var ds = ScheduleOne.UI.DailySummary.Instance;
                if (ds == null)
                {
                    logger.Warning("DailySummary.Instance is null - cannot send auth response!");
                    ShowAuthenticationError("Connection lost - please reconnect");
                    return;
                }
                
                if (passwordInput == null || string.IsNullOrEmpty(passwordInput.text))
                {
                    ShowAuthenticationError("Please enter a password");
                    return;
                }

                string password = passwordInput.text;
                logger.Msg("Password submitted, hashing and sending to server");

                // Hash the password
                string passwordHash = DedicatedServerMod.Utils.PasswordHasher.HashPassword(password);

                // Send to server
                DedicatedServerMod.Shared.Networking.MessageRouter.SendAuthenticationResponse(passwordHash);

                // Clear the input for security
                passwordInput.text = "";
                
                // Disable submit button while waiting for response
                if (passwordSubmitButton != null)
                {
                    passwordSubmitButton.interactable = false;
                    logger.Msg("Submit button disabled while waiting for server response");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error submitting password: {ex}");
                ShowAuthenticationError("Error submitting password");
                
                // Re-enable button on error so user can retry
                if (passwordSubmitButton != null)
                {
                    passwordSubmitButton.interactable = true;
                }
            }
        }

        /// <summary>
        /// Handles password cancel button click.
        /// </summary>
        private void OnPasswordCancel()
        {
            try
            {
                logger.Msg("Cancel button clicked!");
                logger.Msg("Password prompt cancelled by user");
                HidePasswordPrompt();

                // Disconnect from server
                connectionManager?.DisconnectFromDedicatedServer();
            }
            catch (Exception ex)
            {
                logger.Error($"Error cancelling password prompt: {ex}");
            }
        }
        
        /// <summary>
        /// Ensures an EventSystem exists for UI interaction.
        /// </summary>
        private void EnsureEventSystemExists()
        {
            try
            {
                // Check if EventSystem already exists
                var existingEventSystem = UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
                if (existingEventSystem != null)
                {
                    logger.Msg("EventSystem already exists");
                    return;
                }

                // Create EventSystem for UI input
                var eventSystemObj = new GameObject("PasswordDialogEventSystem");
                var eventSystem = eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                var inputModule = eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                
                UnityEngine.Object.DontDestroyOnLoad(eventSystemObj);
                
                logger.Msg("Created EventSystem for password dialog");
            }
            catch (Exception ex)
            {
                logger.Warning($"Failed to ensure EventSystem exists: {ex.Message}");
            }
        }

        /// <summary>
        /// Update method to be called from ClientBootstrap.OnUpdate
        /// Monitors EventSystem and UI state while dialog is active.
        /// </summary>
        public void Update()
        {
            if (!IsVisible || !isPasswordDialogActive)
                return;

            // Check EventSystem status - DON'T recreate if already showing dialog
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem == null)
            {
                logger.Warning("EventSystem is null while password dialog is active! Attempting to find existing...");
                
                // Try to find an existing EventSystem first
                var existing = UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
                if (existing != null)
                {
                    logger.Msg("Found existing EventSystem, activating it");
                    existing.gameObject.SetActive(true);
                }
                else
                {
                    logger.Warning("No EventSystem found - this will break button clicks! Creating new one...");
                    EnsureEventSystemExists();
                }
            }

        }
    }
}
