using System;
using Newtonsoft.Json;

namespace DedicatedServerMod.Shared.Networking
{
    /// <summary>
    /// Authentication challenge sent from server to client when password is required.
    /// </summary>
    [Serializable]
    public class AuthenticationChallengeMessage
    {
        /// <summary>
        /// Indicates whether the server requires password authentication.
        /// </summary>
        [JsonProperty("requiresPassword")]
        public bool RequiresPassword { get; set; }

        /// <summary>
        /// Server name for display purposes.
        /// </summary>
        [JsonProperty("serverName")]
        public string ServerName { get; set; }
    }

    /// <summary>
    /// Authentication response sent from client to server containing password hash.
    /// </summary>
    [Serializable]
    public class AuthenticationResponseMessage
    {
        /// <summary>
        /// SHA256 hash of the password entered by the client.
        /// </summary>
        [JsonProperty("passwordHash")]
        public string PasswordHash { get; set; }

        /// <summary>
        /// Client version for compatibility checking (optional).
        /// </summary>
        [JsonProperty("clientVersion")]
        public string ClientVersion { get; set; }
    }

    /// <summary>
    /// Authentication result sent from server to client after validation.
    /// </summary>
    [Serializable]
    public class AuthenticationResultMessage
    {
        /// <summary>
        /// Whether authentication was successful.
        /// </summary>
        [JsonProperty("success")]
        public bool Success { get; set; }

        /// <summary>
        /// Error message to display to the user if authentication failed.
        /// </summary>
        [JsonProperty("errorMessage")]
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Server information message sent to clients (extended with password protection flag).
    /// </summary>
    [Serializable]
    public class ServerInfoMessage
    {
        /// <summary>
        /// Server name.
        /// </summary>
        [JsonProperty("serverName")]
        public string ServerName { get; set; }

        /// <summary>
        /// Whether the server is password protected.
        /// </summary>
        [JsonProperty("isPasswordProtected")]
        public bool IsPasswordProtected { get; set; }

        /// <summary>
        /// Current number of players.
        /// </summary>
        [JsonProperty("currentPlayers")]
        public int CurrentPlayers { get; set; }

        /// <summary>
        /// Maximum number of players.
        /// </summary>
        [JsonProperty("maxPlayers")]
        public int MaxPlayers { get; set; }

        /// <summary>
        /// Server description.
        /// </summary>
        [JsonProperty("description")]
        public string Description { get; set; }
    }
}
