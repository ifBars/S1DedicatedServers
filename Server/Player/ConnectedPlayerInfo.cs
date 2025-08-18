using FishNet.Connection;
using ScheduleOne.PlayerScripts;
using System;

namespace DedicatedServerMod.Server.Player
{
    /// <summary>
    /// Information about a connected player including connection details,
    /// identity information, and status.
    /// </summary>
    public class ConnectedPlayerInfo
    {
        /// <summary>
        /// The FishNet network connection
        /// </summary>
        public NetworkConnection Connection { get; set; }

        /// <summary>
        /// The client ID from FishNet
        /// </summary>
        public int ClientId { get; set; }

        /// <summary>
        /// The player's Steam ID
        /// </summary>
        public string SteamId { get; set; }

        /// <summary>
        /// The player's display name
        /// </summary>
        public string PlayerName { get; set; }

        /// <summary>
        /// When the player connected
        /// </summary>
        public DateTime ConnectTime { get; set; }

        /// <summary>
        /// The spawned Player instance (if spawned)
        /// </summary>
        public ScheduleOne.PlayerScripts.Player PlayerInstance { get; set; }

        /// <summary>
        /// Whether the player has been authenticated
        /// </summary>
        public bool IsAuthenticated { get; set; }

        /// <summary>
        /// Whether the player is currently spawned in the game
        /// </summary>
        public bool IsSpawned => PlayerInstance != null && PlayerInstance.gameObject != null;

        /// <summary>
        /// How long the player has been connected
        /// </summary>
        public TimeSpan ConnectionDuration => DateTime.Now - ConnectTime;

        /// <summary>
        /// A display name for the player (prioritizes PlayerName, falls back to ClientId)
        /// </summary>
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(PlayerName))
                    return PlayerName;
                if (!string.IsNullOrEmpty(SteamId))
                    return $"Player_{SteamId.Substring(Math.Max(0, SteamId.Length - 6))}";
                return $"Client_{ClientId}";
            }
        }

        /// <summary>
        /// A unique identifier for the player (SteamId if available, otherwise ClientId)
        /// </summary>
        public string UniqueId => !string.IsNullOrEmpty(SteamId) ? SteamId : ClientId.ToString();

        /// <summary>
        /// Whether the player has identity information (SteamId and name)
        /// </summary>
        public bool HasIdentity => !string.IsNullOrEmpty(SteamId) && !string.IsNullOrEmpty(PlayerName);

        /// <summary>
        /// Get a detailed string representation of the player
        /// </summary>
        public override string ToString()
        {
            var status = IsSpawned ? "Spawned" : "Connected";
            var auth = IsAuthenticated ? "Auth" : "NoAuth";
            var duration = ConnectionDuration.ToString(@"mm\:ss");
            
            return $"{DisplayName} (ID: {ClientId}, Steam: {SteamId ?? "N/A"}, {status}, {auth}, {duration})";
        }

        /// <summary>
        /// Get a brief string representation of the player
        /// </summary>
        public string ToShortString()
        {
            return $"{DisplayName} (ID: {ClientId})";
        }

        /// <summary>
        /// Check if this player matches the given identifier (name, steamid, or clientid)
        /// </summary>
        public bool MatchesIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return false;

            identifier = identifier.Trim();

            // Check exact matches first
            if (PlayerName?.Equals(identifier, StringComparison.OrdinalIgnoreCase) == true)
                return true;
            if (SteamId?.Equals(identifier, StringComparison.OrdinalIgnoreCase) == true)
                return true;
            if (ClientId.ToString().Equals(identifier, StringComparison.OrdinalIgnoreCase))
                return true;

            // Check partial name matches
            if (PlayerName?.Contains(identifier, StringComparison.OrdinalIgnoreCase) == true)
                return true;

            return false;
        }
    }
}
