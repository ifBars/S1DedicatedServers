namespace DedicatedServerMod.API.Toml
{
    /// <summary>
    /// Supported TOML value kinds for the framework's constrained TOML subset.
    /// </summary>
    public enum TomlValueKind
    {
        /// <summary>
        /// A TOML string value.
        /// </summary>
        String,

        /// <summary>
        /// A TOML boolean value.
        /// </summary>
        Boolean,

        /// <summary>
        /// A TOML integer value.
        /// </summary>
        Integer,

        /// <summary>
        /// A TOML floating-point value.
        /// </summary>
        Float,

        /// <summary>
        /// A TOML array value.
        /// </summary>
        Array,

        /// <summary>
        /// A TOML inline table value.
        /// </summary>
        InlineTable
    }
}
