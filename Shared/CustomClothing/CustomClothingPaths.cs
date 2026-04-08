using MelonLoader.Utils;

namespace DedicatedServerMod.Shared.CustomClothing
{
    internal static class CustomClothingPaths
    {
        public static string AuthoringRoot =>
            Path.Combine(MelonEnvironment.UserDataDirectory, "CustomClothing");

        public static string CacheRoot =>
            Path.Combine(MelonEnvironment.UserDataDirectory, "DedicatedServerMod", "CustomClothingCache");

        public static string NormalizeResourcePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return path.Replace('\\', '/').Trim('/');
        }

        public static string BuildRuntimeAssetPath(string canonicalTemplatePath, string variantSlug)
        {
            return $"DedicatedServerMod/CustomClothing/{NormalizeResourcePath(canonicalTemplatePath)}/{variantSlug}".Trim('/');
        }

        public static string BuildServerCacheDirectory(string host, int port, string manifestHash)
        {
            string endpointHash = CustomClothingHashUtility.ComputeSha256(System.Text.Encoding.UTF8.GetBytes($"{host}:{port}".ToLowerInvariant()));
            return Path.Combine(
                CacheRoot,
                ShortenHash(endpointHash),
                ShortenHash(manifestHash));
        }

        public static string ShortenHash(string hash, int length = 16)
        {
            if (string.IsNullOrWhiteSpace(hash))
            {
                return "default";
            }

            string normalized = hash.Trim().ToLowerInvariant();
            return normalized.Length <= length
                ? normalized
                : normalized.Substring(0, length);
        }
    }
}
