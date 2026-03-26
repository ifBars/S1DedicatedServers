using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DedicatedServerMod.Shared
{
    /// <summary>
    /// Represents a single player entry in a <see cref="PlayerListSnapshot"/>.
    /// </summary>
    [Serializable]
    public sealed class PlayerListEntry
    {
        /// <summary>
        /// The player's display name as shown in-game.
        /// </summary>
        [JsonProperty("name")]
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Round-trip time in milliseconds as reported by the client.
        /// <c>-1</c> means the value has not yet been received from the client.
        /// </summary>
        [JsonProperty("ping")]
        public int PingMs { get; set; } = -1;
    }

    /// <summary>
    /// A point-in-time snapshot of all visible connected players and their measured pings.
    /// Serialised to JSON and broadcast from server to every client once per second via
    /// <see cref="Utils.Constants.Messages.PlayerListUpdate"/>.
    /// The ghost-host loopback player is excluded.
    /// </summary>
    [Serializable]
    public sealed class PlayerListSnapshot
    {
        /// <summary>
        /// All visible (non-loopback) connected players, in the order they appear in
        /// <c>PlayerManager.GetConnectedPlayers()</c>.
        /// </summary>
        [JsonProperty("players")]
        public List<PlayerListEntry> Players { get; set; } = new List<PlayerListEntry>();
    }
}
