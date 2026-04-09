namespace DedicatedServerMod.Server.Player
{
    /// <summary>
    /// Player statistics information.
    /// </summary>
    public sealed class PlayerStats
    {
        /// <summary>
        /// Gets or sets the number of currently connected players.
        /// </summary>
        public int ConnectedPlayers { get; set; }

        /// <summary>
        /// Gets or sets the configured maximum player count.
        /// </summary>
        public int MaxPlayers { get; set; }

        /// <summary>
        /// Gets or sets the number of ban entries currently stored by the server.
        /// </summary>
        public int TotalBannedPlayers { get; set; }

        /// <summary>
        /// Gets or sets the current connected-player snapshot.
        /// </summary>
        public IReadOnlyList<ConnectedPlayerInfo> Players { get; set; } = Array.Empty<ConnectedPlayerInfo>();

        public override string ToString()
        {
            return $"Players: {ConnectedPlayers}/{MaxPlayers} | Banned: {TotalBannedPlayers}";
        }
    }
}
