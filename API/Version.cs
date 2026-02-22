using System.Runtime.CompilerServices;

namespace DedicatedServerMod.API
{
    /// <summary>
    /// Provides version information for DedicatedServerMod.
    /// This class serves as the authoritative source for all version-related data.
    /// </summary>
    /// <remarks>
    /// Version Format: {MOD_VERSION} (API {API_VERSION})
    /// Example: "0.2.1-beta (API 0.2.0)"
    /// 
    /// Version History:
    /// - 0.1.x: Initial alpha releases with basic server functionality
    /// - 0.2.x: Beta releases with full admin system, TCP console, and mod API
    /// </remarks>
    public static class Version
    {
        #region Version Numbers

        /// <summary>
        /// The mod version following semantic versioning (MAJOR.MINOR.PATCH with prerelease).
        /// </summary>
        public const string ModVersion = "0.2.1-beta";

        /// <summary>
        /// The major version number for breaking change tracking.
        /// </summary>
        public const int MajorVersion = 0;

        /// <summary>
        /// The minor version number for feature additions.
        /// </summary>
        public const int MinorVersion = 2;

        /// <summary>
        /// The patch version number for bug fixes.
        /// </summary>
        public const int PatchVersion = 1;

        /// <summary>
        /// The API version for compatibility checking between mods and core.
        /// </summary>
        public const string APIVersion = "0.2.0";

        /// <summary>
        /// The major API version number.
        /// </summary>
        public const int APIMajorVersion = 0;

        /// <summary>
        /// The minor API version number.
        /// </summary>
        public const int APIMinorVersion = 2;

        #endregion

        #region Version Information

        /// <summary>
        /// Gets the full version string including mod and API versions.
        /// </summary>
        /// <returns>A string in format: "MOD_VERSION (API API_VERSION)"</returns>
        public static string FullVersion => $"{ModVersion} (API {APIVersion})";

        /// <summary>
        /// Gets the version string for assembly metadata.
        /// </summary>
        public static string AssemblyVersion => ModVersion;

        /// <summary>
        /// Gets the informational version string.
        /// </summary>
        public static string InformationalVersion => $"{ModVersion}+api{APIVersion}";

        #endregion

        #region Compatibility

        /// <summary>
        /// Checks if this version is compatible with the given API version.
        /// </summary>
        /// <param name="apiVersion">The API version to check compatibility with</param>
        /// <returns>True if the versions are compatible (same major API version)</returns>
        public static bool IsCompatibleWithApi(string apiVersion)
        {
            if (string.IsNullOrEmpty(apiVersion))
                return false;

            // Extract major version from API version string
            var parts = apiVersion.Split('.');
            if (parts.Length < 1 || !int.TryParse(parts[0], out int major))
                return false;

            // Same major version = compatible
            return major == APIMajorVersion;
        }

        /// <summary>
        /// Checks if this is a prerelease version.
        /// </summary>
        /// <returns>True if the version contains a prerelease tag (e.g., "-beta")</returns>
        public static bool IsPrerelease => ModVersion.Contains("-");

        /// <summary>
        /// Gets the prerelease tag if this is a prerelease version.
        /// </summary>
        /// <returns>The prerelease tag (e.g., "beta"), or null if not a prerelease</returns>
        public static string PrereleaseTag
        {
            get
            {
                var index = ModVersion.IndexOf('-');
                return index >= 0 ? ModVersion.Substring(index + 1) : null;
            }
        }

        #endregion

        #region Version Parsing

        /// <summary>
        /// Parses a version string into its component parts.
        /// </summary>
        /// <param name="versionString">The version string to parse</param>
        /// <returns>A tuple containing (major, minor, patch, prerelease)</returns>
        public static (int major, int minor, int patch, string prerelease) ParseVersion(string versionString)
        {
            if (string.IsNullOrEmpty(versionString))
                return (0, 0, 0, null);

            // Remove any prefix like "v" or "version"
            var cleaned = versionString.Trim().ToLower()
                .Replace("v", "")
                .Replace("version ", "")
                .Replace("api ", "");

            // Split by hyphen for prerelease
            var hyphenIndex = cleaned.IndexOf('-');
            var coreVersion = hyphenIndex >= 0 ? cleaned.Substring(0, hyphenIndex) : cleaned;
            var prerelease = hyphenIndex >= 0 ? cleaned.Substring(hyphenIndex + 1) : null;

            // Parse core version
            var parts = coreVersion.Split('.');
            var major = parts.Length > 0 && int.TryParse(parts[0], out var m) ? m : 0;
            var minor = parts.Length > 1 && int.TryParse(parts[1], out var mi) ? mi : 0;
            var patch = parts.Length > 2 && int.TryParse(parts[2], out var p) ? p : 0;

            return (major, minor, patch, prerelease);
        }

        #endregion

        #region Version Comparison

        /// <summary>
        /// Compares this version with another version string.
        /// </summary>
        /// <param name="otherVersion">The other version string to compare</param>
        /// <returns>
        /// -1 if this version is less than otherVersion
        /// 0 if equal
        /// 1 if this version is greater than otherVersion
        /// </returns>
        public static int CompareTo(string otherVersion)
        {
            var (major, minor, patch, _) = ParseVersion(otherVersion);
            var (ourMajor, ourMinor, ourPatch, _) = ParseVersion(ModVersion);

            if (ourMajor != major) return ourMajor > major ? 1 : -1;
            if (ourMinor != minor) return ourMinor > minor ? 1 : -1;
            if (ourPatch != patch) return ourPatch > patch ? 1 : -1;
            return 0;
        }

        #endregion

        #region Release Information

        /// <summary>
        /// Checks if this is a stable release (not alpha, beta, or RC).
        /// </summary>
        /// <returns>True if this is a stable release</returns>
        public static bool IsStableRelease
        {
            get
            {
                var tag = PrereleaseTag;
                if (string.IsNullOrEmpty(tag))
                    return true;

                // Not stable if contains alpha, beta, rc, dev, etc.
                return !tag.Contains("alpha") &&
                       !tag.Contains("beta") &&
                       !tag.Contains("rc") &&
                       !tag.Contains("dev") &&
                       !tag.Contains("nightly");
            }
        }

        /// <summary>
        /// Gets the release channel based on the prerelease tag.
        /// </summary>
        /// <returns>The release channel: "stable", "beta", "alpha", or "dev"</returns>
        public static string ReleaseChannel
        {
            get
            {
                var tag = PrereleaseTag;
                if (string.IsNullOrEmpty(tag))
                    return "stable";

                if (tag.Contains("alpha") || tag.Contains("dev") || tag.Contains("nightly"))
                    return "alpha";

                if (tag.Contains("beta"))
                    return "beta";

                if (tag.Contains("rc"))
                    return "release-candidate";

                return "unknown";
            }
        }

        #endregion

        #region Formatting

        /// <summary>
        /// Gets a short version string suitable for display.
        /// </summary>
        /// <returns>The mod version without API version</returns>
        public static string ShortVersion => ModVersion;

        /// <summary>
        /// Gets a detailed version string for logging.
        /// </summary>
        /// <returns>A detailed version string</returns>
        public static string DetailedVersion => $"DedicatedServerMod v{ModVersion} (API v{APIVersion})";

        /// <summary>
        /// Gets a one-line version summary for console output.
        /// </summary>
        /// <returns>A formatted version summary</returns>
        public static string Summary => $"DedicatedServerMod {FullVersion}";

        #endregion

        #region Metadata

        /// <summary>
        /// The release date of this version (format: YYYY-MM-DD).
        /// Update this when releasing new versions.
        /// </summary>
        public const string ReleaseDate = "2024-XX-XX";

        /// <summary>
        /// The Git commit or tag this version was built from.
        /// </summary>
        public const string BuildSource = "development";

        /// <summary>
        /// The build number for CI/CD tracking.
        /// </summary>
        public const int BuildNumber = 0;

        #endregion
    }
}
