using MelonLoader;
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        private GameObject serversButton;
        private bool menuUISetup = false;

        // Server menu UI
        private GameObject serverMenuOverlay;
        private GameObject serverMenuPanel;
        private CanvasGroup serverMenuCanvasGroup;
        private TMP_InputField serverAddressInput;
        private TMP_Text serverMenuStatusText;
        private Button serverMenuConnectButton;
        private Button serverMenuListButton;
        private Button serverMenuCloseButton;

        // Theme
        private static readonly Color ACCENT = new Color(0.10f, 0.65f, 1f, 1f);
        private static readonly Color PANEL_BG = new Color(0.08f, 0.09f, 0.12f, 0.96f);
        private static readonly Color OVERLAY_DIM = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color INPUT_BG = new Color(0.12f, 0.13f, 0.16f, 1f);
        private static readonly Color BTN_BG = new Color(0.18f, 0.20f, 0.25f, 0.95f);
        private static readonly Color BTN_BG_HOVER = new Color(0.22f, 0.24f, 0.30f, 0.95f);
        private static readonly Color BTN_BG_PRESSED = new Color(0.14f, 0.16f, 0.20f, 0.95f);

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
                if (AddServersButton())
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
        private bool AddServersButton()
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

                // Create servers button by cloning continue button
                serversButton = GameObject.Instantiate(continueButton.gameObject, bank);
                serversButton.name = "ServersButton";
                
                // Position it to the right of the continue button so it's not blocked
                PositionServersButton(serversButton, continueButton);
                
                // Update button appearance
                UpdateServersButtonText(serversButton);
                
                // Replace original button component to strip persistent onClick listeners from the cloned Continue button
                StripPersistentOnClick(serversButton);

                // Setup button functionality
                SetupServersButtonClick(serversButton);

                logger.Msg("Servers button added to main menu successfully");
                return true;
            }
            catch (Exception ex)
            {
                logger.Error($"Error adding servers button: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Position the prototype button relative to the continue button
        /// </summary>
        private void PositionServersButton(GameObject newButton, Transform continueButton)
        {
            var rectTransform = newButton.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                var pos = rectTransform.anchoredPosition;
                rectTransform.anchoredPosition = new Vector2(pos.x + 100f, pos.y);
            }
        }

        /// <summary>
        /// Update the button text to reflect its purpose
        /// </summary>
        private void UpdateServersButtonText(GameObject button)
        {
            var tmp = button.GetComponentInChildren<TMP_Text>();
            if (tmp != null)
            {
                tmp.text = "Servers";
                return;
            }
            var legacy = button.GetComponentInChildren<Text>();
            if (legacy != null)
            {
                legacy.text = "Servers";
                return;
            }
            logger.Warning("Could not find text component on servers button");
        }

        /// <summary>
        /// Remove any persistent listeners copied from the original Continue button, ensuring only our handler runs.
        /// </summary>
        private void StripPersistentOnClick(GameObject button)
        {
            try
            {
                var btn = button.GetComponent<Button>();
                if (btn == null)
                {
                    logger.Warning("StripPersistentOnClick: Button component not found");
                    return;
                }

                // Replace the entire UnityEvent to ensure no persistent listeners remain
                btn.onClick = new Button.ButtonClickedEvent();
            }
            catch (Exception ex)
            {
                logger.Error($"Error stripping onClick listeners: {ex}");
            }
        }

        /// <summary>
        /// Setup the button click handler
        /// </summary>
        private void SetupServersButtonClick(GameObject button)
        {
            var buttonComponent = button.GetComponent<Button>();
            if (buttonComponent != null)
            {
                buttonComponent.onClick.RemoveAllListeners();
                buttonComponent.onClick.AddListener(OnServersButtonClicked);
            }
            else
            {
                logger.Warning("Could not find Button component on servers button");
            }
        }

        /// <summary>
        /// Handle prototype button click
        /// </summary>
        private void OnServersButtonClicked()
        {
            try
            {
                logger.Msg("Servers button clicked - opening server menu");
                ToggleServerMenu(true);
            }
            catch (Exception ex)
            {
                logger.Error($"Error handling servers button click: {ex}");
            }
        }

        /// <summary>
        /// Update button text based on connection state
        /// </summary>
        public void UpdateButtonState()
        {
            if (serversButton == null)
                return;

            try
            {
                var textComponent = serversButton.GetComponentInChildren<Text>();
                if (textComponent != null)
                {
                    textComponent.text = "Servers";
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
                if (Input.GetKeyDown(KeyCode.F8))
                {
                    ShowConnectionStatus();
                }

                // F10 - Update button state
                if (Input.GetKeyDown(KeyCode.F10))
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
                if (serversButton != null)
                {
                    GameObject.Destroy(serversButton);
                    serversButton = null;
                }
                
                if (serverMenuPanel != null)
                {
                    GameObject.Destroy(serverMenuPanel);
                    serverMenuPanel = null;
                    serverAddressInput = null;
                    serverMenuStatusText = null;
                    serverMenuConnectButton = null;
                    serverMenuListButton = null;
                    serverMenuCloseButton = null;
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

        private void ToggleServerMenu(bool show)
        {
            try
            {
                if (serverMenuPanel == null && show)
                {
                    CreateServerMenuUI();
                }

                if (serverMenuPanel != null)
                {
                    if (serverMenuOverlay != null)
                    {
                        serverMenuOverlay.SetActive(show);
                    }
                    serverMenuPanel.SetActive(show);
                    if (serverMenuCanvasGroup != null)
                    {
                        serverMenuCanvasGroup.alpha = show ? 1f : 0f;
                        serverMenuCanvasGroup.interactable = show;
                        serverMenuCanvasGroup.blocksRaycasts = show;
                    }
                    if (show)
                    {
                        PrefillServerAddress();
                        UpdateServerMenuState();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error toggling server menu: {ex}");
            }
        }

        private void PrefillServerAddress()
        {
            try
            {
                if (serverAddressInput != null)
                {
                    var target = ClientConnectionManager.GetTargetServer();
                    serverAddressInput.text = $"{target.ip}:{target.port}";
                    serverAddressInput.caretPosition = serverAddressInput.text.Length;
                    serverAddressInput.selectionStringAnchorPosition = serverAddressInput.caretPosition;
                    serverAddressInput.selectionStringFocusPosition = serverAddressInput.caretPosition;
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error pre-filling server address: {ex}");
            }
        }

        private void CreateServerMenuUI()
        {
            // Find main menu root to attach our panel
            var mainMenu = GameObject.Find("MainMenu");
            if (mainMenu == null)
            {
                logger.Warning("Cannot create server menu - MainMenu not found");
                return;
            }

            // Overlay dim
            serverMenuOverlay = new GameObject("ServerMenuOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            serverMenuOverlay.transform.SetParent(mainMenu.transform, false);
            var overlayRect = serverMenuOverlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = new Vector2(0f, 0f);
            overlayRect.anchorMax = new Vector2(1f, 1f);
            overlayRect.pivot = new Vector2(0.5f, 0.5f);
            overlayRect.sizeDelta = Vector2.zero;
            var overlayImage = serverMenuOverlay.GetComponent<Image>();
            overlayImage.color = OVERLAY_DIM;

            // Panel
            serverMenuPanel = new GameObject("ServerMenuPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            serverMenuPanel.transform.SetParent(serverMenuOverlay.transform, false);

            serverMenuCanvasGroup = serverMenuPanel.GetComponent<CanvasGroup>();
            serverMenuCanvasGroup.alpha = 1f;

            var panelRect = serverMenuPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(680f, 380f);
            panelRect.anchoredPosition = new Vector2(0f, 0f);

            var panelImage = serverMenuPanel.GetComponent<Image>();
            panelImage.color = PANEL_BG;
            panelImage.raycastTarget = true;

            // Panel decorative border
            var border = new GameObject("Border", typeof(RectTransform), typeof(Image));
            border.transform.SetParent(serverMenuPanel.transform, false);
            var borderRect = border.GetComponent<RectTransform>();
            borderRect.anchorMin = new Vector2(0f, 0f);
            borderRect.anchorMax = new Vector2(1f, 1f);
            borderRect.offsetMin = new Vector2(2f, 2f);
            borderRect.offsetMax = new Vector2(-2f, -2f);
            var borderImage = border.GetComponent<Image>();
            borderImage.color = new Color(1f, 1f, 1f, 0.03f);

            // Title
            var titleGO = new GameObject("Title", typeof(RectTransform));
            titleGO.transform.SetParent(serverMenuPanel.transform, false);
            var titleRect = titleGO.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(560f, 40f);
            titleRect.anchoredPosition = new Vector2(0f, -18f);
            var titleText = titleGO.AddComponent<TextMeshProUGUI>();
            titleText.text = "Dedicated Servers";
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.fontSize = 30;
            titleText.color = Color.white;
            titleText.fontStyle = FontStyles.Bold;

            // Address label
            var addrLabelGO = new GameObject("AddressLabel", typeof(RectTransform));
            addrLabelGO.transform.SetParent(serverMenuPanel.transform, false);
            var addrLabelRect = addrLabelGO.GetComponent<RectTransform>();
            addrLabelRect.anchorMin = new Vector2(0f, 1f);
            addrLabelRect.anchorMax = new Vector2(0f, 1f);
            addrLabelRect.pivot = new Vector2(0f, 1f);
            addrLabelRect.sizeDelta = new Vector2(200f, 26f);
            addrLabelRect.anchoredPosition = new Vector2(28f, -84f);
            var addrLabelText = addrLabelGO.AddComponent<TextMeshProUGUI>();
            addrLabelText.text = "Server (IP:Port)";
            addrLabelText.alignment = TextAlignmentOptions.Left;
            addrLabelText.fontSize = 20;
            addrLabelText.color = new Color(1f, 1f, 1f, 0.9f);

            // Address input background
            var addrBG = new GameObject("AddressInputBG", typeof(RectTransform), typeof(Image));
            addrBG.transform.SetParent(serverMenuPanel.transform, false);
            var addrBGRect = addrBG.GetComponent<RectTransform>();
            addrBGRect.anchorMin = new Vector2(0f, 1f);
            addrBGRect.anchorMax = new Vector2(0f, 1f);
            addrBGRect.pivot = new Vector2(0f, 1f);
            addrBGRect.sizeDelta = new Vector2(440f, 44f);
            addrBGRect.anchoredPosition = new Vector2(28f, -118f);
            var addrBGImage = addrBG.GetComponent<Image>();
            addrBGImage.color = INPUT_BG;
            addrBGImage.raycastTarget = true;

            // Input border accent
            var addrBorder = new GameObject("AddressBorder", typeof(RectTransform), typeof(Image));
            addrBorder.transform.SetParent(addrBG.transform, false);
            var addrBorderRect = addrBorder.GetComponent<RectTransform>();
            addrBorderRect.anchorMin = new Vector2(0f, 0f);
            addrBorderRect.anchorMax = new Vector2(1f, 0f);
            addrBorderRect.pivot = new Vector2(0.5f, 0f);
            addrBorderRect.sizeDelta = new Vector2(0f, 2f);
            addrBorderRect.anchoredPosition = new Vector2(0f, 0f);
            var addrBorderImage = addrBorder.GetComponent<Image>();
            addrBorderImage.color = ACCENT;

            // Address input
            var addrInputGO = new GameObject("AddressInput", typeof(RectTransform));
            addrInputGO.transform.SetParent(addrBG.transform, false);
            var addrInputRect = addrInputGO.GetComponent<RectTransform>();
            addrInputRect.anchorMin = new Vector2(0f, 0f);
            addrInputRect.anchorMax = new Vector2(1f, 1f);
            addrInputRect.pivot = new Vector2(0.5f, 0.5f);
            addrInputRect.sizeDelta = new Vector2(-16f, -16f);
            addrInputRect.anchoredPosition = new Vector2(0f, 0f);
            serverAddressInput = addrInputGO.AddComponent<TMP_InputField>();
            var addrText = addrInputGO.AddComponent<TextMeshProUGUI>();
            addrText.fontSize = 18;
            addrText.color = Color.white;
            addrText.enableWordWrapping = false;
            addrText.alignment = TextAlignmentOptions.MidlineLeft;
            serverAddressInput.textComponent = addrText;
            var placeholder = new GameObject("Placeholder", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            placeholder.transform.SetParent(addrInputGO.transform, false);
            var phRect = placeholder.GetComponent<RectTransform>();
            phRect.anchorMin = new Vector2(0f, 0f);
            phRect.anchorMax = new Vector2(1f, 1f);
            phRect.offsetMin = new Vector2(0f, 0f);
            phRect.offsetMax = new Vector2(0f, 0f);
            placeholder.text = "e.g. 127.0.0.1:38465";
            placeholder.fontSize = 18;
            placeholder.color = new Color(1f, 1f, 1f, 0.35f);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            serverAddressInput.placeholder = placeholder;

            // Connect button
            serverMenuConnectButton = CreateStyledButton(serverMenuPanel.transform, new Vector2(488f, -118f), new Vector2(152f, 44f), "Connect");
            serverMenuConnectButton.onClick.AddListener(OnConnectClicked);

            // Server list button
            serverMenuListButton = CreateStyledButton(serverMenuPanel.transform, new Vector2(28f, -178f), new Vector2(180f, 40f), "Server List");
            serverMenuListButton.onClick.AddListener(OnServerListClicked);

            // Status text
            var statusGO = new GameObject("StatusText", typeof(RectTransform));
            statusGO.transform.SetParent(serverMenuPanel.transform, false);
            var statusRect = statusGO.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 0f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.sizeDelta = new Vector2(-40f, 60f);
            statusRect.anchoredPosition = new Vector2(0f, 20f);
            serverMenuStatusText = statusGO.AddComponent<TextMeshProUGUI>();
            serverMenuStatusText.text = "";
            serverMenuStatusText.alignment = TextAlignmentOptions.TopLeft;
            serverMenuStatusText.fontSize = 16;
            serverMenuStatusText.color = new Color(1f, 1f, 1f, 0.9f);

            // Close button
            serverMenuCloseButton = CreateIconButton(serverMenuPanel.transform, new Vector2(640f, -24f), new Vector2(28f, 28f), "X");
            var closeRect = serverMenuCloseButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-24f, -24f);
            serverMenuCloseButton.onClick.AddListener(() => ToggleServerMenu(false));

            // Hide initially
            serverMenuOverlay.SetActive(false);
            serverMenuPanel.SetActive(false);
        }

        private Button CreateStyledButton(Transform parent, Vector2 anchoredPosition, Vector2 size, string label)
        {
            var buttonGO = new GameObject($"Button_{label}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonGO.transform.SetParent(parent, false);
            var rect = buttonGO.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            var image = buttonGO.GetComponent<Image>();
            image.color = BTN_BG;

            var colors = new ColorBlock
            {
                colorMultiplier = 1f,
                disabledColor = new Color(1f, 1f, 1f, 0.3f),
                highlightedColor = BTN_BG_HOVER,
                normalColor = BTN_BG,
                pressedColor = BTN_BG_PRESSED,
                selectedColor = BTN_BG
            };
            var btn = buttonGO.GetComponent<Button>();
            btn.colors = colors;

            var textGO = new GameObject("Text", typeof(RectTransform));
            textGO.transform.SetParent(buttonGO.transform, false);
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = new Vector2(-10f, -10f);
            var text = textGO.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 20;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;

            return btn;
        }

        private Button CreateIconButton(Transform parent, Vector2 anchoredPosition, Vector2 size, string label)
        {
            var button = CreateStyledButton(parent, anchoredPosition, size, label);
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            text.fontSize = 16;
            text.color = new Color(1f, 1f, 1f, 0.85f);
            return button;
        }

        private void OnConnectClicked()
        {
            try
            {
                string input = serverAddressInput != null ? serverAddressInput.text : string.Empty;
                if (!TryParseAddress(input, out string ip, out int port))
                {
                    SetStatusText("Invalid address. Use IP:Port, e.g. 127.0.0.1:38465");
                    return;
                }

                connectionManager.SetTargetServer(ip, port);
                SetStatusText($"Connecting to {ip}:{port}...");
                UpdateServerMenuState();
                connectionManager.StartDedicatedConnection();
            }
            catch (Exception ex)
            {
                logger.Error($"Error handling connect click: {ex}");
            }
        }

        private void OnServerListClicked()
        {
            try
            {
                SetStatusText("Server list not implemented yet.");
            }
            catch (Exception ex)
            {
                logger.Error($"Error handling server list click: {ex}");
            }
        }

        private void SetStatusText(string message)
        {
            if (serverMenuStatusText != null)
            {
                serverMenuStatusText.text = message ?? string.Empty;
            }
        }

        private void UpdateServerMenuState()
        {
            try
            {
                bool isConnecting = connectionManager.IsConnecting;
                if (serverMenuConnectButton != null)
                {
                    serverMenuConnectButton.interactable = !isConnecting;
                }
                if (serverAddressInput != null)
                {
                    serverAddressInput.interactable = !isConnecting;
                }
                if (serverMenuStatusText != null)
                {
                    if (isConnecting)
                    {
                        serverMenuStatusText.text = "Connecting...";
                    }
                    else if (connectionManager.IsConnectedToDedicatedServer)
                    {
                        serverMenuStatusText.text = "Connected";
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error updating server menu state: {ex}");
            }
        }

        private bool TryParseAddress(string input, out string ip, out int port)
        {
            ip = null;
            port = 0;

            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            input = input.Trim();

            int colonIndex = input.LastIndexOf(':');
            if (colonIndex > 0 && colonIndex < input.Length - 1)
            {
                string ipPart = input.Substring(0, colonIndex);
                string portPart = input.Substring(colonIndex + 1);
                if (int.TryParse(portPart, out int parsedPort) && parsedPort > 0 && parsedPort <= 65535)
                {
                    ip = ipPart;
                    port = parsedPort;
                    return true;
                }
                return false;
            }
            else
            {
                // No port provided; use current default
                var target = ClientConnectionManager.GetTargetServer();
                ip = input;
                port = target.port;
                return true;
            }
        }
    }
}
