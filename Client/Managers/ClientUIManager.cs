using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using DedicatedServerMod.Assets;
using DedicatedServerMod.Client.Data;
using DedicatedServerMod.Utils;
#if IL2CPP
using Il2CppFishNet;
using Il2CppTMPro;
#else
using FishNet;
using TMPro;
#endif
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace DedicatedServerMod.Client.Managers
{
    /// <summary>
    /// Manages UI additions for the dedicated server client mod.
    /// Handles adding the prototype connection button and other UI elements.
    /// </summary>
    public sealed class ClientUIManager
    {
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

        // AssetBundle-driven UI (replaces runtime-created UI)
        private AssetBundle dedicatedUiBundle;
        private Transform dsServerBrowserPanel;
        private Transform dsDirectConnectPanel;
        private Button dsOpenDirectConnectButton;
        private Button dsConnectButton;
        private Button dsCancelButton;
        private TMP_InputField dsIpInput;
        private TMP_InputField dsPortInput;
        private TMP_Text dsDirectConnectStatusText;
        private Button dsAddServerButton;
        private Button dsRefreshButton;
        private Button dsFavoritesButton;
        private Button dsHistoryButton;
        private Transform dsFavoritesListPanel;
        private Transform dsHistoryListPanel;
        private Transform dsFavoritesContent;
        private Transform dsHistoryContent;
        private GameObject dsFavoritesEntryTemplate;
        private GameObject dsHistoryEntryTemplate;
        private TMP_Text dsFavoritesEmptyPlaceholder;
        private TMP_Text dsHistoryEmptyPlaceholder;
        private GameObject dsAddServerPanel;
        private TMP_Text dsAddServerTitleText;
        private TMP_Text dsAddServerStatusText;
        private TMP_InputField dsFavoriteNameInput;
        private TMP_InputField dsFavoriteIpInput;
        private TMP_InputField dsFavoritePortInput;
        private Button dsFavoriteSaveButton;
        private Button dsFavoriteCancelButton;
        private string editingFavoriteId;
        private string pendingHistoryName;
        private readonly List<GameObject> spawnedFavoriteEntries = new List<GameObject>();
        private readonly List<GameObject> spawnedHistoryEntries = new List<GameObject>();
        private readonly ClientServerListRepository serverListRepository;
        private readonly ServerStatusQueryService serverStatusQueryService = new ServerStatusQueryService();
        private readonly HashSet<string> statusQueriesInFlight = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private object pendingMenuCursorRestoreCoroutine;
        private ServerBrowserTab activeTab = ServerBrowserTab.Favorites;
        private float gameplayPingSampleTimer;

        // Menu animation controller
        private MenuAnimationController menuAnimationController;

        // Theme
        private static readonly Color ACCENT = new Color(0.10f, 0.65f, 1f, 1f);
        private static readonly Color PANEL_BG = new Color(0.08f, 0.09f, 0.12f, 0.96f);
        private static readonly Color OVERLAY_DIM = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color INPUT_BG = new Color(0.12f, 0.13f, 0.16f, 1f);
        private static readonly Color BTN_BG = new Color(0.18f, 0.20f, 0.25f, 0.95f);
        private static readonly Color BTN_BG_HOVER = new Color(0.22f, 0.24f, 0.30f, 0.95f);
        private static readonly Color BTN_BG_PRESSED = new Color(0.14f, 0.16f, 0.20f, 0.95f);

        // Captured fonts from existing UI to apply to AssetBundle panels
        private TMP_FontAsset capturedTmpFont;
        private Material capturedTmpMaterial;
        private Font capturedLegacyFont;

        internal ClientUIManager(ClientConnectionManager connectionManager)
        {
            this.connectionManager = connectionManager;
            serverListRepository = new ClientServerListRepository();
        }

        internal void Initialize()
        {
            try
            {
                serverListRepository.Initialize();
                serverListRepository.Changed += OnServerListRepositoryChanged;
                connectionManager.DedicatedServerConnected += OnDedicatedServerConnected;
                ServerDataStore.OnUpdated += OnServerDataUpdated;
                gameplayPingSampleTimer = 0f;
                
                // Initialize menu animation controller
                menuAnimationController = new MenuAnimationController();
                
                DebugLog.StartupDebug("ClientUIManager initialized");
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Failed to initialize ClientUIManager: {ex}");
            }
        }

        /// <summary>
        /// Handle scene loading events
        /// </summary>
        internal void OnSceneLoaded(string sceneName)
        {
            try
            {
                if (sceneName == "Menu")
                {
                    connectionManager?.RestoreMenuCursorState();
                    StartDeferredMenuCursorRestore();
                    RefreshMenuUiReferences();
                    menuAnimationController?.Reset();

                    if (!menuUISetup || !HasLiveMenuUi())
                    {
                        DebugLog.StartupDebug("Menu scene loaded - setting up prototype UI");
                        MelonCoroutines.Start(SetupMenuUI());
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error handling scene load ({sceneName}): {ex}");
            }
        }

        private void StartDeferredMenuCursorRestore()
        {
            if (pendingMenuCursorRestoreCoroutine != null)
            {
                MelonCoroutines.Stop(pendingMenuCursorRestoreCoroutine);
                pendingMenuCursorRestoreCoroutine = null;
            }

            pendingMenuCursorRestoreCoroutine = MelonCoroutines.Start(DeferredMenuCursorRestore());
        }

        private IEnumerator DeferredMenuCursorRestore()
        {
            yield return null;
            yield return null;

            // Re-apply the menu cursor state for a short window because
            // scene/input initialization can re-lock the cursor after disconnect.
            for (int i = 0; i < 8; i++)
            {
                if (SceneManager.GetActiveScene().name != "Menu")
                {
                    break;
                }

                connectionManager?.RestoreMenuCursorState();
                yield return new WaitForSeconds(0.1f);
            }

            pendingMenuCursorRestoreCoroutine = null;
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
                RefreshMenuUiReferences();

                if (AddServersButton())
                {
                    menuUISetup = true;
                    DebugLog.StartupDebug("Menu UI setup completed");
                }
                else
                {
                    DebugLog.Warning("Failed to setup menu UI - will retry next time menu loads");
                    menuUISetup = false;
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error("Error setting up menu UI", ex);
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
                if (serversButton != null)
                    return true;

                // Find main menu structure
                var mainMenu = GameObject.Find("MainMenu");
                if (mainMenu == null)
                {
                    DebugLog.Warning("MainMenu not found");
                    return false;
                }

                var continueButton = FindContinueButton(mainMenu.transform);
                if (continueButton == null)
                {
                    DebugLog.Warning("Continue button not found");
                    return false;
                }

                var buttonParent = continueButton.parent;
                if (buttonParent == null)
                {
                    DebugLog.Warning("Continue button parent not found");
                    return false;
                }

                // Create servers button by cloning continue button
                serversButton = GameObject.Instantiate(continueButton.gameObject, buttonParent);
                serversButton.name = "ServersButton";
                
                // Position it to the right of the continue button so it's not blocked
                PositionServersButton(serversButton, continueButton);
                
                // Update button appearance
                UpdateServersButtonText(serversButton);
                
                // Replace original button component to strip persistent onClick listeners from the cloned Continue button
                StripPersistentOnClick(serversButton);

                // Setup button functionality
                SetupServersButtonClick(serversButton);

                DebugLog.StartupDebug("Servers button added to main menu successfully");
                return true;
            }
            catch (Exception ex)
            {
                DebugLog.Error("Error adding servers button", ex);
                return false;
            }
        }

        /// <summary>
        /// Position the Servers button above Continue.
        /// If the menu is layout-driven, sibling order is enough. Otherwise fall back to manual positioning.
        /// </summary>
        private void PositionServersButton(GameObject newButton, Transform continueButton)
        {
            if (newButton == null || continueButton == null)
            {
                return;
            }

            Transform parent = continueButton.parent;
            if (parent == null)
            {
                return;
            }

            newButton.transform.SetSiblingIndex(continueButton.GetSiblingIndex());

            LayoutGroup layoutGroup = parent.GetComponent<LayoutGroup>();
            if (layoutGroup != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(parent as RectTransform);
                return;
            }

            RectTransform continueRect = continueButton as RectTransform;
            RectTransform newRect = newButton.GetComponent<RectTransform>();
            if (continueRect == null || newRect == null)
            {
                return;
            }

            float verticalSpacing = continueRect.rect.height + 12f;
            newRect.anchoredPosition = continueRect.anchoredPosition + new Vector2(0f, verticalSpacing);
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
                // Capture TMP font/material from existing UI to reuse inside bundle panels
                var tmpUGUI = tmp as TextMeshProUGUI;
                if (tmpUGUI != null)
                {
                    capturedTmpFont = tmpUGUI.font ?? capturedTmpFont;
                    capturedTmpMaterial = tmpUGUI.fontMaterial ?? capturedTmpMaterial;
                }
                return;
            }
            var legacy = button.GetComponentInChildren<Text>();
            if (legacy != null)
            {
                legacy.text = "Servers";
                capturedLegacyFont = legacy.font ?? capturedLegacyFont;
                return;
            }
            DebugLog.Warning("Could not find text component on servers button");
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
                    DebugLog.Warning("StripPersistentOnClick: Button component not found");
                    return;
                }

                // Replace the entire UnityEvent to ensure no persistent listeners remain
                btn.onClick = new Button.ButtonClickedEvent();
            }
            catch (Exception ex)
            {
                DebugLog.Error("Error stripping onClick listeners", ex);
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
                buttonComponent.onClick.AddListener((UnityAction)OnServersButtonClicked);
            }
            else
            {
                DebugLog.Warning("Could not find Button component on servers button");
            }
        }

        /// <summary>
        /// Handle prototype button click
        /// </summary>
        private void OnServersButtonClicked()
        {
            try
            {
                ToggleServerMenu(true);
            }
            catch (Exception ex)
            {
                DebugLog.Error("Error handling servers button click", ex);
            }
        }

        /// <summary>
        /// Update button text based on connection state
        /// </summary>
        internal void UpdateButtonState()
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
                DebugLog.Error("Error updating button state", ex);
            }
        }

        /// <summary>
        /// Show connection status in a temporary UI element
        /// </summary>
        internal void ShowConnectionStatus()
        {
            try
            {
                var status = connectionManager.GetConnectionStatus();
                DebugLog.Info("=== Connection Status ===");
                DebugLog.Info(status);
                
                // In a full implementation, this could show a popup or console overlay
                // For now, just log to console
            }
            catch (Exception ex)
            {
                DebugLog.Error("Error showing connection status", ex);
            }
        }

        internal void HandleInput()
        {
            try
            {
                bool backPressed = Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1);
                if (backPressed && IsServerBrowserOpen())
                {
                    HandleBackNavigation();
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error("Error handling input", ex);
            }
        }

        private bool IsServerBrowserOpen()
        {
            return (dsServerBrowserPanel != null && dsServerBrowserPanel.gameObject.activeSelf) ||
                   (dsDirectConnectPanel != null && dsDirectConnectPanel.gameObject.activeSelf) ||
                   (dsAddServerPanel != null && dsAddServerPanel.activeSelf) ||
                   (serverMenuPanel != null && serverMenuPanel.activeSelf);
        }

        private void CloseServerBrowser()
        {
            ToggleServerMenu(false);
        }

        private void HandleBackNavigation()
        {
            if (IsSecondaryServerBrowserViewOpen())
            {
                ShowServerBrowserView(ServerBrowserView.Browser);
                return;
            }

            CloseServerBrowser();
        }

        private bool IsSecondaryServerBrowserViewOpen()
        {
            return (dsDirectConnectPanel != null && dsDirectConnectPanel.gameObject.activeSelf) ||
                   (dsAddServerPanel != null && dsAddServerPanel.activeSelf);
        }

        /// <summary>
        /// Remove UI elements when cleaning up
        /// </summary>
        internal void Cleanup()
        {
            try
            {
                serverListRepository.Changed -= OnServerListRepositoryChanged;
                connectionManager.DedicatedServerConnected -= OnDedicatedServerConnected;
                ServerDataStore.OnUpdated -= OnServerDataUpdated;

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

                if (dsServerBrowserPanel != null)
                {
                    GameObject.Destroy(dsServerBrowserPanel.gameObject);
                    dsServerBrowserPanel = null;
                }
                if (dsDirectConnectPanel != null)
                {
                    GameObject.Destroy(dsDirectConnectPanel.gameObject);
                    dsDirectConnectPanel = null;
                }
                if (dsAddServerPanel != null)
                {
                    GameObject.Destroy(dsAddServerPanel);
                    dsAddServerPanel = null;
                }
                spawnedFavoriteEntries.Clear();
                spawnedHistoryEntries.Clear();
                if (dedicatedUiBundle != null)
                {
                    try { dedicatedUiBundle.Unload(false); } catch { }
                    dedicatedUiBundle = null;
                }

                menuUISetup = false;
            }
            catch (Exception ex)
            {
                DebugLog.Error("Error during UI cleanup", ex);
            }
        }

        /// <summary>
        /// Reset UI state when returning to menu
        /// </summary>
        internal void ResetUIState()
        {
            try
            {
                RefreshMenuUiReferences();
                menuUISetup = false;
                menuAnimationController?.Reset();
                UpdateButtonState();
            }
            catch (Exception ex)
            {
                DebugLog.Error("Error resetting UI state", ex);
            }
        }

        private void ToggleServerMenu(bool show)
        {
            try
            {
                // Hide/show main menu buttons using animation controller
                menuAnimationController?.ToggleMenuVisibility(!show);

                // Prefer AssetBundle-driven UI. Fall back to runtime-built panel if bundle missing.
                if (show)
                {
                    if (EnsureDedicatedClientUi())
                    {
                        PrefillDedicatedDirectConnectFields();
                        ShowServerBrowserView(ServerBrowserView.Browser);
                        return;
                    }
                }
                else
                {
                    if (dsServerBrowserPanel != null) dsServerBrowserPanel.gameObject.SetActive(false);
                    if (dsDirectConnectPanel != null) dsDirectConnectPanel.gameObject.SetActive(false);
                    if (dsAddServerPanel != null) dsAddServerPanel.SetActive(false);
                    return;
                }

                // Fallback path (legacy runtime-created panel)
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
                DebugLog.Error("Error toggling server menu", ex);
            }
        }

        private void RefreshMenuUiReferences()
        {
            if (serversButton != null && serversButton.Equals(null))
            {
                serversButton = null;
            }

            if (serverMenuOverlay != null && serverMenuOverlay.Equals(null))
            {
                serverMenuOverlay = null;
            }

            if (serverMenuPanel != null && serverMenuPanel.Equals(null))
            {
                serverMenuPanel = null;
                serverMenuCanvasGroup = null;
                serverAddressInput = null;
                serverMenuStatusText = null;
                serverMenuConnectButton = null;
                serverMenuListButton = null;
                serverMenuCloseButton = null;
            }

            if (dsServerBrowserPanel != null && dsServerBrowserPanel.Equals(null))
            {
                dsServerBrowserPanel = null;
                dsAddServerButton = null;
                dsRefreshButton = null;
                dsFavoritesButton = null;
                dsHistoryButton = null;
                dsFavoritesListPanel = null;
                dsHistoryListPanel = null;
                dsFavoritesContent = null;
                dsHistoryContent = null;
                dsFavoritesEntryTemplate = null;
                dsHistoryEntryTemplate = null;
                dsFavoritesEmptyPlaceholder = null;
                dsHistoryEmptyPlaceholder = null;
                dsOpenDirectConnectButton = null;
            }

            if (dsDirectConnectPanel != null && dsDirectConnectPanel.Equals(null))
            {
                dsDirectConnectPanel = null;
                dsConnectButton = null;
                dsCancelButton = null;
                dsIpInput = null;
                dsPortInput = null;
                dsDirectConnectStatusText = null;
            }

            if (dsAddServerPanel != null && dsAddServerPanel.Equals(null))
            {
                dsAddServerPanel = null;
                dsAddServerTitleText = null;
                dsAddServerStatusText = null;
                dsFavoriteNameInput = null;
                dsFavoriteIpInput = null;
                dsFavoritePortInput = null;
                dsFavoriteSaveButton = null;
                dsFavoriteCancelButton = null;
                editingFavoriteId = null;
            }
        }

        internal void Update()
        {
            try
            {
                SampleGameplayPing();
            }
            catch (Exception ex)
            {
                DebugLog.Error("Error updating client server browser metrics", ex);
            }
        }

        private bool HasLiveMenuUi()
        {
            return serversButton != null || dsServerBrowserPanel != null || dsAddServerPanel != null || serverMenuPanel != null;
        }

        private bool EnsureDedicatedClientUi()
        {
            try
            {
                // Only create once
                if (dsServerBrowserPanel != null && dsDirectConnectPanel != null && dsAddServerPanel != null)
                {
                    ShowServerBrowserView(ServerBrowserView.Browser);
                    return true;
                }

                // Load embedded AssetBundle containing the prefab
                if (dedicatedUiBundle == null)
                {
                    dedicatedUiBundle = AssetBundleLoader.LoadEmbeddedBundle(
                        "DedicatedServerMod.Assets.dedicatedclientui",
                        msg => DebugLog.Error(msg),
                        msg => DebugLog.Info(msg));
                }

                if (dedicatedUiBundle == null)
                {
                    DebugLog.Warning("Dedicated client UI bundle could not be loaded; using runtime UI");
                    return false;
                }

                // Load both panels as separate prefabs to avoid nesting a Canvas in a Canvas
                var serverBrowserPrefab = AssetBundleLoader.LoadAsset<GameObject>(dedicatedUiBundle, "ServerBrowserPanel (GameUI)", msg => DebugLog.Error(msg))
                    ?? AssetBundleLoader.LoadAsset<GameObject>(dedicatedUiBundle, "ServerBrowserPanel", msg => DebugLog.Error(msg));
                var directConnectPrefab = AssetBundleLoader.LoadAsset<GameObject>(dedicatedUiBundle, "DirectConnectPanel", msg => DebugLog.Error(msg));

                if (serverBrowserPrefab == null || directConnectPrefab == null)
                {
                    DebugLog.Warning("Required UI prefabs not found in bundle; expected 'ServerBrowserPanel (GameUI)' and 'DirectConnectPanel'");
                    return false;
                }

                var mainMenu = GameObject.Find("MainMenu");
                Transform parent = mainMenu != null ? mainMenu.transform : null;

                var serverBrowserGO = parent != null ? GameObject.Instantiate(serverBrowserPrefab, parent) : GameObject.Instantiate(serverBrowserPrefab);
                var directConnectGO = parent != null ? GameObject.Instantiate(directConnectPrefab, parent) : GameObject.Instantiate(directConnectPrefab);

                dsServerBrowserPanel = serverBrowserGO.transform;
                dsDirectConnectPanel = directConnectGO.transform;

                dsServerBrowserPanel.gameObject.name = "ServerBrowserPanel_Instance";
                dsDirectConnectPanel.gameObject.name = "DirectConnectPanel_Instance";
                dsServerBrowserPanel.gameObject.SetActive(false);
                dsDirectConnectPanel.gameObject.SetActive(false);

                // Wire up buttons and inputs inside the prefabs
                dsAddServerButton = FindDeepChild(dsServerBrowserPanel, "AddServerButton")?.GetComponent<Button>();
                dsOpenDirectConnectButton = FindDeepChild(dsServerBrowserPanel, "DirectConnectButton")?.GetComponent<Button>();
                dsRefreshButton = FindDeepChild(dsServerBrowserPanel, "RefreshButton")?.GetComponent<Button>();
                dsFavoritesButton = FindDeepChild(dsServerBrowserPanel, "FavoritesButton")?.GetComponent<Button>();
                dsHistoryButton = FindDeepChild(dsServerBrowserPanel, "HistoryButton")?.GetComponent<Button>();
                dsFavoritesListPanel = FindDeepChild(dsServerBrowserPanel, "FavoritesListPanel");
                dsHistoryListPanel = FindDeepChild(dsServerBrowserPanel, "HistoryListPanel");
                dsFavoritesContent = FindDeepChild(dsServerBrowserPanel, "FavoritesListPanel") != null ? FindDeepChild(FindDeepChild(dsServerBrowserPanel, "FavoritesListPanel"), "Content") : null;
                dsHistoryContent = FindDeepChild(dsServerBrowserPanel, "HistoryListPanel") != null ? FindDeepChild(FindDeepChild(dsServerBrowserPanel, "HistoryListPanel"), "Content") : null;
                dsFavoritesEmptyPlaceholder = FindDeepChild(dsServerBrowserPanel, "FavoritesListPanel") != null ? FindDeepChild(FindDeepChild(dsServerBrowserPanel, "FavoritesListPanel"), "EmptyPlaceholder")?.GetComponent<TMP_Text>() : null;
                dsHistoryEmptyPlaceholder = FindDeepChild(dsServerBrowserPanel, "HistoryListPanel") != null ? FindDeepChild(FindDeepChild(dsServerBrowserPanel, "HistoryListPanel"), "EmptyPlaceholder")?.GetComponent<TMP_Text>() : null;
                dsFavoritesEntryTemplate = FindDeepChild(dsServerBrowserPanel, "FavoritesListPanel") != null ? FindDeepChild(FindDeepChild(dsServerBrowserPanel, "FavoritesListPanel"), "ServerEntryPrefab")?.gameObject : null;
                dsHistoryEntryTemplate = FindDeepChild(dsServerBrowserPanel, "HistoryListPanel") != null ? FindDeepChild(FindDeepChild(dsServerBrowserPanel, "HistoryListPanel"), "ServerEntryPrefab")?.gameObject : null;
                dsConnectButton = FindDeepChild(dsDirectConnectPanel, "ConnectButton")?.GetComponent<Button>();
                dsCancelButton = FindDeepChild(dsDirectConnectPanel, "CancelButton")?.GetComponent<Button>();

                var ipTransform = FindDeepChild(dsDirectConnectPanel, "IP");
                var portTransform = FindDeepChild(dsDirectConnectPanel, "Port");
                var directConnectContent = FindDeepChild(dsDirectConnectPanel, "ServerListPanel");
                dsIpInput = ipTransform != null ? ipTransform.GetComponent<TMP_InputField>() : null;
                dsPortInput = portTransform != null ? portTransform.GetComponent<TMP_InputField>() : null;

                // Fallback to legacy InputField if TMP not used
                if (dsIpInput == null && ipTransform != null)
                {
                    var legacy = ipTransform.GetComponent<InputField>();
                    if (legacy != null)
                    {
                        dsIpInput = ipTransform.gameObject.AddComponent<TMP_InputField>();
                        var text = ipTransform.GetComponent<TextMeshProUGUI>() ?? ipTransform.gameObject.AddComponent<TextMeshProUGUI>();
                        dsIpInput.textComponent = text;
                        dsIpInput.text = legacy.text;
                    }
                }
                if (dsPortInput == null && portTransform != null)
                {
                    var legacy = portTransform.GetComponent<InputField>();
                    if (legacy != null)
                    {
                        dsPortInput = portTransform.gameObject.AddComponent<TMP_InputField>();
                        var text = portTransform.GetComponent<TextMeshProUGUI>() ?? portTransform.gameObject.AddComponent<TextMeshProUGUI>();
                        dsPortInput.textComponent = text;
                        dsPortInput.text = legacy.text;
                    }
                }

                // Hook up behavior
                if (dsAddServerButton != null)
                {
                    dsAddServerButton.onClick.RemoveAllListeners();
                    dsAddServerButton.onClick.AddListener((UnityAction)OpenAddServerPanel);
                }
                if (dsOpenDirectConnectButton != null)
                {
                    dsOpenDirectConnectButton.onClick.RemoveAllListeners();
                    dsOpenDirectConnectButton.onClick.AddListener((UnityAction)delegate { PrefillDedicatedDirectConnectFields(); ShowServerBrowserView(ServerBrowserView.DirectConnect); });
                }
                if (dsRefreshButton != null)
                {
                    dsRefreshButton.onClick.RemoveAllListeners();
                    dsRefreshButton.onClick.AddListener((UnityAction)RefreshAllServerMetadata);
                }
                if (dsFavoritesButton != null)
                {
                    dsFavoritesButton.onClick.RemoveAllListeners();
                    dsFavoritesButton.onClick.AddListener((UnityAction)delegate { SetActiveTab(ServerBrowserTab.Favorites); });
                }
                if (dsHistoryButton != null)
                {
                    dsHistoryButton.onClick.RemoveAllListeners();
                    dsHistoryButton.onClick.AddListener((UnityAction)delegate { SetActiveTab(ServerBrowserTab.History); });
                }
                if (dsCancelButton != null)
                {
                    dsCancelButton.onClick.RemoveAllListeners();
                    dsCancelButton.onClick.AddListener((UnityAction)delegate { ShowServerBrowserView(ServerBrowserView.Browser); });
                }
                if (dsConnectButton != null)
                {
                    dsConnectButton.onClick.RemoveAllListeners();
                    dsConnectButton.onClick.AddListener((UnityAction)OnDirectConnectConfirm);
                }

                if (directConnectContent != null && dsDirectConnectStatusText == null)
                {
                    dsDirectConnectStatusText = CreatePanelText(directConnectContent, "DirectConnectStatusText", new Vector2(0f, -314f), new Vector2(420f, 24f), 15, FontStyles.Normal, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.78f));
                    dsDirectConnectStatusText.text = string.Empty;
                }

                EnsureAddServerPanel();

                // Apply captured fonts/colors so text is visible in game
                ApplyCapturedFonts(dsServerBrowserPanel);
                ApplyCapturedFonts(dsDirectConnectPanel);
                ApplyCapturedFonts(dsAddServerPanel != null ? dsAddServerPanel.transform : null);

                SetActiveTab(ServerBrowserTab.Favorites);
                RefreshServerLists();
                RefreshAllServerMetadata();
                PrefillDedicatedDirectConnectFields();
                ShowServerBrowserView(ServerBrowserView.Browser);

                DebugLog.StartupDebug("Dedicated client UI loaded and initialized from AssetBundle");
                return true;
            }
            catch (Exception ex)
            {
                DebugLog.Error("Error loading dedicated client UI", ex);
                return false;
            }
        }

        private void ShowDirectConnectPanel(bool show)
        {
            try
            {
                ShowServerBrowserView(show ? ServerBrowserView.DirectConnect : ServerBrowserView.Browser);
            }
            catch (Exception ex)
            {
                DebugLog.Error("Error toggling DirectConnect panel", ex);
            }
        }

        private void ShowServerBrowserView(ServerBrowserView view)
        {
            if (dsServerBrowserPanel != null)
            {
                dsServerBrowserPanel.gameObject.SetActive(view == ServerBrowserView.Browser);
            }

            if (dsDirectConnectPanel != null)
            {
                dsDirectConnectPanel.gameObject.SetActive(view == ServerBrowserView.DirectConnect);
            }

            if (dsAddServerPanel != null)
            {
                dsAddServerPanel.SetActive(view == ServerBrowserView.AddFavorite);
            }
        }

        private void EnsureAddServerPanel()
        {
            if (dsAddServerPanel != null || dsServerBrowserPanel == null)
            {
                return;
            }

            Transform parent = dsServerBrowserPanel.parent;
            if (parent == null)
            {
                return;
            }

            dsAddServerPanel = CreateUiObject("AddServerPanel", parent);
            EnsureComponent<CanvasRenderer>(dsAddServerPanel);
            EnsureComponent<Image>(dsAddServerPanel);
            EnsureComponent<Outline>(dsAddServerPanel);
            var panelRect = dsAddServerPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(620f, 420f);
            panelRect.anchoredPosition = Vector2.zero;

            var panelImage = dsAddServerPanel.GetComponent<Image>();
            panelImage.color = PANEL_BG;

            var panelOutline = dsAddServerPanel.GetComponent<Outline>();
            panelOutline.effectColor = new Color(1f, 1f, 1f, 0.1f);
            panelOutline.effectDistance = new Vector2(1f, -1f);

            dsAddServerTitleText = CreatePanelText(dsAddServerPanel.transform, "Title", new Vector2(0f, -24f), new Vector2(520f, 34f), 28, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);

            var helperText = CreatePanelText(dsAddServerPanel.transform, "HelperText", new Vector2(0f, -62f), new Vector2(520f, 26f), 16, FontStyles.Normal, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.72f));
            helperText.text = "Keep favorite servers handy, then join them from the browser at any time.";

            dsFavoriteNameInput = CreateLabeledInput(dsAddServerPanel.transform, "Server Name", "NameInput", new Vector2(38f, -116f), "Neighborhood Host");
            dsFavoriteIpInput = CreateLabeledInput(dsAddServerPanel.transform, "IP / Host", "HostInput", new Vector2(38f, -196f), "127.0.0.1 or play.example.com");
            dsFavoritePortInput = CreateLabeledInput(dsAddServerPanel.transform, "Port", "PortInput", new Vector2(38f, -276f), "38465");

            dsFavoriteSaveButton = CreateStyledButton(dsAddServerPanel.transform, new Vector2(38f, -350f), new Vector2(198f, 42f), "Save Server");
            dsFavoriteCancelButton = CreateStyledButton(dsAddServerPanel.transform, new Vector2(252f, -350f), new Vector2(144f, 42f), "Cancel");
            dsFavoriteSaveButton.onClick.AddListener((UnityAction)OnSaveFavoriteConfirmed);
            dsFavoriteCancelButton.onClick.AddListener((UnityAction)delegate { ShowServerBrowserView(ServerBrowserView.Browser); });

            var closeButton = CreateIconButton(dsAddServerPanel.transform, new Vector2(580f, -18f), new Vector2(28f, 28f), "X");
            var closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-16f, -16f);
            closeButton.onClick.AddListener((UnityAction)delegate { ShowServerBrowserView(ServerBrowserView.Browser); });

            dsAddServerStatusText = CreatePanelText(dsAddServerPanel.transform, "StatusText", new Vector2(0f, -384f), new Vector2(520f, 38f), 15, FontStyles.Normal, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.8f));
            dsAddServerStatusText.text = string.Empty;

            ApplyCapturedFonts(dsAddServerPanel.transform);
            dsAddServerPanel.SetActive(false);
        }

        private TMP_InputField CreateLabeledInput(Transform parent, string label, string name, Vector2 anchoredPosition, string placeholderText)
        {
            CreatePanelText(parent, $"{name}_Label", anchoredPosition + new Vector2(0f, 22f), new Vector2(220f, 24f), 16, FontStyles.Bold, TextAlignmentOptions.Left, new Color(1f, 1f, 1f, 0.9f), leftAnchored: true).text = label;
            return CreateInputField(parent, name, anchoredPosition, new Vector2(544f, 42f), placeholderText, false);
        }

        private TMP_InputField CreateLabeledMultilineInput(Transform parent, string label, string name, Vector2 anchoredPosition, string placeholderText, float height)
        {
            CreatePanelText(parent, $"{name}_Label", anchoredPosition + new Vector2(0f, 22f), new Vector2(220f, 24f), 16, FontStyles.Bold, TextAlignmentOptions.Left, new Color(1f, 1f, 1f, 0.9f), leftAnchored: true).text = label;
            return CreateInputField(parent, name, anchoredPosition, new Vector2(544f, height), placeholderText, true);
        }

        private TMP_InputField CreateInputField(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, string placeholderText, bool multiline)
        {
            GameObject background = CreateUiObject(name, parent);
            EnsureComponent<CanvasRenderer>(background);
            EnsureComponent<Image>(background);
            var backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 1f);
            backgroundRect.anchorMax = new Vector2(0f, 1f);
            backgroundRect.pivot = new Vector2(0f, 1f);
            backgroundRect.anchoredPosition = anchoredPosition;
            backgroundRect.sizeDelta = size;

            var backgroundImage = background.GetComponent<Image>();
            backgroundImage.color = INPUT_BG;

            GameObject textArea = CreateUiObject($"{name}_TextArea", background.transform);
            var textAreaRect = textArea.GetComponent<RectTransform>();
            textAreaRect.anchorMin = new Vector2(0f, 0f);
            textAreaRect.anchorMax = new Vector2(1f, 1f);
            textAreaRect.offsetMin = new Vector2(12f, 8f);
            textAreaRect.offsetMax = new Vector2(-12f, -8f);

            GameObject placeholderObject = CreateUiObject($"{name}_Placeholder", textArea.transform);
            var placeholderRect = placeholderObject.GetComponent<RectTransform>();
            placeholderRect.anchorMin = new Vector2(0f, 0f);
            placeholderRect.anchorMax = new Vector2(1f, 1f);
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;
            var placeholder = placeholderObject.AddComponent<TextMeshProUGUI>();
            placeholder.text = placeholderText;
            placeholder.fontSize = 17;
            placeholder.color = new Color(1f, 1f, 1f, 0.32f);
            placeholder.alignment = multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft;

            GameObject textObject = CreateUiObject($"{name}_Text", textArea.transform);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = 17;
            text.color = Color.white;
            text.alignment = multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = multiline ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;

            var inputField = background.AddComponent<TMP_InputField>();
            inputField.textViewport = textAreaRect;
            inputField.placeholder = placeholder;
            inputField.textComponent = text;
            inputField.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;

            return inputField;
        }

        private TMP_Text CreatePanelText(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment, Color color, bool leftAnchored = false)
        {
            GameObject textObject = CreateUiObject(name, parent);
            var textRect = textObject.GetComponent<RectTransform>();
            if (leftAnchored)
            {
                textRect.anchorMin = new Vector2(0f, 1f);
                textRect.anchorMax = new Vector2(0f, 1f);
                textRect.pivot = new Vector2(0f, 1f);
            }
            else
            {
                textRect.anchorMin = new Vector2(0.5f, 1f);
                textRect.anchorMax = new Vector2(0.5f, 1f);
                textRect.pivot = new Vector2(0.5f, 1f);
            }

            textRect.anchoredPosition = anchoredPosition;
            textRect.sizeDelta = size;

            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = color;
            return text;
        }

        private void OpenAddServerPanel()
        {
            OpenAddServerPanel(null);
        }

        private void OpenAddServerPanel(SavedServerEntry favorite)
        {
            EnsureAddServerPanel();
            if (dsAddServerPanel == null)
            {
                return;
            }

            editingFavoriteId = favorite?.Id;
            if (dsAddServerTitleText != null)
            {
                dsAddServerTitleText.text = favorite == null ? "Add Favorite Server" : "Edit Favorite Server";
            }

            if (dsFavoriteNameInput != null) dsFavoriteNameInput.text = favorite?.Name ?? string.Empty;
            if (dsFavoriteIpInput != null) dsFavoriteIpInput.text = favorite?.Host ?? string.Empty;
            if (dsFavoritePortInput != null) dsFavoritePortInput.text = favorite != null ? favorite.Port.ToString() : ClientConnectionManager.GetTargetServer().port.ToString();
            if (dsAddServerStatusText != null) dsAddServerStatusText.text = string.Empty;

            ShowServerBrowserView(ServerBrowserView.AddFavorite);
        }

        private void OnSaveFavoriteConfirmed()
        {
            try
            {
                string name = dsFavoriteNameInput != null ? dsFavoriteNameInput.text : string.Empty;
                string host = dsFavoriteIpInput != null ? dsFavoriteIpInput.text : string.Empty;
                string portText = dsFavoritePortInput != null ? dsFavoritePortInput.text : string.Empty;
                if (string.IsNullOrWhiteSpace(host))
                {
                    SetAddServerStatus("IP / host is required.");
                    return;
                }

                if (!int.TryParse(portText, out int port) || port <= 0 || port > 65535)
                {
                    SetAddServerStatus("Enter a valid port between 1 and 65535.");
                    return;
                }

                serverListRepository.SaveFavorite(editingFavoriteId, name, host, port);
                SetActiveTab(ServerBrowserTab.Favorites);
                ShowServerBrowserView(ServerBrowserView.Browser);
                SetAddServerStatus("Saved. Querying server status...");
                RefreshServerMetadata(host, port, true);
            }
            catch (Exception ex)
            {
                DebugLog.Error("Error saving favorite server", ex);
                SetAddServerStatus("Unable to save this server right now.");
            }
        }

        private void SetAddServerStatus(string message)
        {
            if (dsAddServerStatusText != null)
            {
                dsAddServerStatusText.text = message ?? string.Empty;
            }
        }

        private void SetActiveTab(ServerBrowserTab tab)
        {
            activeTab = tab;

            if (dsFavoritesListPanel != null)
            {
                dsFavoritesListPanel.gameObject.SetActive(tab == ServerBrowserTab.Favorites);
            }

            if (dsHistoryListPanel != null)
            {
                dsHistoryListPanel.gameObject.SetActive(tab == ServerBrowserTab.History);
            }

            ApplyTabState(dsFavoritesButton, tab == ServerBrowserTab.Favorites);
            ApplyTabState(dsHistoryButton, tab == ServerBrowserTab.History);
        }

        private void ApplyTabState(Button button, bool active)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = !active;
            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = active ? ACCENT : BTN_BG;
            }
        }

        private void RefreshServerLists()
        {
            RenderServerEntries(serverListRepository.Favorites, dsFavoritesContent, dsFavoritesEntryTemplate, dsFavoritesEmptyPlaceholder, spawnedFavoriteEntries, true);
            RenderServerEntries(serverListRepository.History, dsHistoryContent, dsHistoryEntryTemplate, dsHistoryEmptyPlaceholder, spawnedHistoryEntries, false);
        }

        private void RefreshAllServerMetadata()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in serverListRepository.Favorites)
            {
                string key = BuildEndpointKey(entry.Host, entry.Port);
                if (seen.Add(key))
                {
                    RefreshServerMetadata(entry.Host, entry.Port, false);
                }
            }

            foreach (var entry in serverListRepository.History)
            {
                string key = BuildEndpointKey(entry.Host, entry.Port);
                if (seen.Add(key))
                {
                    RefreshServerMetadata(entry.Host, entry.Port, false);
                }
            }
        }

        private void RefreshServerMetadata(string host, int port, bool updateAddServerStatus)
        {
            string key = BuildEndpointKey(host, port);
            if (!statusQueriesInFlight.Add(key))
            {
                return;
            }

            MelonCoroutines.Start(QueryServerMetadataCoroutine(host, port, key, updateAddServerStatus));
        }

        private IEnumerator QueryServerMetadataCoroutine(string host, int port, string key, bool updateAddServerStatus)
        {
            var task = serverStatusQueryService.QueryAsync(host, port);
            while (!task.IsCompleted)
            {
                yield return null;
            }

            statusQueriesInFlight.Remove(key);

            if (task.IsFaulted || task.IsCanceled)
            {
                // DebugLog.Warning($"Status query failed for {host}:{port}: {task.Exception?.GetBaseException().Message}");
                serverListRepository.MarkStatusQueryUnavailable(host, port);
                if (updateAddServerStatus)
                {
                    SetAddServerStatus("Saved, but the server did not answer its status query yet.");
                }
                yield break;
            }

            ServerStatusQueryResult result = task.Result;
            serverListRepository.UpdateStatusQueryMetadata(
                host,
                port,
                result.Snapshot.ServerName,
                result.Snapshot.ServerDescription,
                result.Snapshot.CurrentPlayers,
                result.Snapshot.MaxPlayers,
                result.StatusQueryMilliseconds);

            if (updateAddServerStatus)
            {
                SetAddServerStatus($"Saved. {result.Snapshot.CurrentPlayers}/{result.Snapshot.MaxPlayers} players, {result.StatusQueryMilliseconds}ms ping.");
            }
        }

        private void RenderServerEntries(IReadOnlyList<SavedServerEntry> entries, Transform contentRoot, GameObject template, TMP_Text emptyPlaceholder, List<GameObject> spawnedEntries, bool favorites)
        {
            if (contentRoot == null || template == null)
            {
                return;
            }

            for (int i = 0; i < spawnedEntries.Count; i++)
            {
                if (spawnedEntries[i] != null)
                {
                    GameObject.Destroy(spawnedEntries[i]);
                }
            }
            spawnedEntries.Clear();

            bool hasEntries = entries != null && entries.Count > 0;
            if (emptyPlaceholder != null)
            {
                emptyPlaceholder.gameObject.SetActive(!hasEntries);
            }

            if (!hasEntries)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                SavedServerEntry entry = entries[i];
                GameObject row = GameObject.Instantiate(template, contentRoot);
                row.name = $"{(favorites ? "Favorite" : "History")}_{entry.Id}";
                row.SetActive(true);
                BindServerEntry(row.transform, entry, favorites);
                spawnedEntries.Add(row);
            }
        }

        private void BindServerEntry(Transform entryRoot, SavedServerEntry entry, bool favorite)
        {
            string primaryName = !string.IsNullOrWhiteSpace(entry.Name)
                ? entry.Name
                : !string.IsNullOrWhiteSpace(entry.ServerName)
                    ? entry.ServerName
                    : $"{entry.Host}:{entry.Port}";
            bool isCurrentConnection = IsCurrentConnection(entry);
            bool hasResponsiveQuery = entry.StatusQueryMilliseconds >= 0;
            string description = BuildDescriptionText(entry, favorite);
            string pingText = BuildLatencyText(entry, isCurrentConnection);
            string playerCountText = BuildPlayerCountText(entry, isCurrentConnection, hasResponsiveQuery);

            SetText(entryRoot, "ServerName", primaryName);
            SetText(entryRoot, "ServerIP", $"{entry.Host}:{entry.Port}");
            SetText(entryRoot, "ServerDescription", description);
            SetText(entryRoot, "Ping", pingText);
            SetText(entryRoot, "PlayerCount", playerCountText);
            ApplyPingState(entryRoot, isCurrentConnection || hasResponsiveQuery);

            Button joinButton = FindDeepChild(entryRoot, "JoinServerButton")?.GetComponent<Button>();
            if (joinButton != null)
            {
                joinButton.onClick.RemoveAllListeners();
                joinButton.onClick.AddListener((UnityAction)delegate { JoinSavedServer(entry); });
            }

            Button deleteButton = FindDeepChild(entryRoot, "DeleteButton")?.GetComponent<Button>();
            if (deleteButton != null)
            {
                deleteButton.onClick.RemoveAllListeners();
                deleteButton.onClick.AddListener((UnityAction)delegate
                {
                    if (favorite)
                    {
                        serverListRepository.RemoveFavorite(entry.Id);
                    }
                    else
                    {
                        serverListRepository.RemoveHistory(entry.Id);
                    }
                });
            }

            Button editButton = FindDeepChild(entryRoot, "EditButton")?.GetComponent<Button>();
            if (editButton != null)
            {
                editButton.gameObject.SetActive(favorite);
                editButton.onClick.RemoveAllListeners();
                if (favorite)
                {
                    editButton.onClick.AddListener((UnityAction)delegate { OpenAddServerPanel(serverListRepository.GetFavoriteById(entry.Id) ?? entry); });
                }
            }
        }

        private static string BuildDescriptionText(SavedServerEntry entry, bool favorite)
        {
            string sourceName = NormalizeUiText(entry.ServerName);
            string sourceDescription = NormalizeUiText(entry.ServerDescription);

            if (!string.IsNullOrWhiteSpace(sourceName) && !string.IsNullOrWhiteSpace(sourceDescription))
            {
                return $"{sourceName} - {sourceDescription}";
            }

            if (!string.IsNullOrWhiteSpace(sourceDescription))
            {
                return sourceDescription;
            }

            if (!string.IsNullOrWhiteSpace(sourceName) && !string.Equals(entry.Name, entry.ServerName, StringComparison.Ordinal))
            {
                return sourceName;
            }

            return favorite ? "Saved favorite server" : $"Joined {entry.LastJoinedUtc.ToLocalTime():MMM d, HH:mm}";
        }

        private string BuildLatencyText(SavedServerEntry entry, bool isCurrentConnection)
        {
            if (entry == null)
            {
                return "N/A";
            }

            if (isCurrentConnection && entry.GameplayPingMilliseconds >= 0)
            {
                return $"{entry.GameplayPingMilliseconds}ms";
            }

            if (entry.StatusQueryMilliseconds >= 0)
            {
                return $"{entry.StatusQueryMilliseconds}ms";
            }

            return "N/A";
        }

        private static string BuildPlayerCountText(SavedServerEntry entry, bool isCurrentConnection, bool hasResponsiveQuery)
        {
            if (entry == null)
            {
                return "-/-";
            }

            if (entry.MaxPlayers <= 0)
            {
                return "-/-";
            }

            if (isCurrentConnection || hasResponsiveQuery)
            {
                return $"{entry.CurrentPlayers}/{entry.MaxPlayers}";
            }

            return "-/-";
        }

        private static string NormalizeUiText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string BuildEndpointKey(string host, int port)
        {
            return $"{(host ?? string.Empty).Trim().ToLowerInvariant()}:{port}";
        }

        private bool IsCurrentConnection(SavedServerEntry entry)
        {
            if (entry == null || !connectionManager.IsConnectedToDedicatedServer)
            {
                return false;
            }

            var target = ClientConnectionManager.GetTargetServer();
            return string.Equals(BuildEndpointKey(entry.Host, entry.Port), BuildEndpointKey(target.ip, target.port), StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyPingState(Transform entryRoot, bool isOnline)
        {
            TMP_Text pingText = FindDeepChild(entryRoot, "Ping")?.GetComponent<TMP_Text>();
            if (pingText != null)
            {
                pingText.color = isOnline ? new Color(0.82f, 0.94f, 1f, 1f) : new Color(1f, 0.46f, 0.46f, 1f);
            }

            Image pingIcon = FindDeepChild(entryRoot, "Ping") != null ? FindDeepChild(FindDeepChild(entryRoot, "Ping"), "Icon")?.GetComponent<Image>() : null;
            if (pingIcon != null)
            {
                pingIcon.color = isOnline ? Color.white : new Color(1f, 0.25f, 0.25f, 1f);
            }
        }

        private void SetText(Transform root, string childName, string value)
        {
            TMP_Text text = FindDeepChild(root, childName)?.GetComponent<TMP_Text>();
            if (text != null)
            {
                text.text = value ?? string.Empty;
            }
        }

        private void JoinSavedServer(SavedServerEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            try
            {
                pendingHistoryName = entry.Name;
                connectionManager.SetTargetServer(entry.Host, entry.Port);
                connectionManager.StartDedicatedConnection();
            }
            catch (Exception ex)
            {
                DebugLog.Error("Error joining saved server", ex);
            }
        }

        private void OnDedicatedServerConnected(string host, int port)
        {
            serverListRepository.RecordJoinedServer(host, port, pendingHistoryName);
            serverListRepository.UpdateServerMetadata(
                host,
                port,
                ServerDataStore.Current?.ServerName,
                ServerDataStore.Current?.ServerDescription,
                ServerDataStore.Current?.CurrentPlayers ?? 0,
                ServerDataStore.Current?.MaxPlayers ?? 0);
            UpdateGameplayPingForCurrentServer();
            pendingHistoryName = null;
        }

        private void OnServerDataUpdated(Shared.ServerData data)
        {
            var target = ClientConnectionManager.GetTargetServer();
            serverListRepository.UpdateServerMetadata(
                target.ip,
                target.port,
                data?.ServerName,
                data?.ServerDescription,
                data?.CurrentPlayers ?? 0,
                data?.MaxPlayers ?? 0);
            UpdateGameplayPingForCurrentServer();
        }

        private void OnServerListRepositoryChanged()
        {
            RefreshServerLists();
        }

        private void SampleGameplayPing()
        {
            if (!connectionManager.IsConnectedToDedicatedServer)
            {
                gameplayPingSampleTimer = 0f;
                return;
            }

            gameplayPingSampleTimer -= Time.unscaledDeltaTime;
            if (gameplayPingSampleTimer > 0f)
            {
                return;
            }

            gameplayPingSampleTimer = 1f;
            UpdateGameplayPingForCurrentServer();
        }

        private void UpdateGameplayPingForCurrentServer()
        {
            if (!connectionManager.IsConnectedToDedicatedServer)
            {
                return;
            }

            int gameplayPingMilliseconds = GetCurrentGameplayPingMilliseconds();
            if (gameplayPingMilliseconds < 0)
            {
                return;
            }

            var target = ClientConnectionManager.GetTargetServer();
            serverListRepository.UpdateGameplayPing(target.ip, target.port, gameplayPingMilliseconds);
        }

        private static int GetCurrentGameplayPingMilliseconds()
        {
            try
            {
                var timeManager = InstanceFinder.TimeManager;
                if (timeManager == null)
                {
                    return -1;
                }

                return (int)Math.Min(timeManager.RoundTripTime, 9999L);
            }
            catch
            {
                return -1;
            }
        }

        private void ApplyCapturedFonts(Transform root)
        {
            try
            {
                if (root == null) return;

                // Apply to TMP texts
                var tmpTexts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
                for (int i = 0; i < tmpTexts.Length; i++)
                {
                    var t = tmpTexts[i];
                    if (capturedTmpFont != null) t.font = capturedTmpFont;
                    if (capturedTmpMaterial != null) t.fontMaterial = capturedTmpMaterial;
                }

                // Apply to legacy Text components
                var legacyTexts = root.GetComponentsInChildren<Text>(true);
                for (int i = 0; i < legacyTexts.Length; i++)
                {
                    var t = legacyTexts[i];
                    if (capturedLegacyFont != null) t.font = capturedLegacyFont;
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error("Error applying captured fonts", ex);
            }
        }

        private void PrefillDedicatedDirectConnectFields()
        {
            try
            {
                var target = ClientConnectionManager.GetTargetServer();
                
                if (dsIpInput != null)
                {
                    dsIpInput.text = string.Empty; // Clear first to prevent concatenation
                    dsIpInput.text = target.ip ?? "localhost";
                }
                
                if (dsPortInput != null)
                {
                    dsPortInput.text = string.Empty; // Clear first to prevent concatenation
                    dsPortInput.text = target.port.ToString();
                }

                if (dsDirectConnectStatusText != null)
                {
                    dsDirectConnectStatusText.text = string.Empty;
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error("Error pre-filling dedicated UI fields", ex);
            }
        }

        private void OnDirectConnectConfirm()
        {
            try
            {
                string ip = dsIpInput != null ? dsIpInput.text : string.Empty;
                string portText = dsPortInput != null ? dsPortInput.text : string.Empty;
                if (string.IsNullOrWhiteSpace(ip))
                {
                    SetStatusText("IP address is required.");
                    return;
                }
                if (!int.TryParse(portText, out int port) || port <= 0 || port > 65535)
                {
                    SetStatusText("Invalid port. Enter a number between 1 and 65535.");
                    return;
                }

                pendingHistoryName = null;
                connectionManager.SetTargetServer(ip.Trim(), port);
                SetStatusText($"Connecting to {ip.Trim()}:{port}...");
                connectionManager.StartDedicatedConnection();
            }
            catch (Exception ex)
            {
                DebugLog.Error("Error handling direct connect confirm", ex);
            }
        }

        private Transform FindContinueButton(Transform mainMenuRoot)
        {
            if (mainMenuRoot == null)
            {
                return null;
            }

            string[] preferredPaths =
            {
                "Home/Bank/Panel/Continue",
                "Home/Bank/Continue"
            };

            for (int i = 0; i < preferredPaths.Length; i++)
            {
                Transform candidate = mainMenuRoot.Find(preferredPaths[i]);
                if (candidate != null)
                {
                    DebugLog.StartupDebug($"Resolved Continue button at '{GetTransformPath(candidate, mainMenuRoot)}'");
                    return candidate;
                }
            }

            Transform fallback = FindDeepChild(mainMenuRoot, "Continue");
            if (fallback != null)
            {
                DebugLog.Warning($"Continue button found via fallback search at '{GetTransformPath(fallback, mainMenuRoot)}'");
            }

            return fallback;
        }

        private Transform FindDeepChild(Transform parent, string childName)
        {
            if (parent == null || string.IsNullOrEmpty(childName)) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == childName) return child;
                var result = FindDeepChild(child, childName);
                if (result != null) return result;
            }
            return null;
        }

        private string GetTransformPath(Transform target, Transform root)
        {
            if (target == null)
            {
                return string.Empty;
            }

            string path = target.name;
            Transform current = target.parent;

            while (current != null && current != root)
            {
                path = $"{current.name}/{path}";
                current = current.parent;
            }

            return current == root ? $"{root.name}/{path}" : path;
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
                DebugLog.Error("Error pre-filling server address", ex);
            }
        }

        private void CreateServerMenuUI()
        {
            // Find main menu root to attach our panel
            var mainMenu = GameObject.Find("MainMenu");
            if (mainMenu == null)
            {
                DebugLog.Warning("Cannot create server menu - MainMenu not found");
                return;
            }

            // Overlay dim
            serverMenuOverlay = CreateUiObject("ServerMenuOverlay", mainMenu.transform);
            EnsureComponent<CanvasRenderer>(serverMenuOverlay);
            EnsureComponent<Image>(serverMenuOverlay);
            serverMenuOverlay.transform.SetParent(mainMenu.transform, false);
            var overlayRect = serverMenuOverlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = new Vector2(0f, 0f);
            overlayRect.anchorMax = new Vector2(1f, 1f);
            overlayRect.pivot = new Vector2(0.5f, 0.5f);
            overlayRect.sizeDelta = Vector2.zero;
            var overlayImage = serverMenuOverlay.GetComponent<Image>();
            overlayImage.color = OVERLAY_DIM;

            // Panel
            serverMenuPanel = CreateUiObject("ServerMenuPanel", serverMenuOverlay.transform);
            EnsureComponent<CanvasRenderer>(serverMenuPanel);
            EnsureComponent<Image>(serverMenuPanel);
            EnsureComponent<CanvasGroup>(serverMenuPanel);
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
            var border = CreateUiObject("Border", serverMenuPanel.transform);
            EnsureComponent<Image>(border);
            border.transform.SetParent(serverMenuPanel.transform, false);
            var borderRect = border.GetComponent<RectTransform>();
            borderRect.anchorMin = new Vector2(0f, 0f);
            borderRect.anchorMax = new Vector2(1f, 1f);
            borderRect.offsetMin = new Vector2(2f, 2f);
            borderRect.offsetMax = new Vector2(-2f, -2f);
            var borderImage = border.GetComponent<Image>();
            borderImage.color = new Color(1f, 1f, 1f, 0.03f);

            // Title
            var titleGO = CreateUiObject("Title", serverMenuPanel.transform);
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
            var addrLabelGO = CreateUiObject("AddressLabel", serverMenuPanel.transform);
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
            var addrBG = CreateUiObject("AddressInputBG", serverMenuPanel.transform);
            EnsureComponent<Image>(addrBG);
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
            var addrBorder = CreateUiObject("AddressBorder", addrBG.transform);
            EnsureComponent<Image>(addrBorder);
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
            var addrInputGO = CreateUiObject("AddressInput", addrBG.transform);
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
            addrText.textWrappingMode = TextWrappingModes.NoWrap;
            addrText.alignment = TextAlignmentOptions.MidlineLeft;
            serverAddressInput.textComponent = addrText;
            var placeholder = CreateUiObject("Placeholder", addrInputGO.transform).AddComponent<TextMeshProUGUI>();
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
            serverMenuConnectButton.onClick.AddListener((UnityAction)OnConnectClicked);

            // Server list button
            serverMenuListButton = CreateStyledButton(serverMenuPanel.transform, new Vector2(28f, -178f), new Vector2(180f, 40f), "Server List");
            serverMenuListButton.onClick.AddListener((UnityAction)OnServerListClicked);

            // Status text
            var statusGO = CreateUiObject("StatusText", serverMenuPanel.transform);
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
            serverMenuCloseButton.onClick.AddListener((UnityAction)delegate { ToggleServerMenu(false); });

            // Hide initially
            serverMenuOverlay.SetActive(false);
            serverMenuPanel.SetActive(false);
        }

        private Button CreateStyledButton(Transform parent, Vector2 anchoredPosition, Vector2 size, string label)
        {
            var buttonGO = CreateUiObject($"Button_{label}", parent);
            EnsureComponent<CanvasRenderer>(buttonGO);
            EnsureComponent<Image>(buttonGO);
            EnsureComponent<Button>(buttonGO);
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

            var textGO = CreateUiObject("Text", buttonGO.transform);
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
                pendingHistoryName = null;
                SetStatusText($"Connecting to {ip}:{port}...");
                UpdateServerMenuState();
                connectionManager.StartDedicatedConnection();
            }
            catch (Exception ex)
            {
                DebugLog.Error("Error handling connect click", ex);
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
                DebugLog.Error("Error handling server list click", ex);
            }
        }

        private void SetStatusText(string message)
        {
            if (dsDirectConnectStatusText != null)
            {
                dsDirectConnectStatusText.text = message ?? string.Empty;
            }

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
                DebugLog.Error("Error updating server menu state", ex);
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

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name);
            gameObject.AddComponent<RectTransform>();
            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
            }

            return gameObject;
        }

        private static T EnsureComponent<T>(GameObject gameObject) where T : Component
        {
            var component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private enum ServerBrowserTab
        {
            Favorites,
            History
        }

        private enum ServerBrowserView
        {
            Browser,
            DirectConnect,
            AddFavorite
        }
    }
}
