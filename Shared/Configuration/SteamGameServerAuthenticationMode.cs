namespace DedicatedServerMod.Shared.Configuration
{
    /// <summary>
    /// Authentication mode configuration for Steam game server login.
    /// </summary>
    public enum SteamGameServerAuthenticationMode
    {
        /// <summary>
        /// Do not authenticate user logins and do not list in server browser.
        /// </summary>
        NoAuthentication,

        /// <summary>
        /// Authenticate users and list in server browser.
        /// </summary>
        Authentication,

        /// <summary>
        /// Authenticate users, list in server browser, and enable secure mode.
        /// </summary>
        AuthenticationAndSecure
    }
}
