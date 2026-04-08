using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace DedicatedServerMod.Shared.CustomClothing
{
    internal static class CustomClothingHashUtility
    {
        public static string ComputeSha256(byte[] content)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(content);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash)
                {
                    builder.Append(value.ToString("x2"));
                }

                return builder.ToString();
            }
        }

        public static string ComputeManifestHash(IEnumerable<CustomClothingManifestEntry> entries)
        {
            List<CustomClothingManifestEntry> orderedEntries = (entries ?? Enumerable.Empty<CustomClothingManifestEntry>())
                .OrderBy(entry => entry.Id, StringComparer.Ordinal)
                .ToList();

            string json = JsonConvert.SerializeObject(orderedEntries);
            return ComputeSha256(Encoding.UTF8.GetBytes(json));
        }
    }
}
