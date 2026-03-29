using System;

namespace DedicatedServerMod.API
{
    /// <summary>
    /// Declares the stable identity of a client-side mod assembly for server compatibility checks.
    /// </summary>
    /// <remarks>
    /// Apply this attribute at the assembly level from the client-side mod project. DedicatedServerMod
    /// reads this metadata during the join verification handshake to match paired client companions
    /// against server-side requirements.
    /// <para>
    /// Pair this with <see cref="S1DSClientCompanionAttribute"/> on the corresponding server mod
    /// assembly. Unpaired client-only mods can also declare this attribute so servers can identify
    /// and deny or approve them by a stable ID instead of only by file name.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [assembly: S1DSClientModIdentity("ghost.mycoolmod", "1.2.3")]
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public sealed class S1DSClientModIdentityAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new client mod identity declaration.
        /// </summary>
        /// <param name="modId">Stable mod identifier used for companion matching.</param>
        /// <param name="version">Client mod version string.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="modId"/> or <paramref name="version"/> is empty.</exception>
        public S1DSClientModIdentityAttribute(string modId, string version)
        {
            if (string.IsNullOrWhiteSpace(modId))
            {
                throw new ArgumentException("Client mod identity modId cannot be empty.", nameof(modId));
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException("Client mod identity version cannot be empty.", nameof(version));
            }

            ModId = modId.Trim();
            Version = version.Trim();
        }

        /// <summary>
        /// Gets the stable mod identifier for the client mod.
        /// </summary>
        public string ModId { get; }

        /// <summary>
        /// Gets the client mod version string.
        /// </summary>
        public string Version { get; }
    }
}
