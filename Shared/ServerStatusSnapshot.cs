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
        /// Gets or sets the public-directory protocol version implemented by this endpoint.
        /// </summary>
        /// <remarks>
        /// Public-directory clients accept the snapshot only when this value equals
        /// <see cref="PublicServerListProtocol.Version"/>. Direct-connect status queries may leave it at zero.
        /// </remarks>
        /// <example>
        /// <code>snapshot.ProtocolVersion = PublicServerListProtocol.Version;</code>
        /// </example>
        public int ProtocolVersion { get; set; }

        /// <summary>
        /// Gets or sets the account-issued public listing identity bound to this server.
        /// </summary>
        /// <remarks>
        /// For a public-directory verification, this value must be the listing UUID expected by
        /// the client. Direct-connect status queries may use an empty value.
        /// </remarks>
        /// <example>
        /// <code>snapshot.ListingId = "00000000-0000-0000-0000-000000000001";</code>
        /// </example>
        public string ListingId { get; set; } = string.Empty;

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
