using Newtonsoft.Json;

namespace DedicatedServerMod.Shared
{
    internal static class PublicServerListProtocol
    {
        internal const int Version = 2;
    }

    internal sealed class PublicListingRegistrationResponse
    {
        [JsonProperty("success")]
        internal bool Success { get; set; }

        [JsonProperty("listingId")]
        internal string ListingId { get; set; } = string.Empty;

        [JsonProperty("secret")]
        internal string Secret { get; set; } = string.Empty;
    }

    internal sealed class PublicListingHeartbeatRequest
    {
        [JsonProperty("protocolVersion")]
        internal int ProtocolVersion { get; set; } = PublicServerListProtocol.Version;

        [JsonProperty("serverName")]
        internal string ServerName { get; set; } = string.Empty;

        [JsonProperty("serverDescription")]
        internal string ServerDescription { get; set; } = string.Empty;

        [JsonProperty("currentPlayers")]
        internal int CurrentPlayers { get; set; }

        [JsonProperty("maxPlayers")]
        internal int MaxPlayers { get; set; }

        [JsonProperty("port")]
        internal int Port { get; set; }

        [JsonProperty("passwordProtected")]
        internal bool PasswordProtected { get; set; }

        [JsonProperty("gameVersion")]
        internal string GameVersion { get; set; } = string.Empty;

        [JsonProperty("modVersion")]
        internal string ModVersion { get; set; } = string.Empty;
    }

    internal sealed class PublicServerDirectoryEntry
    {
        [JsonProperty("listingId")]
        internal string ListingId { get; set; } = string.Empty;

        [JsonProperty("protocolVersion")]
        internal int ProtocolVersion { get; set; }

        [JsonProperty("serverName")]
        internal string ServerName { get; set; } = string.Empty;

        [JsonProperty("serverDescription")]
        internal string ServerDescription { get; set; } = string.Empty;

        [JsonProperty("currentPlayers")]
        internal int CurrentPlayers { get; set; }

        [JsonProperty("maxPlayers")]
        internal int MaxPlayers { get; set; }

        [JsonProperty("port")]
        internal int Port { get; set; }

        [JsonProperty("host")]
        internal string Host { get; set; } = string.Empty;

        [JsonProperty("passwordProtected")]
        internal bool PasswordProtected { get; set; }

        [JsonProperty("gameVersion")]
        internal string GameVersion { get; set; } = string.Empty;

        [JsonProperty("modVersion")]
        internal string ModVersion { get; set; } = string.Empty;

        [JsonProperty("lastHeartbeat")]
        internal long LastHeartbeat { get; set; }
    }

    internal sealed class PublicServerListResponse
    {
        [JsonProperty("success")]
        internal bool Success { get; set; }

        [JsonProperty("servers")]
        internal List<PublicServerDirectoryEntry> Servers { get; set; } = new List<PublicServerDirectoryEntry>();

        [JsonProperty("nextCursor")]
        internal string NextCursor { get; set; }
    }
}
