using S1API.Items;

namespace DedicatedServerMod.Shared.CustomClothing
{
    internal sealed class CustomClothingTemplateCatalog
    {
        private readonly Dictionary<string, CustomClothingTemplateDefinition> _templates =
            new Dictionary<string, CustomClothingTemplateDefinition>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyCollection<CustomClothingTemplateDefinition> Templates => _templates.Values;

        public static CustomClothingTemplateCatalog Build()
        {
            CustomClothingTemplateCatalog catalog = new CustomClothingTemplateCatalog();
            foreach (ItemDefinition definition in ItemManager.GetAllItemDefinitions())
            {
                if (definition is not ClothingItemDefinition clothingDefinition)
                {
                    continue;
                }

                string assetPath = CustomClothingPaths.NormalizeResourcePath(clothingDefinition.ClothingAssetPath);
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    continue;
                }

                catalog._templates[assetPath] = new CustomClothingTemplateDefinition(
                    clothingDefinition.ID,
                    clothingDefinition.Name,
                    clothingDefinition.Description,
                    assetPath,
                    clothingDefinition.ApplicationType == ClothingApplicationType.Accessory
                        ? CustomClothingAssetKind.Accessory
                        : CustomClothingAssetKind.BodyLayer,
                    (int)clothingDefinition.Slot,
                    (int)clothingDefinition.ApplicationType,
                    clothingDefinition.Colorable,
                    (int)clothingDefinition.DefaultColor,
                    clothingDefinition.SlotsToBlock.Select(slot => (int)slot).ToList(),
                    clothingDefinition.BasePurchasePrice,
                    clothingDefinition.ResellMultiplier);
            }

            return catalog;
        }

        public bool TryResolve(string assetPath, out CustomClothingTemplateDefinition template)
        {
            return _templates.TryGetValue(CustomClothingPaths.NormalizeResourcePath(assetPath), out template);
        }
    }

    internal sealed class CustomClothingTemplateDefinition
    {
        public CustomClothingTemplateDefinition(
            string baseItemId,
            string baseName,
            string baseDescription,
            string assetPath,
            CustomClothingAssetKind assetKind,
            int slot,
            int applicationType,
            bool colorable,
            int defaultColor,
            List<int> blockedSlots,
            float basePurchasePrice,
            float resellMultiplier)
        {
            BaseItemId = baseItemId;
            BaseName = baseName;
            BaseDescription = baseDescription;
            AssetPath = assetPath;
            AssetKind = assetKind;
            Slot = slot;
            ApplicationType = applicationType;
            Colorable = colorable;
            DefaultColor = defaultColor;
            BlockedSlots = blockedSlots ?? new List<int>();
            BasePurchasePrice = basePurchasePrice;
            ResellMultiplier = resellMultiplier;
        }

        public string BaseItemId { get; }

        public string BaseName { get; }

        public string BaseDescription { get; }

        public string AssetPath { get; }

        public CustomClothingAssetKind AssetKind { get; }

        public int Slot { get; }

        public int ApplicationType { get; }

        public bool Colorable { get; }

        public int DefaultColor { get; }

        public List<int> BlockedSlots { get; }

        public float BasePurchasePrice { get; }

        public float ResellMultiplier { get; }
    }
}
