using System;

namespace DedicatedServerMod.Client.Data
{
    /// <summary>
    /// Represents a saved dedicated server entry used by the client UI.
    /// </summary>
    internal sealed class SavedServerEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Name { get; set; } = string.Empty;

        public string ServerName { get; set; } = string.Empty;

        public string Host { get; set; } = "localhost";

        public int Port { get; set; } = 38465;

        public string ServerDescription { get; set; } = string.Empty;

        public int PingMilliseconds { get; set; } = -1;

        public int CurrentPlayers { get; set; }

        public int MaxPlayers { get; set; }

        public DateTime LastMetadataRefreshUtc { get; set; } = DateTime.MinValue;

        public DateTime LastJoinedUtc { get; set; } = DateTime.UtcNow;
    }
}
