using System;

namespace DedicatedServerMod.API
{
    /// <summary>
    /// Declares the dedicated-server client companion expected by a server-side mod assembly.
    /// </summary>
    /// <remarks>
    /// Apply this attribute at the assembly level from the server-side mod project so
    /// DedicatedServerMod can derive required or optional client companion policy automatically.
    /// The <see cref="ModId"/> should match the value declared by the companion client mod's
    /// <see cref="S1DSClientModIdentityAttribute"/>.
    /// <para>
    /// In the default verification mode, DedicatedServerMod matches companion mods by
    /// <see cref="ModId"/> and <see cref="MinVersion"/>. <see cref="PinnedSha256"/> is only
    /// intended for servers that enable strict client mod mode.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [assembly: S1DSClientCompanion(
    ///     modId: "ghost.mycoolmod",
    ///     displayName: "My Cool Mod",
    ///     Required = true,
    ///     MinVersion = "1.2.0")]
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
    public sealed class S1DSClientCompanionAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new client companion declaration.
        /// </summary>
        /// <param name="modId">Stable mod identifier shared by the server and client companion.</param>
        /// <param name="displayName">Human-readable mod name shown in verification messages.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="modId"/> or <paramref name="displayName"/> is empty.</exception>
        public S1DSClientCompanionAttribute(string modId, string displayName)
        {
            if (string.IsNullOrWhiteSpace(modId))
            {
                throw new ArgumentException("Client companion modId cannot be empty.", nameof(modId));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("Client companion displayName cannot be empty.", nameof(displayName));
            }

            ModId = modId.Trim();
            DisplayName = displayName.Trim();
        }

        /// <summary>
        /// Gets the stable mod identifier expected on the client companion.
        /// </summary>
        public string ModId { get; }

        /// <summary>
        /// Gets the human-readable mod name shown to users.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets or sets a value indicating whether this client companion is required.
        /// </summary>
        /// <remarks>
        /// When <see langword="true"/>, clients must load a matching companion mod to join.
        /// When <see langword="false"/>, the companion is optional, but if present it must still
        /// satisfy the configured version and strict-mode rules.
        /// </remarks>
        public bool Required { get; set; } = true;

        /// <summary>
        /// Gets or sets the minimum compatible client companion version.
        /// Empty string disables version floor validation.
        /// </summary>
        /// <remarks>
        /// Versions are compared using DedicatedServerMod's lenient semantic-version comparer.
        /// This is the normal compatibility gate for paired mods and is preferred over per-build
        /// hash pinning for everyday development.
        /// </remarks>
        public string MinVersion { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the exact SHA-256 hashes accepted for this companion in strict mode.
        /// </summary>
        /// <remarks>
        /// Leave this empty for normal compatibility-first servers. Only populate this when you
        /// specifically want strict client mod mode to pin exact companion binaries.
        /// </remarks>
        public string[] PinnedSha256 { get; set; } = Array.Empty<string>();
    }
}
