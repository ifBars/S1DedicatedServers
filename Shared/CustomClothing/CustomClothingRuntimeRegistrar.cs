using MelonLoader;
using S1API.Items;
using S1API.Rendering;
using UnityEngine;

namespace DedicatedServerMod.Shared.CustomClothing
{
    internal sealed class CustomClothingRuntimeRegistrar
    {
        private readonly MelonLogger.Instance _logger;

        public CustomClothingRuntimeRegistrar(MelonLogger.Instance logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool Register(CustomClothingRuntimeEntry runtimeEntry)
        {
            if (runtimeEntry == null)
            {
                throw new ArgumentNullException(nameof(runtimeEntry));
            }

            CustomClothingManifestEntry manifestEntry = runtimeEntry.ManifestEntry;
            Texture2D clothingTexture = CreateTexture(runtimeEntry.TextureBytes, manifestEntry.Id, filterMode: FilterMode.Bilinear);
            if (clothingTexture == null)
            {
                _logger.Warning($"Custom clothing '{manifestEntry.Id}' texture could not be decoded.");
                return false;
            }

            bool assetRegistered = manifestEntry.AssetKind switch
            {
                CustomClothingAssetKind.Accessory => RegisterAccessory(manifestEntry, clothingTexture),
                CustomClothingAssetKind.BodyLayer => AvatarLayerFactory.CreateAndRegisterAvatarLayer(
                    manifestEntry.BaseAssetPath,
                    manifestEntry.AssetPath,
                    manifestEntry.Name,
                    clothingTexture),
                _ => false
            };

            if (!assetRegistered)
            {
                _logger.Warning($"Custom clothing '{manifestEntry.Id}' asset registration failed.");
                return false;
            }

            Sprite icon = null;
            if (runtimeEntry.IconBytes != null && runtimeEntry.IconBytes.Length > 0)
            {
                Texture2D iconTexture = CreateTexture(runtimeEntry.IconBytes, $"{manifestEntry.Id}_icon", FilterMode.Bilinear);
                if (iconTexture != null)
                {
                    icon = Sprite.Create(iconTexture, new Rect(0f, 0f, iconTexture.width, iconTexture.height), new Vector2(0.5f, 0.5f), 100f);
                    UnityEngine.Object.DontDestroyOnLoad(icon);
                    icon.hideFlags = HideFlags.DontUnloadUnusedAsset;
                }
            }

            return CreateOrUpdateItemDefinition(manifestEntry, icon);
        }

        private static Texture2D CreateTexture(byte[] bytes, string name, FilterMode filterMode)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return null;
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, mipChain: false);
            if (!ImageConversion.LoadImage(texture, bytes, markNonReadable: false))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }

            texture.name = name;
            texture.filterMode = filterMode;
            texture.wrapMode = TextureWrapMode.Clamp;
            UnityEngine.Object.DontDestroyOnLoad(texture);
            texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return texture;
        }

        private static bool RegisterAccessory(CustomClothingManifestEntry manifestEntry, Texture2D clothingTexture)
        {
            Dictionary<string, Texture2D> textureReplacements = new Dictionary<string, Texture2D>
            {
                ["_MainTex"] = clothingTexture,
                ["_BaseMap"] = clothingTexture,
                ["_Albedo"] = clothingTexture
            };

            return AccessoryFactory.CreateAndRegisterAccessory(
                manifestEntry.BaseAssetPath,
                manifestEntry.AssetPath,
                manifestEntry.Name,
                textureReplacements);
        }

        private bool CreateOrUpdateItemDefinition(CustomClothingManifestEntry manifestEntry, Sprite icon)
        {
            ItemDefinition existingDefinition = ItemManager.GetItemDefinition(manifestEntry.Id);
            if (existingDefinition != null && existingDefinition is not ClothingItemDefinition)
            {
                _logger.Warning($"Custom clothing id '{manifestEntry.Id}' already exists but is not clothing.");
                return false;
            }

            if (existingDefinition is ClothingItemDefinition existingClothing)
            {
                existingClothing.Name = manifestEntry.Name;
                existingClothing.Description = manifestEntry.Description;
                existingClothing.ClothingAssetPath = manifestEntry.AssetPath;
                existingClothing.Colorable = manifestEntry.Colorable;
                existingClothing.DefaultColor = (ClothingColor)manifestEntry.DefaultColor;
                existingClothing.BasePurchasePrice = manifestEntry.BasePurchasePrice;
                existingClothing.ResellMultiplier = manifestEntry.ResellMultiplier;
                existingClothing.Slot = (ClothingSlot)manifestEntry.Slot;
                existingClothing.ApplicationType = (ClothingApplicationType)manifestEntry.ApplicationType;
                existingClothing.SlotsToBlock = manifestEntry.BlockedSlots.Select(slot => (ClothingSlot)slot).ToList();
                if (icon != null)
                {
                    existingClothing.Icon = icon;
                }

                return ValidateRegisteredItem(manifestEntry.Id);
            }

            ClothingItemDefinitionBuilder builder = ClothingItemCreator.CloneFrom(manifestEntry.BaseItemId);
            if (builder == null)
            {
                _logger.Warning($"Failed to clone base clothing item '{manifestEntry.BaseItemId}' for '{manifestEntry.Id}'.");
                return false;
            }

            ClothingItemDefinition clothingDefinition = builder
                .WithBasicInfo(manifestEntry.Id, manifestEntry.Name, manifestEntry.Description)
                .WithClothingAsset(manifestEntry.AssetPath)
                .WithSlot((ClothingSlot)manifestEntry.Slot)
                .WithApplicationType((ClothingApplicationType)manifestEntry.ApplicationType)
                .WithColorable(manifestEntry.Colorable)
                .WithDefaultColor((ClothingColor)manifestEntry.DefaultColor)
                .WithBlockedSlots(manifestEntry.BlockedSlots.Select(slot => (ClothingSlot)slot).ToArray())
                .WithPricing(manifestEntry.BasePurchasePrice, manifestEntry.ResellMultiplier)
                .Build();

            if (clothingDefinition == null)
            {
                return false;
            }

            if (icon != null)
            {
                clothingDefinition.Icon = icon;
            }

            return ValidateRegisteredItem(manifestEntry.Id);
        }

        private bool ValidateRegisteredItem(string itemId)
        {
            if (ItemManager.GetItemDefinition(itemId) is ClothingItemDefinition)
            {
                return true;
            }

            _logger.Warning($"Custom clothing '{itemId}' is not resolvable from the item registry after registration.");
            return false;
        }
    }
}
