namespace DedicatedServerMod.Shared.Configuration
{
    /// <summary>
    /// Authentication providers available for dedicated server client validation.
    /// </summary>
    public enum AuthenticationProvider
    {
        /// <summary>
        /// No authentication provider. Players are accepted without Steam ticket checks.
        /// </summary>
        None,

        /// <summary>
        /// Deprecated compatibility value for the incomplete Steam Web API ticket path.
        /// Use <see cref="SteamGameServer"/> instead.
        /// </summary>
        [System.Obsolete("SteamWebApi authentication is deprecated and is normalized to SteamGameServer. Use SteamGameServer instead.", false)]
        SteamWebApi,

        /// <summary>
        /// Steam game server API validation using BeginAuthSession callbacks.
        /// </summary>
        SteamGameServer
    }
}
