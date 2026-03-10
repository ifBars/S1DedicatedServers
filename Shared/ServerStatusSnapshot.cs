using System;

namespace DedicatedServerMod.Shared
{
    /// <summary>
    /// Represents lightweight server browser metadata returned by the status query endpoint.
    /// </summary>
    [Serializable]
    public sealed class ServerStatusSnapshot
    {
        public string ServerName { get; set; } = "Schedule One Dedicated Server";

        public string ServerDescription { get; set; } = "A dedicated server for Schedule One";

        public int CurrentPlayers { get; set; }

        public int MaxPlayers { get; set; }
    }
}
