using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using MelonLoader;
using FishNet;
using MelonLoader.Utils;
using ScheduleOne.PlayerScripts;
using Steamworks;

namespace DedicatedServerMod
{
    /// <summary>
    /// Comprehensive server configuration management system with admin/operator permissions
    /// Handles all server settings, admin management, and persistence
    /// </summary>
    [Serializable]
    public class ServerConfig
    {
        #region Server Settings
        [JsonProperty("serverName")]
        public string ServerName { get; set; } = "Schedule One Dedicated Server";

        [JsonProperty("serverDescription")]
        public string ServerDescription { get; set; } = "A dedicated server for Schedule One";

        [JsonProperty("maxPlayers")]
        public int MaxPlayers { get; set; } = 16;

        [JsonProperty("serverPort")]
        public int ServerPort { get; set; } = 38465;

        [JsonProperty("serverPassword")]
        public string ServerPassword { get; set; } = "";

        [JsonProperty("requireAuthentication")]
        public bool RequireAuthentication { get; set; } = false;

        [JsonProperty("requireFriends")]
        public bool RequireFriends { get; set; } = false;

        [JsonProperty("publicServer")]
        public bool PublicServer { get; set; } = true;
        #endregion

        #region Time & Gameplay Settings
        [JsonProperty("ignoreGhostHostForSleep")]
        public bool IgnoreGhostHostForSleep { get; set; } = true;

        [JsonProperty("timeNeverStops")]
        public bool TimeNeverStops { get; set; } = true;

        [JsonProperty("timeProgressionMultiplier")]
        public float TimeProgressionMultiplier { get; set; } = 1.0f;

        [JsonProperty("allowSleeping")]
        public bool AllowSleeping { get; set; } = true;

        [JsonProperty("pauseGameWhenEmpty")]
        public bool PauseGameWhenEmpty { get; set; } = false;
        #endregion

        #region Auto-Save Settings
        [JsonProperty("autoSaveEnabled")]
        public bool AutoSaveEnabled { get; set; } = true;

        [JsonProperty("autoSaveIntervalMinutes")]
        public float AutoSaveIntervalMinutes { get; set; } = 10f;

        [JsonProperty("autoSaveOnPlayerJoin")]
        public bool AutoSaveOnPlayerJoin { get; set; } = true;

        [JsonProperty("autoSaveOnPlayerLeave")]
        public bool AutoSaveOnPlayerLeave { get; set; } = true;

        [JsonProperty("maxAutoSaveBackups")]
        public int MaxAutoSaveBackups { get; set; } = 5;
        #endregion

        #region Admin/Operator System
        [JsonProperty("operators")]
        public HashSet<string> Operators { get; set; } = new HashSet<string>();

        [JsonProperty("admins")]
        public HashSet<string> Admins { get; set; } = new HashSet<string>();

        [JsonProperty("enableConsoleForOps")]
        public bool EnableConsoleForOps { get; set; } = true;

        [JsonProperty("enableConsoleForAdmins")]
        public bool EnableConsoleForAdmins { get; set; } = true;

        [JsonProperty("logAdminCommands")]
        public bool LogAdminCommands { get; set; } = true;

        [JsonProperty("allowedCommands")]
        public HashSet<string> AllowedCommands { get; set; } = new HashSet<string>
        {
            // Safe commands that admins can use
            "settime", "save", "teleport", "give", "clearinventory",
            "changecash", "changebalance", "addxp", "spawnvehicle",
            "setmovespeed", "setjumpforce", "setowned", "sethealth",
            "setenergy", "setvar", "setqueststate", "setquestentrystate",
            "setemotion", "setunlocked", "setrelationship", "addemployee",
            "setdiscovered", "growplants", "setlawintensity", "setquality",
            "cleartrash", "raisewanted", "lowerwanted", "clearwanted",
            "packageproduct", "setstaminareserve"
        };

        [JsonProperty("restrictedCommands")]
        public HashSet<string> RestrictedCommands { get; set; } = new HashSet<string>
        {
            // Commands only operators can use
            "settimescale", "freecam", "endtutorial", "disable", "enable",
            "disablenpcasset", "hideui", "showfps", "hidefps"
        };
        #endregion

        #region MOTD & Welcome Messages
        [JsonProperty("enableMotd")]
        public bool EnableMotd { get; set; } = true;

        [JsonProperty("motdMessage")]
        public string MotdMessage { get; set; } = "Welcome to the server! Type /help for commands.";

        [JsonProperty("welcomeMessage")]
        public string WelcomeMessage { get; set; } = "Welcome {playerName} to {serverName}!";

        [JsonProperty("showPlayerJoinMessages")]
        public bool ShowPlayerJoinMessages { get; set; } = true;

        [JsonProperty("showPlayerLeaveMessages")]
        public bool ShowPlayerLeaveMessages { get; set; } = true;
        #endregion

        #region Debug & Logging
        [JsonProperty("debugMode")]
        public bool DebugMode { get; set; } = false;

        [JsonProperty("verboseLogging")]
        public bool VerboseLogging { get; set; } = false;

        [JsonProperty("logPlayerActions")]
        public bool LogPlayerActions { get; set; } = true;

        [JsonProperty("enablePerformanceMonitoring")]
        public bool EnablePerformanceMonitoring { get; set; } = false;
        #endregion

        #region Static Instance & Management
        private static ServerConfig _instance;
        private static MelonLogger.Instance logger;
        private static string configPath;

        public static ServerConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    LoadConfig();
                }
                return _instance;
            }
        }

        public static void Initialize(MelonLogger.Instance loggerInstance)
        {
            logger = loggerInstance;
            configPath = Path.Combine(MelonEnvironment.UserDataDirectory, "server_config.json");
            LoadConfig();
        }
        #endregion

        #region Config File Management
        public static void LoadConfig()
        {
            try
            {
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    _instance = JsonConvert.DeserializeObject<ServerConfig>(json);
                    logger?.Msg("Server configuration loaded successfully");
                }
                else
                {
                    _instance = new ServerConfig();
                    SaveConfig();
                    logger?.Msg("Created new server configuration file");
                }
            }
            catch (Exception ex)
            {
                logger?.Error($"Failed to load server config: {ex}");
                _instance = new ServerConfig();
                SaveConfig();
            }
        }

        public static void SaveConfig()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_instance, Formatting.Indented);
                File.WriteAllText(configPath, json);
                logger?.Msg("Server configuration saved successfully");
            }
            catch (Exception ex)
            {
                logger?.Error($"Failed to save server config: {ex}");
            }
        }

        public static void ReloadConfig()
        {
            logger?.Msg("Reloading server configuration...");
            LoadConfig();
        }
        #endregion

        #region Admin/Operator Management
        public static bool IsOperator(string steamId)
        {
            if (string.IsNullOrEmpty(steamId)) return false;
            return Instance.Operators.Contains(steamId);
        }

        public static bool IsAdmin(string steamId)
        {
            if (string.IsNullOrEmpty(steamId)) return false;
            return Instance.Admins.Contains(steamId) || IsOperator(steamId);
        }

        public static bool IsOperator(Player player)
        {
            if (player?.Owner?.ClientId == null) return false;
            string steamId = GetPlayerSteamId(player);
            return IsOperator(steamId);
        }

        public static bool IsAdmin(Player player)
        {
            if (player?.Owner?.ClientId == null) return false;
            string steamId = GetPlayerSteamId(player);
            return IsAdmin(steamId);
        }

        public static bool AddOperator(string steamId)
        {
            if (string.IsNullOrEmpty(steamId)) return false;
            
            bool added = Instance.Operators.Add(steamId);
            if (added)
            {
                // Also add to admins if not already there
                Instance.Admins.Add(steamId);
                SaveConfig();
                logger?.Msg($"Added operator: {steamId}");
            }
            return added;
        }

        public static bool RemoveOperator(string steamId)
        {
            if (string.IsNullOrEmpty(steamId)) return false;
            
            bool removed = Instance.Operators.Remove(steamId);
            if (removed)
            {
                SaveConfig();
                logger?.Msg($"Removed operator: {steamId}");
            }
            return removed;
        }

        public static bool AddAdmin(string steamId)
        {
            if (string.IsNullOrEmpty(steamId)) return false;
            
            bool added = Instance.Admins.Add(steamId);
            if (added)
            {
                SaveConfig();
                logger?.Msg($"Added admin: {steamId}");
            }
            return added;
        }

        public static bool RemoveAdmin(string steamId)
        {
            if (string.IsNullOrEmpty(steamId)) return false;
            
            bool removed = Instance.Admins.Remove(steamId);
            if (removed)
            {
                // Also remove from operators if they were one
                Instance.Operators.Remove(steamId);
                SaveConfig();
                logger?.Msg($"Removed admin: {steamId}");
            }
            return removed;
        }

        public static List<string> GetAllOperators()
        {
            return new List<string>(Instance.Operators);
        }

        public static List<string> GetAllAdmins()
        {
            return new List<string>(Instance.Admins);
        }
        #endregion

        #region Permission Checking
        public static bool CanUseConsole(Player player)
        {
            logger?.Msg($"[DEBUG] CanUseConsole called for player: {player?.PlayerName ?? "null"}");
            
            if (player?.Owner?.ClientId == null)
            {
                logger?.Warning("[DEBUG] CanUseConsole: Player or Owner or ClientId is null");
                return false;
            }

            // Allow console on server for remote clients (treat loopback host as non-remote)
            if (InstanceFinder.IsServer)
            {
                bool isHostFlag = InstanceFinder.IsHost;
                bool isClientFlag = InstanceFinder.IsClient;
                bool isRemoteClient = player.Owner != null && !player.Owner.IsLocalClient;
                
                logger?.Msg($"[DEBUG] CanUseConsole: Server-side check (IsServer: {InstanceFinder.IsServer}, IsHost: {isHostFlag}, IsClient: {isClientFlag}, PlayerIsRemote: {isRemoteClient})");
                
                if (!isRemoteClient)
                {
                    logger?.Warning($"[DEBUG] CanUseConsole: Player {player.PlayerName} is local/loopback; denying console access here");
                    return false;
                }
                
                bool isOp = IsOperator(player);
                bool isAdmin = IsAdmin(player);
                
                logger?.Msg($"[DEBUG] CanUseConsole: Player {player.PlayerName} - IsOperator: {isOp}, IsAdmin: {isAdmin}");
                logger?.Msg($"[DEBUG] CanUseConsole: EnableConsoleForOps: {Instance.EnableConsoleForOps}, EnableConsoleForAdmins: {Instance.EnableConsoleForAdmins}");
                
                if (isOp && Instance.EnableConsoleForOps)
                {
                    logger?.Msg($"[DEBUG] CanUseConsole: Allowing console access for operator {player.PlayerName}");
                    return true;
                }
                if (isAdmin && Instance.EnableConsoleForAdmins)
                {
                    logger?.Msg($"[DEBUG] CanUseConsole: Allowing console access for admin {player.PlayerName}");
                    return true;
                }
                
                logger?.Warning($"[DEBUG] CanUseConsole: Denying console access for {player.PlayerName} - not operator/admin or console disabled");
            }
            else
            {
                logger?.Msg($"[DEBUG] CanUseConsole: Not on server (IsServer: {InstanceFinder.IsServer}), denying");
            }

            logger?.Warning($"[DEBUG] CanUseConsole: Final result: false for {player?.PlayerName ?? "null"}");
            return false;
        }

        public static bool CanUseCommand(Player player, string command)
        {
            logger?.Msg($"[DEBUG] CanUseCommand called for player: {player?.PlayerName ?? "null"}, command: {command}");
            
            if (player?.Owner?.ClientId == null)
            {
                logger?.Warning("[DEBUG] CanUseCommand: Player or Owner or ClientId is null");
                return false;
            }
            if (string.IsNullOrEmpty(command))
            {
                logger?.Warning("[DEBUG] CanUseCommand: Command is null or empty");
                return false;
            }

            command = command.ToLower();
            logger?.Msg($"[DEBUG] CanUseCommand: Normalized command: {command}");

            // Operators can use all commands
            if (IsOperator(player))
            {
                logger?.Msg($"[DEBUG] CanUseCommand: Player {player.PlayerName} is operator - allowing all commands");
                return true;
            }

            // Admins can use allowed commands but not restricted ones
            if (IsAdmin(player))
            {
                logger?.Msg($"[DEBUG] CanUseCommand: Player {player.PlayerName} is admin - checking command permissions");
                
                if (Instance.RestrictedCommands.Contains(command))
                {
                    logger?.Warning($"[DEBUG] CanUseCommand: Command '{command}' is restricted for admins");
                    return false;
                }
                
                bool isAllowed = Instance.AllowedCommands.Contains(command);
                logger?.Msg($"[DEBUG] CanUseCommand: Command '{command}' allowed for admin: {isAllowed}");
                return isAllowed;
            }

            logger?.Warning($"[DEBUG] CanUseCommand: Player {player.PlayerName} is neither operator nor admin");
            return false;
        }

        public static bool CanUseCommand(string steamId, string command)
        {
            logger?.Msg($"[DEBUG] CanUseCommand(string) called for steamId: {steamId}, command: {command}");
            
            if (string.IsNullOrEmpty(steamId) || string.IsNullOrEmpty(command))
            {
                logger?.Warning("[DEBUG] CanUseCommand(string): SteamId or command is null or empty");
                return false;
            }

            command = command.ToLower();
            logger?.Msg($"[DEBUG] CanUseCommand(string): Normalized command: {command}");

            // Operators can use all commands
            if (IsOperator(steamId))
            {
                logger?.Msg($"[DEBUG] CanUseCommand(string): SteamId {steamId} is operator - allowing all commands");
                return true;
            }

            // Admins can use allowed commands but not restricted ones
            if (IsAdmin(steamId))
            {
                logger?.Msg($"[DEBUG] CanUseCommand(string): SteamId {steamId} is admin - checking command permissions");
                
                if (Instance.RestrictedCommands.Contains(command))
                {
                    logger?.Warning($"[DEBUG] CanUseCommand(string): Command '{command}' is restricted for admins");
                    return false;
                }
                
                bool isAllowed = Instance.AllowedCommands.Contains(command);
                logger?.Msg($"[DEBUG] CanUseCommand(string): Command '{command}' allowed for admin: {isAllowed}");
                return isAllowed;
            }

            logger?.Warning($"[DEBUG] CanUseCommand(string): SteamId {steamId} is neither operator nor admin");
            return false;
        }
        #endregion

        #region Utility Methods
        public static string GetPlayerSteamId(Player player)
        {
            try
            {
                logger?.Msg($"[DEBUG] GetPlayerSteamId called for player: {player?.PlayerName ?? "null"}");

                if (player == null || player.Owner == null)
                {
                    logger?.Warning("[DEBUG] GetPlayerSteamId: Player or Owner is null");
                    return null;
                }

                // Preferred: PlayerCode is synced from client via SendPlayerNameData and holds SteamID as string
                if (!string.IsNullOrEmpty(player.PlayerCode))
                {
                    logger?.Msg($"[DEBUG] GetPlayerSteamId: Using Player.PlayerCode: {player.PlayerCode}");
                    return player.PlayerCode;
                }

                // Secondary (server-only): Look up mapping captured by ServerManager when player spawned
#if SERVER
                var connected = ServerManager.GetPlayer(player.Owner);
                if (connected != null && !string.IsNullOrEmpty(connected.SteamId))
                {
                    logger?.Msg($"[DEBUG] GetPlayerSteamId: Using ServerManager mapping: {connected.SteamId}");
                    return connected.SteamId;
                }
#endif

                // Tertiary (local only): If Steam is running and this is the local server client
                if (SteamAPI.IsSteamRunning() && player.Owner.IsLocalClient)
                {
                    try
                    {
                        var localSteamId = SteamUser.GetSteamID().m_SteamID.ToString();
                        logger?.Msg($"[DEBUG] GetPlayerSteamId: LocalClient fallback SteamID: {localSteamId}");
                        return localSteamId;
                    }
                    catch (Exception ex)
                    {
                        logger?.Warning($"[DEBUG] GetPlayerSteamId: Failed local SteamID read: {ex.Message}");
                    }
                }

                // Last resort: use FishNet ClientId as placeholder (NOT a real SteamID)
                string fallbackId = player.Owner.ClientId.ToString();
                logger?.Warning($"[DEBUG] GetPlayerSteamId: Falling back to ClientId (not a SteamID): {fallbackId}");
                return fallbackId;
            }
            catch (Exception ex)
            {
                logger?.Error($"[DEBUG] GetPlayerSteamId error: {ex}");
                return null;
            }
        }

        public static Player GetPlayerBySteamId(string steamId)
        {
            logger?.Msg($"[DEBUG] GetPlayerBySteamId called for steamId: {steamId}");
            
            if (string.IsNullOrEmpty(steamId))
            {
                logger?.Warning("[DEBUG] GetPlayerBySteamId: SteamId is null or empty");
                return null;
            }

            foreach (var player in Player.PlayerList)
            {
                string playerSteamId = GetPlayerSteamId(player);
                logger?.Msg($"[DEBUG] GetPlayerBySteamId: Checking player {player?.PlayerName ?? "null"} with steamId: {playerSteamId}");
                
                if (playerSteamId == steamId)
                {
                    logger?.Msg($"[DEBUG] GetPlayerBySteamId: Found player {player.PlayerName} for steamId {steamId}");
                    return player;
                }
            }

            logger?.Warning($"[DEBUG] GetPlayerBySteamId: No player found for steamId {steamId}");
            return null;
        }

        public static void LogAdminAction(Player player, string command, string args = "")
        {
            logger?.Msg($"[DEBUG] LogAdminAction called for player: {player?.PlayerName ?? "null"}, command: {command}, args: {args}");
            
            if (!Instance.LogAdminCommands)
            {
                logger?.Msg("[DEBUG] LogAdminAction: Admin command logging is disabled");
                return;
            }

            string steamId = GetPlayerSteamId(player);
            string playerName = player?.PlayerName ?? "Unknown";
            string logMessage = $"Admin Action - Player: {playerName} ({steamId}) | Command: {command}";
            
            if (!string.IsNullOrEmpty(args))
            {
                logMessage += $" | Args: {args}";
            }

            logger?.Msg(logMessage);

            // Also write to admin log file
            try
            {
                string adminLogPath = Path.Combine(Application.persistentDataPath, "admin_actions.log");
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {logMessage}\n";
                File.AppendAllText(adminLogPath, logEntry);
                logger?.Msg($"[DEBUG] LogAdminAction: Wrote to admin log file: {adminLogPath}");
            }
            catch (Exception ex)
            {
                logger?.Error($"[DEBUG] LogAdminAction: Failed to write to admin log: {ex}");
            }
        }

        /// <summary>
        /// Get the current local player's Steam ID for testing/admin purposes
        /// </summary>
        public static string GetLocalPlayerSteamId()
        {
            try
            {
                if (SteamManager.Initialized)
                {
                    CSteamID steamId = SteamUser.GetSteamID();
                    string steamIdString = steamId.m_SteamID.ToString();
                    logger?.Msg($"[DEBUG] GetLocalPlayerSteamId: Local Steam ID: {steamIdString}");
                    return steamIdString;
                }
                else
                {
                    logger?.Warning("[DEBUG] GetLocalPlayerSteamId: Steam not initialized");
                    return null;
                }
            }
            catch (Exception ex)
            {
                logger?.Error($"[DEBUG] GetLocalPlayerSteamId error: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Add the current local player as an operator (for testing)
        /// </summary>
        public static bool AddLocalPlayerAsOperator()
        {
            try
            {
                string steamId = GetLocalPlayerSteamId();
                if (string.IsNullOrEmpty(steamId))
                {
                    logger?.Warning("[DEBUG] AddLocalPlayerAsOperator: Could not get local Steam ID");
                    return false;
                }

                logger?.Msg($"[DEBUG] AddLocalPlayerAsOperator: Adding local player {steamId} as operator");
                return AddOperator(steamId);
            }
            catch (Exception ex)
            {
                logger?.Error($"[DEBUG] AddLocalPlayerAsOperator error: {ex}");
                return false;
            }
        }
        #endregion

        #region Command Line Integration
        public static void ParseCommandLineArgs(string[] args)
        {
            logger?.Msg("Parsing command line arguments for server config...");

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--server-name":
                        if (i + 1 < args.Length)
                        {
                            Instance.ServerName = args[i + 1];
                            logger?.Msg($"Server name set to: {Instance.ServerName}");
                        }
                        break;

                    case "--max-players":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int maxPlayers))
                        {
                            Instance.MaxPlayers = maxPlayers;
                            logger?.Msg($"Max players set to: {Instance.MaxPlayers}");
                        }
                        break;

                    case "--server-password":
                        if (i + 1 < args.Length)
                        {
                            Instance.ServerPassword = args[i + 1];
                            logger?.Msg("Server password set");
                        }
                        break;

                    case "--add-operator":
                        if (i + 1 < args.Length)
                        {
                            AddOperator(args[i + 1]);
                        }
                        break;

                    case "--add-admin":
                        if (i + 1 < args.Length)
                        {
                            AddAdmin(args[i + 1]);
                        }
                        break;

                    case "--debug":
                        Instance.DebugMode = true;
                        logger?.Msg("Debug mode enabled");
                        break;

                    case "--verbose":
                        Instance.VerboseLogging = true;
                        logger?.Msg("Verbose logging enabled");
                        break;
                }
            }

            SaveConfig();
        }
        #endregion

        #region Server Info
        public static string GetServerInfo()
        {
            var info = "=== Server Configuration ===\n";
            info += $"Server Name: {Instance.ServerName}\n";
            info += $"Max Players: {Instance.MaxPlayers}\n";
            info += $"Server Port: {Instance.ServerPort}\n";
            info += $"Password Protected: {!string.IsNullOrEmpty(Instance.ServerPassword)}\n";
            info += $"Public Server: {Instance.PublicServer}\n";
            info += $"Operators: {Instance.Operators.Count}\n";
            info += $"Admins: {Instance.Admins.Count}\n";
            info += $"Auto-Save: {Instance.AutoSaveEnabled} ({Instance.AutoSaveIntervalMinutes}min)\n";
            info += $"Time Never Stops: {Instance.TimeNeverStops}\n";
            info += $"Debug Mode: {Instance.DebugMode}\n";

            return info;
        }
        #endregion
    }
}
