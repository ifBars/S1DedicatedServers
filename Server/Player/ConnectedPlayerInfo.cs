#if IL2CPP
using Il2CppFishNet;
using Il2CppFishNet.Connection;
using Il2CppScheduleOne.PlayerScripts;
#else
using FishNet;
using FishNet.Connection;
#endif
using DedicatedServerMod.Utils;

namespace DedicatedServerMod.Server.Player
{
    /// <summary>
    /// Information about a connected player including connection details,
    /// identity information, and status.
    /// </summary>
    public sealed class ConnectedPlayerInfo
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
        /// When the player connected, in UTC.
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
        /// Whether authentication is currently pending for this connection.
        /// </summary>
        public bool IsAuthenticationPending { get; set; }

        /// <summary>
        /// The most recent challenge nonce issued by the server.
        /// </summary>
        public string AuthenticationNonce { get; set; }

        /// <summary>
        /// UTC timestamp when the current authentication attempt started.
        /// </summary>
        public DateTime? AuthStartedAtUtc { get; set; }

        /// <summary>
        /// UTC timestamp of the most recent authentication completion.
        /// </summary>
        public DateTime? LastAuthenticationAttemptUtc { get; set; }

        /// <summary>
        /// Last authentication status message produced for this player.
        /// </summary>
        public string LastAuthenticationMessage { get; set; }

        /// <summary>
        /// SteamID verified by the authentication backend.
        /// </summary>
        public string AuthenticatedSteamId { get; set; }

        /// <summary>
        /// Whether this connection is the local loopback host connection.
        /// </summary>
        public bool IsLoopbackConnection { get; set; }

        /// <summary>
        /// Whether join lifecycle notifications have been emitted for this player.
        /// </summary>
        public bool HasCompletedJoinFlow { get; set; }

        /// <summary>
        /// Whether client mod verification has completed successfully for this player.
        /// </summary>
        public bool IsModVerificationComplete { get; set; }

        /// <summary>
        /// Whether client mod verification is currently pending for this player.
        /// </summary>
        public bool IsModVerificationPending { get; set; }

        /// <summary>
        /// The most recent mod verification challenge nonce issued by the server.
        /// </summary>
        public string ModVerificationNonce { get; set; }

        /// <summary>
        /// UTC timestamp when the current mod verification attempt started.
        /// </summary>
        public DateTime? ModVerificationStartedAtUtc { get; set; }

        /// <summary>
        /// UTC timestamp when the most recent mod verification attempt completed.
        /// </summary>
        public DateTime? LastModVerificationAttemptUtc { get; set; }

        /// <summary>
        /// Last mod verification status message produced for this player.
        /// </summary>
        public string LastModVerificationMessage { get; set; }

        /// <summary>
        /// Whether disconnect cleanup has already been processed for this tracked player entry.
        /// </summary>
        public bool IsDisconnectProcessed { get; set; }

        /// <summary>
        /// Whether the player is currently connected to the server
        /// </summary>
        public bool IsConnected
        {
            get
            {
                if (Connection == null)
                {
                    return false;
                }

                if (Connection.IsActive)
                {
                    return true;
                }

                return InstanceFinder.ServerManager?.Clients != null &&
                       InstanceFinder.ServerManager.Clients.TryGetValue(ClientId, out NetworkConnection trackedConnection) &&
                       trackedConnection != null &&
                       trackedConnection.IsActive;
            }
        }

        /// <summary>
        /// Whether the player is currently spawned in the game
        /// </summary>
        public bool IsSpawned => PlayerInstance != null;

        /// <summary>
        /// How long the player has been connected
        /// </summary>
        public TimeSpan ConnectionDuration => DateTime.UtcNow - ConnectTime;

        /// <summary>
        /// A display name for the player (prioritizes PlayerName, falls back to ClientId)
        /// </summary>
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(PlayerName))
                {
                    if (IsLoopbackConnection && string.Equals(PlayerName, "Player", StringComparison.Ordinal))
                        return Constants.GhostHostDisplayName;

                    return PlayerName;
                }

                if (IsLoopbackConnection)
                    return Constants.GhostHostDisplayName;

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
        /// Trusted unique identifier for the player.
        /// Uses backend-verified SteamID when available.
        /// </summary>
        public string TrustedUniqueId =>
            !string.IsNullOrEmpty(AuthenticatedSteamId)
                ? AuthenticatedSteamId
                : UniqueId;

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
            var auth = IsAuthenticated ? "Auth" : (IsAuthenticationPending ? "AuthPending" : "NoAuth");
            var modVerification = IsModVerificationComplete ? "ModVerified" : (IsModVerificationPending ? "ModVerifyPending" : "NoModVerify");
            var duration = ConnectionDuration.ToString(@"mm\:ss");
            
            return $"{DisplayName} (ID: {ClientId}, Steam: {SteamId ?? "N/A"}, {status}, {auth}, {modVerification}, {duration})";
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
