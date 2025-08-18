using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using DedicatedServerMod;

namespace DedicatedServerMod.Server.Player
{
    /// <summary>
    /// Manages player permissions including operators and administrators.
    /// </summary>
    public class PlayerPermissions
    {
        private readonly MelonLogger.Instance logger;

        public PlayerPermissions(MelonLogger.Instance loggerInstance)
        {
            logger = loggerInstance;
        }

        /// <summary>
        /// Initialize the permissions system
        /// </summary>
        public void Initialize()
        {
            logger.Msg("Player permissions system initialized");
        }

        /// <summary>
        /// Check if a player is an operator
        /// </summary>
        public bool IsOperator(string steamId)
        {
            return ServerConfig.IsOperator(steamId);
        }

        /// <summary>
        /// Check if a player is an administrator
        /// </summary>
        public bool IsAdministrator(string steamId)
        {
            return ServerConfig.IsAdmin(steamId);
        }

        /// <summary>
        /// Check if a player has operator privileges
        /// </summary>
        public bool IsOperator(ConnectedPlayerInfo player)
        {
            return IsOperator(player?.SteamId);
        }

        /// <summary>
        /// Check if a player has administrator privileges
        /// </summary>
        public bool IsAdministrator(ConnectedPlayerInfo player)
        {
            return IsAdministrator(player?.SteamId);
        }

        /// <summary>
        /// Check if a player has any elevated privileges
        /// </summary>
        public bool HasElevatedPrivileges(string steamId)
        {
            return IsOperator(steamId) || IsAdministrator(steamId);
        }

        /// <summary>
        /// Check if a player has any elevated privileges
        /// </summary>
        public bool HasElevatedPrivileges(ConnectedPlayerInfo player)
        {
            return HasElevatedPrivileges(player?.SteamId);
        }

        /// <summary>
        /// Add a player as an operator
        /// </summary>
        public bool AddOperator(string steamId)
        {
            return ServerConfig.AddOperator(steamId);
        }

        /// <summary>
        /// Remove a player from operators
        /// </summary>
        public bool RemoveOperator(string steamId)
        {
            return ServerConfig.RemoveOperator(steamId);
        }

        /// <summary>
        /// Add a player as an administrator
        /// </summary>
        public bool AddAdministrator(string steamId)
        {
            return ServerConfig.AddAdmin(steamId);
        }

        /// <summary>
        /// Remove a player from administrators
        /// </summary>
        public bool RemoveAdministrator(string steamId)
        {
            return ServerConfig.RemoveAdmin(steamId);
        }

        /// <summary>
        /// Get all operators
        /// </summary>
        public List<string> GetOperators()
        {
            return new List<string>(ServerConfig.Instance.Operators);
        }

        /// <summary>
        /// Get all administrators
        /// </summary>
        public List<string> GetAdministrators()
        {
            return new List<string>(ServerConfig.Instance.Admins);
        }

        /// <summary>
        /// Get permission level for a player
        /// </summary>
        public PermissionLevel GetPermissionLevel(string steamId)
        {
            if (string.IsNullOrEmpty(steamId))
                return PermissionLevel.None;

            if (IsAdministrator(steamId))
                return PermissionLevel.Administrator;
            
            if (IsOperator(steamId))
                return PermissionLevel.Operator;

            return PermissionLevel.Player;
        }

        /// <summary>
        /// Get permission level for a player
        /// </summary>
        public PermissionLevel GetPermissionLevel(ConnectedPlayerInfo player)
        {
            return GetPermissionLevel(player?.SteamId);
        }

        /// <summary>
        /// Check if a player can execute a command based on required permission level
        /// </summary>
        public bool CanExecuteCommand(ConnectedPlayerInfo player, PermissionLevel requiredLevel)
        {
            var playerLevel = GetPermissionLevel(player);
            return playerLevel >= requiredLevel;
        }

        /// <summary>
        /// Check if a player can execute a command based on required permission level
        /// </summary>
        public bool CanExecuteCommand(string steamId, PermissionLevel requiredLevel)
        {
            var playerLevel = GetPermissionLevel(steamId);
            return playerLevel >= requiredLevel;
        }

        /// <summary>
        /// Get a summary of all permissions
        /// </summary>
        public PermissionSummary GetPermissionSummary()
        {
            return new PermissionSummary
            {
                TotalOperators = ServerConfig.Instance.Operators.Count,
                TotalAdministrators = ServerConfig.Instance.Admins.Count,
                Operators = GetOperators(),
                Administrators = GetAdministrators()
            };
        }
    }

    /// <summary>
    /// Permission levels for players
    /// </summary>
    public enum PermissionLevel
    {
        None = 0,
        Player = 1,
        Operator = 2,
        Administrator = 3
    }

    /// <summary>
    /// Summary of server permissions
    /// </summary>
    public class PermissionSummary
    {
        public int TotalOperators { get; set; }
        public int TotalAdministrators { get; set; }
        public List<string> Operators { get; set; }
        public List<string> Administrators { get; set; }

        public override string ToString()
        {
            return $"Operators: {TotalOperators}, Administrators: {TotalAdministrators}";
        }
    }
}
