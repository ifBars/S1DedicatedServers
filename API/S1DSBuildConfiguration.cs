namespace DedicatedServerMod.API
{
    /// <summary>
    /// Describes the compile-time DedicatedServerMod build surface exposed by the current assembly.
    /// </summary>
    public enum S1DSBuildConfiguration
    {
        /// <summary>
        /// The current assembly did not define a recognized side-aware build configuration.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The current assembly exposes the server-side API surface.
        /// </summary>
        Server = 1,

        /// <summary>
        /// The current assembly exposes the client-side API surface.
        /// </summary>
        Client = 2,

        /// <summary>
        /// The current assembly exposes both server and client API surfaces, such as the documentation build.
        /// </summary>
        ServerClient = 3
    }
}
