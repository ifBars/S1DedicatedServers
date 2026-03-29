using System;
using System.IO;
using MelonLoader.Utils;

namespace DedicatedServerMod.API.Configuration
{
    /// <summary>
    /// Provides standard configuration paths for server addons.
    /// </summary>
    public static class ModConfigPaths
    {
        /// <summary>
        /// Gets the default configuration path for an addon.
        /// </summary>
        /// <param name="modId">The addon identifier.</param>
        /// <returns>The default TOML path for the addon.</returns>
        public static string GetDefault(string modId)
        {
            return GetPath(modId, "config.toml");
        }

        /// <summary>
        /// Gets a configuration path for an addon-scoped filename.
        /// </summary>
        /// <param name="modId">The addon identifier.</param>
        /// <param name="fileName">The addon-scoped file name.</param>
        /// <returns>The resolved TOML path.</returns>
        public static string GetPath(string modId, string fileName)
        {
            if (string.IsNullOrWhiteSpace(modId))
            {
                throw new ArgumentException("Mod identifier cannot be null or whitespace.", nameof(modId));
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name cannot be null or whitespace.", nameof(fileName));
            }

            return Path.Combine(
                MelonEnvironment.UserDataDirectory,
                "DedicatedServerMod",
                "Mods",
                modId.Trim(),
                fileName.Trim());
        }
    }
}
