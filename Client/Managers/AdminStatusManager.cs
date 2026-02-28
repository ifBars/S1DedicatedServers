using System;
using System.Collections.Generic;
using MelonLoader;
#if IL2CPP
using Il2CppFishNet;
using Il2CppFishNet.Connection;
using Il2CppFishNet.Managing;
using Il2CppScheduleOne.PlayerScripts;
#else
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using ScheduleOne.PlayerScripts;
#endif
using UnityEngine;

namespace DedicatedServerMod.Client.Managers
{
    /// <summary>
    /// Manages admin status checking and caching for client-side console access.
    /// Provides methods to query and cache admin permissions from the server.
    /// </summary>
    public static class AdminStatusManager
    {
        private static MelonLogger.Instance logger;
        private static bool? _cachedAdminStatus = null;
        private static bool? _cachedOperatorStatus = null;
        private static float _lastStatusCheck = 0f;
        private static readonly float STATUS_CHECK_INTERVAL = 10f; // Check every 10 seconds
        private static readonly float STATUS_TIMEOUT = 30f; // Consider status stale after 30 seconds

        // Steam ID to admin/operator status mapping (for offline checking)
        private static Dictionary<string, bool> _cachedAdminList = new Dictionary<string, bool>();
        private static Dictionary<string, bool> _cachedOperatorList = new Dictionary<string, bool>();

        public static void Initialize(MelonLogger.Instance loggerInstance)
        {
            logger = loggerInstance;
            logger?.Msg("AdminStatusManager initialized");
        }

        /// <summary>
        /// Check if the local player is an admin on the current server
        /// </summary>
        public static bool IsLocalPlayerAdmin()
        {
            try
            {
                // Return cached result if recent and valid
                if (_cachedAdminStatus.HasValue && 
                    Time.time - _lastStatusCheck < STATUS_CHECK_INTERVAL)
                {
                    return _cachedAdminStatus.Value;
                }

                var localPlayer = Player.Local;
                if (localPlayer == null)
                {
                    _cachedAdminStatus = false;
                    _lastStatusCheck = Time.time;
                    return false;
                }

                // For dedicated server connections, we need to implement proper admin checking
                if (InstanceFinder.IsClient && !InstanceFinder.IsHost)
                {
                    // Only trust server-provided status via cache
                    string playerId = GetLocalPlayerId();
                    
                    if (!string.IsNullOrEmpty(playerId))
                    {
                        if (_cachedAdminList.TryGetValue(playerId, out bool isAdmin))
                        {
                            bool result = isAdmin;
                            _cachedAdminStatus = result;
                            _lastStatusCheck = Time.time;
                            return result;
                        }
                        
                        if (_cachedOperatorList.TryGetValue(playerId, out bool isOperator))
                        {
                            bool result = isOperator;
                            _cachedAdminStatus = result;
                            _lastStatusCheck = Time.time;
                            return result;
                        }
                        
                    }

                    // If no cached server-provided status is available, default to false
                    _cachedAdminStatus = false;
                    _lastStatusCheck = Time.time;
                    return false;
                }

                // For hosts, they always have admin access
                if (InstanceFinder.IsHost)
                {
                    _cachedAdminStatus = true;
                    _lastStatusCheck = Time.time;
                    return true;
                }

                _cachedAdminStatus = false;
                _lastStatusCheck = Time.time;
                return false;
            }
            catch (Exception ex)
            {
                logger?.Error($"IsLocalPlayerAdmin: Exception occurred: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Check if the local player is an operator on the current server
        /// </summary>
        public static bool IsLocalPlayerOperator()
        {
            try
            {
                // Return cached result if recent and valid
                if (_cachedOperatorStatus.HasValue && 
                    Time.time - _lastStatusCheck < STATUS_CHECK_INTERVAL)
                {
                    return _cachedOperatorStatus.Value;
                }

                var localPlayer = Player.Local;
                if (localPlayer == null)
                {
                    _cachedOperatorStatus = false;
                    return false;
                }

                // Check cached operator status
                string playerId = GetLocalPlayerId();
                if (!string.IsNullOrEmpty(playerId) && 
                    _cachedOperatorList.TryGetValue(playerId, out bool isOperator))
                {
                    _cachedOperatorStatus = isOperator;
                    return isOperator;
                }

                // For hosts, they always have operator access
                if (InstanceFinder.IsHost)
                {
                    _cachedOperatorStatus = true;
                    return true;
                }

                _cachedOperatorStatus = false;
                return false;
            }
            catch (Exception ex)
            {
                logger?.Error($"Error checking operator status: {ex}");
                _cachedOperatorStatus = false;
                return false;
            }
        }

        /// <summary>
        /// Update admin status from server data (called when server sends admin list)
        /// </summary>
        public static void UpdateAdminStatus(string playerId, bool isAdmin, bool isOperator)
        {
            if (string.IsNullOrEmpty(playerId)) return;

            _cachedAdminList[playerId] = isAdmin;
            _cachedOperatorList[playerId] = isOperator;
            
            // If this is the local player, update cached status
            string localPlayerId = GetLocalPlayerId();
            if (playerId == localPlayerId)
            {
                _cachedAdminStatus = isAdmin || isOperator;
                _cachedOperatorStatus = isOperator;
                _lastStatusCheck = Time.time;
                
                logger?.Msg($"Updated local player admin status: Admin={isAdmin}, Operator={isOperator}");
            }
        }

        /// <summary>
        /// Clear all cached admin status (call when disconnecting or changing servers)
        /// </summary>
        public static void ClearCache()
        {
            _cachedAdminStatus = null;
            _cachedOperatorStatus = null;
            _lastStatusCheck = 0f;
            _cachedAdminList.Clear();
            _cachedOperatorList.Clear();
            
            logger?.Msg("Admin status cache cleared");
        }

        /// <summary>
        /// Force refresh of admin status on next check
        /// </summary>
        public static void InvalidateCache()
        {
            _cachedAdminStatus = null;
            _cachedOperatorStatus = null;
            _lastStatusCheck = 0f;
            
            logger?.Msg("Admin status cache invalidated");
        }

        /// <summary>
        /// Get the local player's identifier for admin checking
        /// </summary>
        private static string GetLocalPlayerId()
        {
            try
            {
                var localPlayer = Player.Local;
                if (localPlayer?.Owner?.ClientId != null)
                {
                    // For now, use ClientId as identifier
                    string clientId = localPlayer.Owner.ClientId.ToString();
                    return clientId;
                }
                
                logger?.Warning("GetLocalPlayerId: Local player or Owner or ClientId is null");
                return null;
            }
            catch (Exception ex)
            {
                logger?.Error($"GetLocalPlayerId: Error getting local player ID: {ex}");
                return null;
            }
        }

        

        /// <summary>
        /// Check if a specific command is allowed for the current player
        /// </summary>
        public static bool CanUseCommand(string command)
        {
            if (string.IsNullOrEmpty(command)) return false;

            command = command.ToLower();

            // Operators can use all commands
            if (IsLocalPlayerOperator()) return true;

            // Admins can use most commands but not restricted ones
            if (IsLocalPlayerAdmin())
            {
                var restrictedCommands = new[]
                {
                    "settimescale", "freecam", "disable", "enable", "endtutorial",
                    "disablenpcasset", "hideui", "bind", "unbind", "clearbinds"
                };

                return !Array.Exists(restrictedCommands, 
                    cmd => cmd.Equals(command, StringComparison.OrdinalIgnoreCase));
            }

            return false;
        }

        /// <summary>
        /// Get a descriptive string of the current player's permissions
        /// </summary>
        public static string GetPermissionInfo()
        {
            if (IsLocalPlayerOperator())
                return "OPERATOR (Full console access)";
            else if (IsLocalPlayerAdmin())
                return "ADMIN (Limited console access)";
            else
                return "PLAYER (No console access)";
        }
    }
}
