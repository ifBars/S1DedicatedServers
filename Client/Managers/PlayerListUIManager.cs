#if CLIENT
using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
#if IL2CPP
using Il2CppFishNet;
#else
using FishNet;
#endif
using DedicatedServerMod.Shared;
using DedicatedServerMod.Shared.Networking;
using DedicatedServerMod.Utils;

namespace DedicatedServerMod.Client.Managers
{
    /// <summary>
    /// Displays an in-game player list overlay showing each connected player's name and
    /// server-measured ping while the client is on a dedicated server.
    /// </summary>
    /// <remarks>
    /// <para>The overlay is visible only while the player holds <see cref="HOLD_KEY"/> (F8).
    /// It is active only when the client is connected to a dedicated server.</para>
    /// <para>This manager also periodically sends the client's own measured RTT to the
    /// server so it can be included in the <see cref="Constants.Messages.PlayerListUpdate"/>
    /// broadcast. The measurement uses <c>InstanceFinder.TimeManager.RoundTripTime</c>
    /// (FishNet client-side metric) and falls back to <c>-1</c> when unavailable.</para>
    /// </remarks>
    public sealed class PlayerListUIManager
    {
        private const float PING_REPORT_INTERVAL = 5f;
        private const int MAX_ROWS = 64;
        private const KeyCode HOLD_KEY = KeyCode.F8;
        private const float NAME_COLUMN_WIDTH = 178f;
        private const float ROLE_COLUMN_WIDTH = 74f;
        private const float PING_COLUMN_WIDTH = 52f;

        private static readonly Color PANEL_BG = new Color(0.08f, 0.09f, 0.12f, 0.92f);
        private static readonly Color HEADER_COLOR = new Color(0.10f, 0.65f, 1.00f, 1.00f);
        private static readonly Color COLUMN_HEADER_COLOR = new Color(0.72f, 0.78f, 0.88f, 0.95f);
        private static readonly Color ROW_BG_EVEN = new Color(0.10f, 0.11f, 0.14f, 0.85f);
        private static readonly Color ROW_BG_ODD = new Color(0.12f, 0.13f, 0.17f, 0.85f);
        private static readonly Color PING_GOOD = new Color(0.20f, 0.90f, 0.30f, 1.00f); // < 70 ms
        private static readonly Color PING_OK = new Color(1.00f, 0.85f, 0.10f, 1.00f); // 70–150 ms
        private static readonly Color PING_BAD = new Color(1.00f, 0.30f, 0.20f, 1.00f); // > 150 ms
        private static readonly Color PING_UNKNOWN = new Color(0.55f, 0.55f, 0.55f, 1.00f);
        private static readonly Color ROLE_PLAYER = new Color(0.72f, 0.75f, 0.80f, 1.00f);
        private static readonly Color ROLE_ADMIN = new Color(0.98f, 0.70f, 0.18f, 1.00f);
        private static readonly Color ROLE_OPERATOR = new Color(0.30f, 0.85f, 1.00f, 1.00f);

        private readonly MelonLogger.Instance _logger;
        private readonly ClientConnectionManager _connectionManager;

        // Root UI objects
        private GameObject _canvasGo;
        private GameObject _panel;
        private Text _headerText;
        private Transform _rowContainer;

        // Row pool
        private readonly List<PlayerRow> _rows = new List<PlayerRow>();

        // Cached font
        private Font _font;

        // State
        private bool _isVisible;
        private bool _wasConnected;
        private float _pingReportTimer;
        private float _lastRefreshTime;
        private int _maxPlayers; // cached on first receive — static for the server lifetime

        public PlayerListUIManager(MelonLogger.Instance logger, ClientConnectionManager connectionManager)
        {
            _logger = logger;
            _connectionManager = connectionManager;
        }

        public void Initialize()
        {
            PlayerListStore.OnUpdated += OnPlayerListUpdated;
            _connectionManager.DedicatedServerConnected += OnConnectedToDedicatedServer;
        }

        /// <summary>
        /// Tears down the UI canvas so it can be rebuilt fresh in the new scene.
        /// </summary>
        public void OnSceneLoaded(string sceneName)
        {
            if (sceneName == "Main")
            {
                // Scene transition destroys the old canvas — reset state so EnsureUiBuilt recreates it.
                _canvasGo = null;
                _panel = null;
                _rows.Clear();
                _font = null;
                _maxPlayers = 0;
            }
        }

        /// <summary>
        /// Handles toggle input, connection state tracking, and periodic ping reporting.
        /// </summary>
        public void Update()
        {
            bool nowConnected = _connectionManager.IsConnectedToDedicatedServer;

            if (nowConnected && !_wasConnected)
            {
                EnsureUiBuilt();
                _pingReportTimer = 0f; // report immediately on connect
            }
            else if (!nowConnected && _wasConnected)
            {
                SetVisible(false);
                PlayerListStore.Reset();
                _maxPlayers = 0;
            }

            _wasConnected = nowConnected;

            if (!nowConnected) return;

            // Show only while F8 is held
            SetVisible(Input.GetKey(HOLD_KEY) && !IsInputFocused());

            // Periodically report this client's RTT to the server
            _pingReportTimer -= Time.unscaledDeltaTime;
            if (_pingReportTimer <= 0f)
            {
                ReportPingToServer();
                _pingReportTimer = PING_REPORT_INTERVAL;
            }
        }

        private void OnConnectedToDedicatedServer(string host, int port)
        {
            EnsureUiBuilt();
            _wasConnected = true;
            _pingReportTimer = 0f;
        }

        private void OnPlayerListUpdated(IReadOnlyList<PlayerListEntry> players)
        {
            if (_panel == null) return;

            // Throttle redraws to avoid unnecessary work between server broadcasts
            float now = Time.unscaledTime;
            if (now - _lastRefreshTime < 0.3f) return;
            _lastRefreshTime = now;

            RefreshRows(players);
        }

        #region UI Construction

        private void EnsureUiBuilt()
        {
            if (_canvasGo != null && _panel != null) return;
            try
            {
                BuildUI();
            }
            catch (Exception ex)
            {
                _logger.Warning($"PlayerListUIManager.BuildUI: {ex.Message}");
            }
        }

        private void BuildUI()
        {
            _font = AcquireFont();

            // ── Canvas ──────────────────────────────────────────────────────
            _canvasGo = new GameObject("DS_PlayerListCanvas");
            GameObject.DontDestroyOnLoad(_canvasGo);

            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            _canvasGo.AddComponent<GraphicRaycaster>();

            // ── Outer panel (top-right, fixed size) ─────────────────────────
            _panel = MakePanel(_canvasGo.transform, "DS_PlayerListPanel");
            var panelRt = _panel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(1f, 1f);
            panelRt.anchorMax = new Vector2(1f, 1f);
            panelRt.pivot = new Vector2(1f, 1f);
            panelRt.sizeDelta = new Vector2(336f, 0f); // height driven by ContentSizeFitter
            panelRt.anchoredPosition = new Vector2(-10f, -10f);

            var panelImg = _panel.AddComponent<Image>();
            panelImg.color = PANEL_BG;

            var panelFitter = _panel.AddComponent<ContentSizeFitter>();
            panelFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var panelVlg = _panel.AddComponent<VerticalLayoutGroup>();
            panelVlg.padding = new RectOffset(8, 8, 6, 6);
            panelVlg.spacing = 2f;
            panelVlg.childControlHeight = false;
            panelVlg.childControlWidth = true;
            panelVlg.childForceExpandWidth = true;
            panelVlg.childForceExpandHeight = false;

            // ── Header ───────────────────────────────────────────────────────
            var headerGo = MakePanel(_panel.transform, "DS_PlayerListHeader");
            headerGo.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 22f);
            _headerText = headerGo.AddComponent<Text>();
            _headerText.text = "Players";
            _headerText.color = HEADER_COLOR;
            _headerText.fontSize = 13;
            _headerText.fontStyle = FontStyle.Bold;
            _headerText.alignment = TextAnchor.MiddleLeft;
            _headerText.font = _font;

            // ── Column headers ──────────────────────────────────────────────
            CreateColumnHeaderRow();

            // ── Thin separator ───────────────────────────────────────────────
            var sepGo = MakePanel(_panel.transform, "DS_PlayerListSep");
            sepGo.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 1f);
            var sepImg = sepGo.AddComponent<Image>();
            sepImg.color = new Color(0.25f, 0.25f, 0.25f, 1f);

            // ── Row container (expands to fit rows) ──────────────────────────
            var rowContainerGo = MakePanel(_panel.transform, "DS_PlayerListRows");
            _rowContainer = rowContainerGo.transform;

            var rowVlg = rowContainerGo.AddComponent<VerticalLayoutGroup>();
            rowVlg.spacing = 1f;
            rowVlg.childControlHeight = false;
            rowVlg.childControlWidth = true;
            rowVlg.childForceExpandWidth = true;
            rowVlg.childForceExpandHeight = false;

            var rowFitter = rowContainerGo.AddComponent<ContentSizeFitter>();
            rowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Always start hidden; Update() will show it when F8 is held
            _isVisible = false;
            _panel.SetActive(false);
            DebugLog.Debug("PlayerListUIManager: UI canvas built");
        }

        private PlayerRow CreateRow(int index)
        {
            var rowGo = new GameObject($"DS_PlayerRow_{index}");
            rowGo.transform.SetParent(_rowContainer, false);

            var rowRt = rowGo.AddComponent<RectTransform>();
            rowRt.sizeDelta = new Vector2(0f, 18f);

            var bg = rowGo.AddComponent<Image>();
            bg.color = (index % 2 == 0) ? ROW_BG_EVEN : ROW_BG_ODD;

            var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(4, 4, 1, 1);
            hlg.childControlHeight = true;
            hlg.childControlWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childForceExpandWidth = false;

            // Name cell (takes most of the width)
            var nameGo = new GameObject("NameText");
            nameGo.transform.SetParent(rowGo.transform, false);
            nameGo.AddComponent<RectTransform>().sizeDelta = new Vector2(NAME_COLUMN_WIDTH, 0f);
            var nameText = nameGo.AddComponent<Text>();
            nameText.color = Color.white;
            nameText.fontSize = 11;
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.font = _font;

            // Role cell
            var roleGo = new GameObject("RoleText");
            roleGo.transform.SetParent(rowGo.transform, false);
            roleGo.AddComponent<RectTransform>().sizeDelta = new Vector2(ROLE_COLUMN_WIDTH, 0f);
            var roleText = roleGo.AddComponent<Text>();
            roleText.color = ROLE_PLAYER;
            roleText.fontSize = 10;
            roleText.alignment = TextAnchor.MiddleCenter;
            roleText.fontStyle = FontStyle.Bold;
            roleText.font = _font;

            // Ping cell (right-aligned)
            var pingGo = new GameObject("PingText");
            pingGo.transform.SetParent(rowGo.transform, false);
            pingGo.AddComponent<RectTransform>().sizeDelta = new Vector2(PING_COLUMN_WIDTH, 0f);
            var pingText = pingGo.AddComponent<Text>();
            pingText.color = PING_UNKNOWN;
            pingText.fontSize = 11;
            pingText.alignment = TextAnchor.MiddleRight;
            pingText.font = _font;

            return new PlayerRow { Root = rowGo, NameText = nameText, RoleText = roleText, PingText = pingText };
        }

        private void CreateColumnHeaderRow()
        {
            var headerRowGo = new GameObject("DS_PlayerListColumnHeaders");
            headerRowGo.transform.SetParent(_panel.transform, false);

            var headerRowRt = headerRowGo.AddComponent<RectTransform>();
            headerRowRt.sizeDelta = new Vector2(0f, 16f);

            var hlg = headerRowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(4, 4, 0, 0);
            hlg.spacing = 0f;
            hlg.childControlHeight = true;
            hlg.childControlWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childForceExpandWidth = false;

            CreateColumnHeaderCell(headerRowGo.transform, "NameHeader", "NAME", NAME_COLUMN_WIDTH, TextAnchor.MiddleLeft);
            CreateColumnHeaderCell(headerRowGo.transform, "RoleHeader", "ROLE", ROLE_COLUMN_WIDTH, TextAnchor.MiddleCenter);
            CreateColumnHeaderCell(headerRowGo.transform, "PingHeader", "PING", PING_COLUMN_WIDTH, TextAnchor.MiddleRight);
        }

        private void CreateColumnHeaderCell(Transform parent, string name, string text, float width, TextAnchor alignment)
        {
            var cellGo = new GameObject(name);
            cellGo.transform.SetParent(parent, false);

            var rectTransform = cellGo.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(width, 0f);

            var headerText = cellGo.AddComponent<Text>();
            headerText.text = text;
            headerText.color = COLUMN_HEADER_COLOR;
            headerText.fontSize = 9;
            headerText.fontStyle = FontStyle.Bold;
            headerText.alignment = alignment;
            headerText.font = _font;
        }

        #endregion

        private void RefreshRows(IReadOnlyList<PlayerListEntry> players)
        {
            if (_rowContainer == null || _headerText == null) return;

            // Sort: lowest ping first; unknown (-1) goes last
            var sorted = players
                .OrderBy(p => p.PingMs < 0 ? int.MaxValue : p.PingMs)
                .ToList();

            if (_maxPlayers == 0)
                _maxPlayers = ServerDataStore.Current?.MaxPlayers ?? 0;
            _headerText.text = _maxPlayers > 0
                ? $"Players ({sorted.Count}/{_maxPlayers})"
                : $"Players ({sorted.Count})";

            // Grow pool as needed
            while (_rows.Count < sorted.Count && _rows.Count < MAX_ROWS)
                _rows.Add(CreateRow(_rows.Count));

            // Update or hide each row
            for (int i = 0; i < _rows.Count; i++)
            {
                bool active = i < sorted.Count;
                _rows[i].Root.SetActive(active);
                if (!active) continue;

                var entry = sorted[i];
                _rows[i].NameText.text = entry.DisplayName;
                _rows[i].RoleText.text = entry.Role;
                _rows[i].RoleText.color = GetRoleColor(entry.Role);

                if (entry.PingMs < 0)
                {
                    _rows[i].PingText.text = "---";
                    _rows[i].PingText.color = PING_UNKNOWN;
                }
                else
                {
                    _rows[i].PingText.text = $"{entry.PingMs}ms";
                    _rows[i].PingText.color = entry.PingMs < 70 ? PING_GOOD
                                            : entry.PingMs < 150 ? PING_OK
                                            : PING_BAD;
                }
            }
        }

        private static Color GetRoleColor(string role)
        {
            switch (role)
            {
                case "Operator":
                    return ROLE_OPERATOR;
                case "Admin":
                    return ROLE_ADMIN;
                case "Player":
                default:
                    return ROLE_PLAYER;
            }
        }

        private void SetVisible(bool visible)
        {
            if (_isVisible == visible) return;
            _isVisible = visible;
            _panel?.SetActive(visible);
        }

        private void ReportPingToServer()
        {
            try
            {
                int rttMs = -1;
                var tm = InstanceFinder.TimeManager;
                if (tm != null)
                {
                    long raw = tm.RoundTripTime;
                    rttMs = (int)Math.Min(raw, 9999L);
                }
                CustomMessaging.SendToServer(Constants.Messages.PlayerPingReport, rttMs.ToString());
                DebugLog.Verbose($"PlayerListUIManager: reported ping {rttMs}ms");
            }
            catch (Exception ex)
            {
                DebugLog.Debug($"PlayerListUIManager: ping report error: {ex.Message}");
            }
        }

        private static bool IsInputFocused()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            return es != null && es.currentSelectedGameObject != null;
        }

        private static GameObject MakePanel(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        /// <summary>
        /// Returns the first usable <see cref="Font"/> found in the scene, falling back to Unity's
        /// built-in resource fonts and finally a dynamically-created OS font.
        /// </summary>
        private static Font AcquireFont()
        {
            // Try to reuse a font already loaded in the scene
            var existing = UnityEngine.Object.FindObjectOfType<Text>();
            if (existing != null && existing.font != null)
                return existing.font;

            // Unity built-in fonts (name changed between Unity versions)
            foreach (string name in new[] { "LegacyRuntime.ttf", "Arial.ttf" })
            {
                var f = Resources.GetBuiltinResource<Font>(name);
                if (f != null) return f;
            }

            // Last resort: OS font
            return Font.CreateDynamicFontFromOSFont("Arial", 11);
        }

        private sealed class PlayerRow
        {
            public GameObject Root;
            public Text NameText;
            public Text RoleText;
            public Text PingText;
        }

    }
}
#endif
