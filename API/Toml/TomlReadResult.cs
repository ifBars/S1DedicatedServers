using System;
using System.Collections.Generic;
using System.Linq;

namespace DedicatedServerMod.API.Toml
{
    /// <summary>
    /// Contains the parsed TOML document and any non-fatal diagnostics.
    /// </summary>
    public sealed class TomlReadResult
    {
        /// <summary>
        /// Initializes a new TOML read result.
        /// </summary>
        public TomlReadResult(TomlDocument document, IEnumerable<TomlDiagnostic> diagnostics)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            Diagnostics = (diagnostics ?? Enumerable.Empty<TomlDiagnostic>()).ToList().AsReadOnly();
        }

        /// <summary>
        /// Gets the parsed TOML document.
        /// </summary>
        public TomlDocument Document { get; }

        /// <summary>
        /// Gets the non-fatal parser diagnostics.
        /// </summary>
        public IReadOnlyList<TomlDiagnostic> Diagnostics { get; }
    }
}
