using MelonLoader;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

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

        // Theme colors (matching main UI)
        private static readonly Color PANEL_BG = new Color(0.08f, 0.09f, 0.12f, 0.96f);
        private static readonly Color OVERLAY_DIM = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color INPUT_BG = new Color(0.12f, 0.13f, 0.16f, 1f);
        private static readonly Color BTN_BG = new Color(0.18f, 0.20f, 0.25f, 0.95f);

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

                // Show the overlay
                passwordPromptOverlay.SetActive(true);

                // Focus the input field
                if (passwordInput != null)
                {
                    passwordInput.Select();
                    passwordInput.ActivateInputField();
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error showing password prompt: {ex}");
            }
        }

        /// <summary>
        /// Hides the password prompt dialog.
        /// </summary>
        public void HidePasswordPrompt()
        {
            try
            {
                if (passwordPromptOverlay != null)
                {
                    passwordPromptOverlay.SetActive(false);
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error hiding password prompt: {ex}");
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
                logger.Warning($"Authentication error: {errorMessage}");

                if (passwordErrorText != null)
                {
                    passwordErrorText.text = errorMessage;
                    passwordErrorText.gameObject.SetActive(true);
                }

                // Clear password input for retry
                if (passwordInput != null)
                {
                    passwordInput.text = "";
                    passwordInput.Select();
                    passwordInput.ActivateInputField();
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error showing authentication error: {ex}");
            }
        }

        /// <summary>
        /// Creates the password prompt UI overlay.
        /// </summary>
        private void CreatePasswordPromptUI()
        {
            try
            {
                // Get the canvas
                var canvas = GameObject.Find("Canvas")?.GetComponent<Canvas>();
                if (canvas == null)
                {
                    logger.Error("Canvas not found for password prompt");
                    return;
                }

                // Create overlay
                passwordPromptOverlay = new GameObject("PasswordPromptOverlay");
                passwordPromptOverlay.transform.SetParent(canvas.transform, false);

                // Add canvas group for fading
                var canvasGroup = passwordPromptOverlay.AddComponent<CanvasGroup>();

                // Add full-screen blocker image
                var blockerImage = passwordPromptOverlay.AddComponent<Image>();
                blockerImage.color = OVERLAY_DIM;
                var blockerRect = passwordPromptOverlay.GetComponent<RectTransform>();
                blockerRect.anchorMin = Vector2.zero;
                blockerRect.anchorMax = Vector2.one;
                blockerRect.sizeDelta = Vector2.zero;

                // Create panel
                passwordPromptPanel = new GameObject("PasswordPromptPanel");
                passwordPromptPanel.transform.SetParent(passwordPromptOverlay.transform, false);

                var panelImage = passwordPromptPanel.AddComponent<Image>();
                panelImage.color = PANEL_BG;

                var panelRect = passwordPromptPanel.GetComponent<RectTransform>();
                panelRect.anchorMin = new Vector2(0.5f, 0.5f);
                panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                panelRect.sizeDelta = new Vector2(400, 250);
                panelRect.anchoredPosition = Vector2.zero;

                // Add vertical layout group
                var layout = passwordPromptPanel.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(20, 20, 20, 20);
                layout.spacing = 15;
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
                passwordPromptTitle.fontSize = 20;
                passwordPromptTitle.alignment = TextAlignmentOptions.Center;
                passwordPromptTitle.color = Color.white;
                if (capturedTmpFont != null)
                {
                    passwordPromptTitle.font = capturedTmpFont;
                }
                var titleRect = titleObj.GetComponent<RectTransform>();
                titleRect.sizeDelta = new Vector2(0, 60);

                // Password input
                var inputObj = new GameObject("PasswordInput");
                inputObj.transform.SetParent(passwordPromptPanel.transform, false);
                var inputImage = inputObj.AddComponent<Image>();
                inputImage.color = INPUT_BG;

                passwordInput = inputObj.AddComponent<TMP_InputField>();
                passwordInput.inputType = TMP_InputField.InputType.Password;
                passwordInput.asteriskChar = '•';

                var inputText = new GameObject("Text");
                inputText.transform.SetParent(inputObj.transform, false);
                var tmpText = inputText.AddComponent<TextMeshProUGUI>();
                tmpText.fontSize = 16;
                tmpText.color = Color.white;
                if (capturedTmpFont != null)
                {
                    tmpText.font = capturedTmpFont;
                }

                var placeholder = new GameObject("Placeholder");
                placeholder.transform.SetParent(inputObj.transform, false);
                var tmpPlaceholder = placeholder.AddComponent<TextMeshProUGUI>();
                tmpPlaceholder.text = "Enter password...";
                tmpPlaceholder.fontSize = 16;
                tmpPlaceholder.color = new Color(1, 1, 1, 0.5f);
                if (capturedTmpFont != null)
                {
                    tmpPlaceholder.font = capturedTmpFont;
                }

                passwordInput.textComponent = tmpText;
                passwordInput.placeholder = tmpPlaceholder;

                var inputRect = inputObj.GetComponent<RectTransform>();
                inputRect.sizeDelta = new Vector2(0, 40);

                // Text rect setup
                var textRect = inputText.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(10, 0);
                textRect.offsetMax = new Vector2(-10, 0);

                var placeholderRect = placeholder.GetComponent<RectTransform>();
                placeholderRect.anchorMin = Vector2.zero;
                placeholderRect.anchorMax = Vector2.one;
                placeholderRect.offsetMin = new Vector2(10, 0);
                placeholderRect.offsetMax = new Vector2(-10, 0);

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
                buttonLayout.spacing = 10;
                buttonLayout.childControlWidth = true;
                buttonLayout.childControlHeight = true;
                buttonLayout.childForceExpandWidth = true;
                buttonLayout.childForceExpandHeight = false;
                var buttonContainerRect = buttonContainer.GetComponent<RectTransform>();
                buttonContainerRect.sizeDelta = new Vector2(0, 40);

                // Submit button
                passwordSubmitButton = CreateButton(buttonContainer.transform, "Submit", () => OnPasswordSubmit());

                // Cancel button
                passwordCancelButton = CreateButton(buttonContainer.transform, "Cancel", () => OnPasswordCancel());

                // Initially hidden
                passwordPromptOverlay.SetActive(false);

                logger.Msg("Password prompt UI created successfully");
            }
            catch (Exception ex)
            {
                logger.Error($"Error creating password prompt UI: {ex}");
            }
        }

        /// <summary>
        /// Handles password submit button click.
        /// </summary>
        private void OnPasswordSubmit()
        {
            try
            {
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
            }
            catch (Exception ex)
            {
                logger.Error($"Error submitting password: {ex}");
                ShowAuthenticationError("Error submitting password");
            }
        }

        /// <summary>
        /// Handles password cancel button click.
        /// </summary>
        private void OnPasswordCancel()
        {
            try
            {
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
        /// Helper method to create a button.
        /// </summary>
        private Button CreateButton(Transform parent, string text, UnityAction onClick)
        {
            var buttonObj = new GameObject($"Button_{text}");
            buttonObj.transform.SetParent(parent, false);

            var buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = BTN_BG;

            var button = buttonObj.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.onClick.AddListener(onClick);

            var colorBlock = button.colors;
            colorBlock.normalColor = Color.white;
            colorBlock.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            colorBlock.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            button.colors = colorBlock;

            var buttonText = new GameObject("Text");
            buttonText.transform.SetParent(buttonObj.transform, false);
            var tmpButtonText = buttonText.AddComponent<TextMeshProUGUI>();
            tmpButtonText.text = text;
            tmpButtonText.fontSize = 16;
            tmpButtonText.alignment = TextAlignmentOptions.Center;
            tmpButtonText.color = Color.white;
            if (capturedTmpFont != null)
            {
                tmpButtonText.font = capturedTmpFont;
            }

            var textRect = buttonText.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            return button;
        }
    }
}
