using Newtonsoft.Json;

namespace DedicatedServerMod.Shared.CustomClothing
{
    internal enum CustomClothingAssetKind
    {
        Accessory = 0,
        BodyLayer = 1
    }

    internal sealed class CustomClothingManifest
    {
        public int Version { get; set; } = 1;

        public string ManifestHash { get; set; } = string.Empty;

        public List<CustomClothingManifestEntry> Entries { get; set; } = new List<CustomClothingManifestEntry>();
    }

    internal sealed class CustomClothingManifestEntry
    {
        public string Id { get; set; } = string.Empty;

        public string BaseItemId { get; set; } = string.Empty;

        public string BaseAssetPath { get; set; } = string.Empty;

        public string AssetPath { get; set; } = string.Empty;

        public CustomClothingAssetKind AssetKind { get; set; }

        public string ContentHash { get; set; } = string.Empty;

        public string IconContentHash { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public float BasePurchasePrice { get; set; }

        public float ResellMultiplier { get; set; }

        public bool Colorable { get; set; }

        public int DefaultColor { get; set; }

        public int Slot { get; set; }

        public int ApplicationType { get; set; }

        public List<int> BlockedSlots { get; set; } = new List<int>();

        public List<string> ShopNames { get; set; } = new List<string>();
    }

    internal sealed class CustomClothingAssetPayload
    {
        public string Id { get; set; } = string.Empty;

        public string ContentHash { get; set; } = string.Empty;

        public string TextureBase64 { get; set; } = string.Empty;

        public string IconContentHash { get; set; } = string.Empty;

        public string IconBase64 { get; set; } = string.Empty;
    }

    internal sealed class CustomClothingManifestResponse
    {
        public bool Success { get; set; }

        public string Error { get; set; } = string.Empty;

        public CustomClothingManifest Manifest { get; set; }
    }

    internal sealed class CustomClothingAssetResponse
    {
        public bool Success { get; set; }

        public string Error { get; set; } = string.Empty;

        public CustomClothingAssetPayload Asset { get; set; }
    }

    internal sealed class CustomClothingRuntimeEntry
    {
        public CustomClothingRuntimeEntry(CustomClothingManifestEntry manifestEntry, byte[] textureBytes, byte[] iconBytes)
        {
            ManifestEntry = manifestEntry ?? throw new ArgumentNullException(nameof(manifestEntry));
            TextureBytes = textureBytes ?? throw new ArgumentNullException(nameof(textureBytes));
            IconBytes = iconBytes;
        }

        public CustomClothingManifestEntry ManifestEntry { get; }

        [JsonIgnore]
        public byte[] TextureBytes { get; }

        [JsonIgnore]
        public byte[] IconBytes { get; }
    }
}
