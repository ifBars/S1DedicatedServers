using System;
using Newtonsoft.Json;

namespace DedicatedServerMod.Server.Network
{
    /// <summary>
    /// Data model for server heartbeat payload sent to the master server
    /// </summary>
    [Serializable]
    public class ServerHeartbeatData
    {
        [JsonProperty("apiKey")]
        public string ApiKey { get; set; }

        [JsonProperty("serverName")]
        public string ServerName { get; set; }

        [JsonProperty("serverDescription")]
        public string ServerDescription { get; set; }

        [JsonProperty("currentPlayers")]
        public int CurrentPlayers { get; set; }

        [JsonProperty("maxPlayers")]
        public int MaxPlayers { get; set; }

        [JsonProperty("port")]
        public int Port { get; set; }

        [JsonProperty("publicAddress")]
        public string PublicAddress { get; set; }

        [JsonProperty("passwordProtected")]
        public bool PasswordProtected { get; set; }

        [JsonProperty("gameVersion")]
        public string GameVersion { get; set; }

        [JsonProperty("mapName")]
        public string MapName { get; set; }

        [JsonProperty("modVersion")]
        public string ModVersion { get; set; }
    }

    /// <summary>
    /// Registration request model
    /// </summary>
    [Serializable]
    public class RegisterServerRequest
    {
        [JsonProperty("serverName")]
        public string ServerName { get; set; }

        [JsonProperty("ownerContact")]
        public string OwnerContact { get; set; }
    }

    /// <summary>
    /// Registration response model
    /// </summary>
    [Serializable]
    public class RegisterServerResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("serverId")]
        public string ServerId { get; set; }

        [JsonProperty("apiKey")]
        public string ApiKey { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    /// <summary>
    /// Heartbeat response model
    /// </summary>
    [Serializable]
    public class HeartbeatResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("detectedIp")]
        public string DetectedIp { get; set; }
    }

    /// <summary>
    /// Generic API error response
    /// </summary>
    [Serializable]
    public class ApiErrorResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }
}

