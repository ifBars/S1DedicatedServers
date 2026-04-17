namespace DedicatedServerMod.API.Metadata
{
    /// <summary>
    /// Describes a loaded client-side mod in the local runtime.
    /// </summary>
    /// <remarks>
    /// This metadata is intended for addon introspection and diagnostics. Use
    /// <see cref="ModManager.ClientMods"/> when you need the registered <see cref="IClientMod"/>
    /// lifecycle instances, and use <see cref="ModManager.ClientModMetadata"/> when you need
    /// stable descriptive information such as display name, version, author, or declared mod ID.
    /// </remarks>
    [Serializable]
    public sealed class ClientModMetadata
    {
        /// <summary>
        /// Gets or sets the stable mod identifier declared by
        /// <see cref="S1DSClientModIdentityAttribute"/>, when present.
        /// </summary>
        public string ModId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the human-readable display name for the mod.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the client mod version string.
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the mod author string when available.
        /// </summary>
        public string Author { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the assembly file name or simple assembly name for the mod.
        /// </summary>
        public string AssemblyName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the mod explicitly declared
        /// <see cref="S1DSClientModIdentityAttribute"/>.
        /// </summary>
        public bool IdentityDeclared { get; set; }
    }
}
