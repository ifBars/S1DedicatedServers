namespace DedicatedServerMod.Shared
{
    /// <summary>
    /// Represents lightweight server browser metadata returned by the status query endpoint.
    /// </summary>
    /// <remarks>
    /// This snapshot is safe for unauthenticated discovery queries. Keep it limited to public
    /// status data needed by the client server browser.
    /// </remarks>
    [Serializable]
    public sealed class ServerStatusSnapshot
    {
        /// <summary>
        /// Gets or sets the public server display name.
        /// </summary>
        public string ServerName { get; set; } = "Schedule One Dedicated Server";

        /// <summary>
        /// Gets or sets the public server description.
        /// </summary>
        public string ServerDescription { get; set; } = "A dedicated server for Schedule One";

        /// <summary>
        /// Gets or sets the current connected-player count.
        /// </summary>
        public int CurrentPlayers { get; set; }

        /// <summary>
        /// Gets or sets the maximum supported player count.
        /// </summary>
        public int MaxPlayers { get; set; }
    }
}
