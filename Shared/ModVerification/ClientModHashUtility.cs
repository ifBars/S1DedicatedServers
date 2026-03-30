using System.Security.Cryptography;
using MelonLoader;

namespace DedicatedServerMod.Shared.ModVerification
{
    internal static class ClientModHashUtility
    {
        public static string TryResolveSha256(MelonBase melon)
        {
            if (melon == null)
            {
                return string.Empty;
            }

            string location = melon.MelonAssembly?.Location;
            if (!string.IsNullOrWhiteSpace(location) && File.Exists(location))
            {
                return ComputeFileSha256(location);
            }

            string knownHash = melon.MelonAssembly?.Hash;
            if (!string.IsNullOrWhiteSpace(knownHash))
            {
                return ClientModPolicy.NormalizeHash(knownHash);
            }

            return string.Empty;
        }

        public static string ComputeFileSha256(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return string.Empty;
            }

            try
            {
                using FileStream stream = File.OpenRead(filePath);
                using SHA256 sha256 = SHA256.Create();
                byte[] hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
