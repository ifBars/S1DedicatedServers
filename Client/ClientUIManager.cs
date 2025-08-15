using MelonLoader;
using System;
using System.Collections;
using UnityEngine;

namespace DedicatedServerMod.Client
{
    /// <summary>
    /// Manages UI additions for the dedicated server client mod.
    /// Handles adding the prototype connection button and other UI elements.
    /// </summary>
    public class ClientUIManager
    {
        private readonly MelonLogger.Instance logger;
        private readonly ClientConnectionManager connectionManager;
        
        // UI state
        private GameObject prototypeButton;
        private bool menuUISetup = false;

        public ClientUIManager(MelonLogger.Instance logger, ClientConnectionManager connectionManager)
        {
            this.logger = logger;
            this.connectionManager = connectionManager;
        }

        public void Initialize()
        {
            try
            {
                logger.Msg("Initializing ClientUIManager");
                
                // UI will be setup when menu scene loads
                
                logger.Msg("ClientUIManager initialized");
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to initialize ClientUIManager: {ex}");
            }
        }

        /// <summary>
        /// Handle scene loading events
        /// </summary>
        public void OnSceneLoaded(string sceneName)
        {
            try
            {
                if (sceneName == "Menu" && !menuUISetup)
                {
                    logger.Msg("Menu scene loaded - setting up prototype UI");
                    MelonCoroutines.Start(SetupMenuUI());
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error handling scene load ({sceneName}): {ex}");
            }
        }

        /// <summary>
        /// Setup UI elements in the main menu
        /// </summary>
        private IEnumerator SetupMenuUI()
        {
            // Wait for main menu to be fully loaded
            yield return new WaitForSeconds(0.5f);
            
            try
            {
                if (AddPrototypeButton())
                {
                    menuUISetup = true;
                    logger.Msg("Menu UI setup completed");
                }
                else
                {
                    logger.Warning("Failed to setup menu UI - will retry next time menu loads");
                    menuUISetup = false;
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error setting up menu UI: {ex}");
                menuUISetup = false;
            }
        }

        /// <summary>
        /// Add the prototype server connection button to the main menu
        /// </summary>
        private bool AddPrototypeButton()
        {
            try
            {
                // Find main menu structure
                var mainMenu = GameObject.Find("MainMenu");
                if (mainMenu == null)
                {
                    logger.Warning("MainMenu not found");
                    return false;
                }

                var home = mainMenu.transform.Find("Home");
                if (home == null)
                {
                    logger.Warning("Home not found in MainMenu");
                    return false;
                }

                var bank = home.Find("Bank");
                if (bank == null)
                {
                    logger.Warning("Bank not found in Home");
                    return false;
                }

                var continueButton = bank.Find("Continue");
                if (continueButton == null)
                {
                    logger.Warning("Continue button not found");
                    return false;
                }

                // Create prototype button by cloning continue button
                prototypeButton = GameObject.Instantiate(continueButton.gameObject, bank);
                prototypeButton.name = "PrototypeServerButton";
                
                // Position it below continue button
                PositionPrototypeButton(prototypeButton, continueButton);
                
                // Update button appearance
                UpdateButtonText(prototypeButton);
                
                // Setup button functionality
                SetupButtonClick(prototypeButton);

                logger.Msg("Prototype button added to main menu successfully");
                return true;
            }
            catch (Exception ex)
            {
                logger.Error($"Error adding prototype button: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Position the prototype button relative to the continue button
        /// </summary>
        private void PositionPrototypeButton(GameObject prototypeButton, Transform continueButton)
        {
            var rectTransform = prototypeButton.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                var pos = rectTransform.anchoredPosition;
                rectTransform.anchoredPosition = new Vector2(pos.x, pos.y - 60f);
            }
        }

        /// <summary>
        /// Update the button text to reflect its purpose
        /// </summary>
        private void UpdateButtonText(GameObject button)
        {
            var textComponent = button.GetComponentInChildren<UnityEngine.UI.Text>();
            if (textComponent != null)
            {
                textComponent.text = "Connect to Dedicated Server (Prototype)";
            }
            else
            {
                logger.Warning("Could not find text component on prototype button");
            }
        }

        /// <summary>
        /// Setup the button click handler
        /// </summary>
        private void SetupButtonClick(GameObject button)
        {
            var buttonComponent = button.GetComponent<UnityEngine.UI.Button>();
            if (buttonComponent != null)
            {
                buttonComponent.onClick.RemoveAllListeners();
                buttonComponent.onClick.AddListener(OnPrototypeButtonClicked);
            }
            else
            {
                logger.Warning("Could not find Button component on prototype button");
            }
        }

        /// <summary>
        /// Handle prototype button click
        /// </summary>
        private void OnPrototypeButtonClicked()
        {
            try
            {
                logger.Msg("Prototype server button clicked");
                
                if (connectionManager.IsConnecting)
                {
                    logger.Msg("Connection already in progress - ignoring button click");
                    return;
                }

                if (connectionManager.IsConnectedToDedicatedServer)
                {
                    logger.Msg("Already connected to dedicated server - disconnecting first");
                    connectionManager.DisconnectFromDedicatedServer();
                    return;
                }

                // Start dedicated server connection
                connectionManager.StartDedicatedConnection();
            }
            catch (Exception ex)
            {
                logger.Error($"Error handling prototype button click: {ex}");
            }
        }

        /// <summary>
        /// Update button text based on connection state
        /// </summary>
        public void UpdateButtonState()
        {
            if (prototypeButton == null)
                return;

            try
            {
                var textComponent = prototypeButton.GetComponentInChildren<UnityEngine.UI.Text>();
                if (textComponent != null)
                {
                    if (connectionManager.IsConnecting)
                    {
                        textComponent.text = "Connecting to Dedicated Server...";
                    }
                    else if (connectionManager.IsConnectedToDedicatedServer)
                    {
                        textComponent.text = "Disconnect from Dedicated Server";
                    }
                    else
                    {
                        textComponent.text = "Connect to Dedicated Server (Prototype)";
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error updating button state: {ex}");
            }
        }

        /// <summary>
        /// Show connection status in a temporary UI element
        /// </summary>
        public void ShowConnectionStatus()
        {
            try
            {
                var status = connectionManager.GetConnectionStatus();
                logger.Msg("=== Connection Status ===");
                logger.Msg(status);
                
                // In a full implementation, this could show a popup or console overlay
                // For now, just log to console
            }
            catch (Exception ex)
            {
                logger.Error($"Error showing connection status: {ex}");
            }
        }

        /// <summary>
        /// Add debug UI elements (for development/testing)
        /// </summary>
        public void AddDebugUI()
        {
            try
            {
                // This could add debug panels, status displays, etc.
                // For now, just provide console commands via key bindings
                logger.Msg("Debug UI available via console commands");
            }
            catch (Exception ex)
            {
                logger.Error($"Error adding debug UI: {ex}");
            }
        }

        /// <summary>
        /// Handle debug key inputs for UI testing
        /// </summary>
        public void HandleDebugInput()
        {
            try
            {
                // F8 - Show connection status
                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F8))
                {
                    ShowConnectionStatus();
                }

                // F10 - Update button state
                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F10))
                {
                    UpdateButtonState();
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error handling debug input: {ex}");
            }
        }

        /// <summary>
        /// Remove UI elements when cleaning up
        /// </summary>
        public void Cleanup()
        {
            try
            {
                if (prototypeButton != null)
                {
                    GameObject.Destroy(prototypeButton);
                    prototypeButton = null;
                }
                
                menuUISetup = false;
                logger.Msg("UI elements cleaned up");
            }
            catch (Exception ex)
            {
                logger.Error($"Error during UI cleanup: {ex}");
            }
        }

        /// <summary>
        /// Reset UI state when returning to menu
        /// </summary>
        public void ResetUIState()
        {
            try
            {
                menuUISetup = false;
                UpdateButtonState();
            }
            catch (Exception ex)
            {
                logger.Error($"Error resetting UI state: {ex}");
            }
        }
    }
}
