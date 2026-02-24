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
        /// Steam Web API validation using AuthenticateUserTicket.
        /// </summary>
        SteamWebApi,

        /// <summary>
        /// Steam game server API validation using BeginAuthSession callbacks.
        /// </summary>
        SteamGameServer
    }
}
