using System.Globalization;
using DedicatedServerMod.API.Toml;
using DedicatedServerMod.Shared.CustomClothing;
using MelonLoader;
using S1API.Items;
using S1API.Shops;
using UnityEngine.SceneManagement;

namespace DedicatedServerMod.Server.CustomClothing
{
    internal sealed class ServerCustomClothingManager
    {
        private readonly MelonLogger.Instance _logger;
        private readonly CustomClothingRuntimeRegistrar _runtimeRegistrar;
        private readonly Dictionary<string, ServerCustomClothingSource> _sourcesById =
            new Dictionary<string, ServerCustomClothingSource>(StringComparer.OrdinalIgnoreCase);

        private CustomClothingManifest _manifest = new CustomClothingManifest();
        private bool _shopIntegrationComplete;

        public ServerCustomClothingManager(MelonLogger.Instance logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _runtimeRegistrar = new CustomClothingRuntimeRegistrar(_logger);
        }

        public void Initialize()
        {
            _sourcesById.Clear();
            _shopIntegrationComplete = false;
            Directory.CreateDirectory(CustomClothingPaths.AuthoringRoot);

            CustomClothingTemplateCatalog catalog = CustomClothingTemplateCatalog.Build();
            List<ServerCustomClothingSource> discoveredSources = DiscoverSources(catalog);
            if (discoveredSources.Count == 0)
            {
                _manifest = new CustomClothingManifest
                {
                    ManifestHash = CustomClothingHashUtility.ComputeManifestHash(Array.Empty<CustomClothingManifestEntry>())
                };
                _logger.Msg($"Custom clothing authoring root ready at '{CustomClothingPaths.AuthoringRoot}'");
                return;
            }

            foreach (ServerCustomClothingSource source in discoveredSources)
            {
                if (!_runtimeRegistrar.Register(source.RuntimeEntry))
                {
                    _logger.Warning($"Skipping custom clothing '{source.RuntimeEntry.ManifestEntry.Id}' because runtime registration failed.");
                    continue;
                }

                _sourcesById[source.RuntimeEntry.ManifestEntry.Id] = source;
            }

            RebuildManifest();
            _logger.Msg($"Registered {_sourcesById.Count} custom clothing item(s) from '{CustomClothingPaths.AuthoringRoot}'.");
        }

        public void Tick()
        {
            if (_shopIntegrationComplete || _sourcesById.Count == 0 || !string.Equals(SceneManager.GetActiveScene().name, "Main", StringComparison.Ordinal))
            {
                return;
            }

            Shop[] allShops = ShopManager.GetAllShops();
            if (allShops.Length == 0)
            {
                return;
            }

            foreach (ServerCustomClothingSource source in _sourcesById.Values)
            {
                if (source.ShopsResolved)
                {
                    continue;
                }

                ResolveShops(source);
            }

            _shopIntegrationComplete = _sourcesById.Values.All(source => source.ShopsResolved);
            RebuildManifest();
        }

        public CustomClothingManifest GetManifest()
        {
            return _manifest;
        }

        public bool TryGetAssetPayload(string itemId, out CustomClothingAssetPayload payload)
        {
            if (_sourcesById.TryGetValue(itemId ?? string.Empty, out ServerCustomClothingSource source))
            {
                payload = new CustomClothingAssetPayload
                {
                    Id = source.RuntimeEntry.ManifestEntry.Id,
                    ContentHash = source.RuntimeEntry.ManifestEntry.ContentHash,
                    TextureBase64 = Convert.ToBase64String(source.RuntimeEntry.TextureBytes),
                    IconContentHash = source.RuntimeEntry.ManifestEntry.IconContentHash,
                    IconBase64 = source.RuntimeEntry.IconBytes != null && source.RuntimeEntry.IconBytes.Length > 0
                        ? Convert.ToBase64String(source.RuntimeEntry.IconBytes)
                        : string.Empty
                };
                return true;
            }

            payload = null;
            return false;
        }

        private List<ServerCustomClothingSource> DiscoverSources(CustomClothingTemplateCatalog catalog)
        {
            List<ServerCustomClothingSource> sources = new List<ServerCustomClothingSource>();
            HashSet<string> seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> seenAssetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> excludedIconPaths = CollectReferencedIconPaths();

            foreach (string texturePath in Directory.EnumerateFiles(CustomClothingPaths.AuthoringRoot, "*.png", SearchOption.AllDirectories))
            {
                string normalizedTexturePath = Path.GetFullPath(texturePath);
                if (excludedIconPaths.Contains(normalizedTexturePath))
                {
                    continue;
                }

                string templateRelativePath = CustomClothingPaths.NormalizeResourcePath(
                    Path.GetRelativePath(CustomClothingPaths.AuthoringRoot, Path.GetDirectoryName(texturePath) ?? string.Empty));
                if (!catalog.TryResolve(templateRelativePath, out CustomClothingTemplateDefinition template))
                {
                    _logger.Warning($"Skipping custom clothing texture '{texturePath}' because '{templateRelativePath}' does not match a known clothing asset path.");
                    continue;
                }

                CustomClothingTomlOverrides overrides = CustomClothingTomlOverrides.Load(texturePath, _logger);
                if (!overrides.Enabled)
                {
                    continue;
                }

                string variantSlug = Slugify(Path.GetFileNameWithoutExtension(texturePath));
                if (string.IsNullOrWhiteSpace(variantSlug))
                {
                    _logger.Warning($"Skipping custom clothing texture '{texturePath}' because its file name does not produce a valid variant slug.");
                    continue;
                }

                byte[] textureBytes;
                try
                {
                    textureBytes = File.ReadAllBytes(texturePath);
                }
                catch (Exception ex)
                {
                    _logger.Warning($"Skipping custom clothing texture '{texturePath}': {ex.Message}");
                    continue;
                }

                string itemId = string.IsNullOrWhiteSpace(overrides.Id)
                    ? $"dsc_{Slugify(template.BaseItemId)}_{variantSlug}"
                    : overrides.Id.Trim();
                string runtimeAssetPath = CustomClothingPaths.BuildRuntimeAssetPath(template.AssetPath, variantSlug);
                if (!seenIds.Add(itemId))
                {
                    _logger.Warning($"Skipping duplicate custom clothing item id '{itemId}' from '{texturePath}'.");
                    continue;
                }

                if (!seenAssetPaths.Add(runtimeAssetPath))
                {
                    _logger.Warning($"Skipping duplicate custom clothing asset path '{runtimeAssetPath}' from '{texturePath}'.");
                    continue;
                }

                byte[] iconBytes = overrides.TryLoadIconBytes(texturePath, _logger);
                CustomClothingManifestEntry manifestEntry = new CustomClothingManifestEntry
                {
                    Id = itemId,
                    BaseItemId = template.BaseItemId,
                    BaseAssetPath = template.AssetPath,
                    AssetPath = runtimeAssetPath,
                    AssetKind = template.AssetKind,
                    ContentHash = CustomClothingHashUtility.ComputeSha256(textureBytes),
                    IconContentHash = iconBytes != null && iconBytes.Length > 0 ? CustomClothingHashUtility.ComputeSha256(iconBytes) : string.Empty,
                    Name = string.IsNullOrWhiteSpace(overrides.Name)
                        ? $"{Titleize(variantSlug)} {template.BaseName}".Trim()
                        : overrides.Name.Trim(),
                    Description = string.IsNullOrWhiteSpace(overrides.Description) ? template.BaseDescription : overrides.Description.Trim(),
                    BasePurchasePrice = overrides.PriceOverride.HasValue ? (float)overrides.PriceOverride.Value : template.BasePurchasePrice,
                    ResellMultiplier = template.ResellMultiplier,
                    Colorable = overrides.ColorableOverride ?? template.Colorable,
                    DefaultColor = template.DefaultColor,
                    Slot = template.Slot,
                    ApplicationType = template.ApplicationType,
                    BlockedSlots = new List<int>(template.BlockedSlots),
                    ShopNames = overrides.HasExplicitShopNames
                        ? new List<string>(overrides.ShopNames)
                        : new List<string>()
                };

                sources.Add(new ServerCustomClothingSource(
                    new CustomClothingRuntimeEntry(manifestEntry, textureBytes, iconBytes),
                    overrides.HasExplicitShopNames,
                    overrides.ShopNames));
            }

            return sources;
        }

        private HashSet<string> CollectReferencedIconPaths()
        {
            HashSet<string> iconPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string tomlPath in Directory.EnumerateFiles(CustomClothingPaths.AuthoringRoot, "*.toml", SearchOption.AllDirectories))
            {
                try
                {
                    TomlReadResult readResult = TomlParser.ParseFile(tomlPath);
                    TomlTable table = readResult.Document.Root;
                    if (!table.TryGetString("iconPath", out string iconPath) || string.IsNullOrWhiteSpace(iconPath))
                    {
                        continue;
                    }

                    string resolvedPath = iconPath;
                    if (!Path.IsPathRooted(resolvedPath))
                    {
                        resolvedPath = Path.Combine(Path.GetDirectoryName(tomlPath) ?? string.Empty, resolvedPath);
                    }

                    iconPaths.Add(Path.GetFullPath(resolvedPath));
                }
                catch
                {
                    // Invalid TOML is already surfaced during normal per-item parsing.
                }
            }

            return iconPaths;
        }

        private void ResolveShops(ServerCustomClothingSource source)
        {
            CustomClothingManifestEntry entry = source.RuntimeEntry.ManifestEntry;
            ItemDefinition item = ItemManager.GetItemDefinition(entry.Id);
            if (item == null)
            {
                _logger.Warning($"Unable to resolve shops for '{entry.Id}' because the item is not registered.");
                source.ShopsResolved = true;
                return;
            }

            if (source.HasExplicitShopNames)
            {
                if (source.DesiredShopNames.Count > 0)
                {
                    ShopManager.AddToShops(item, entry.BasePurchasePrice, source.DesiredShopNames.ToArray());
                }
            }
            else
            {
                string[] baseShopNames = ShopManager.FindShopsByItem(entry.BaseItemId)
                    .Select(shop => shop.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (baseShopNames.Length > 0)
                {
                    ShopManager.AddToShops(item, entry.BasePurchasePrice, baseShopNames);
                }
                else
                {
                    ShopManager.AddToCompatibleShops(item, entry.BasePurchasePrice);
                }
            }

            entry.ShopNames = ShopManager.FindShopsByItem(entry.Id)
                .Select(shop => shop.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            source.ShopsResolved = true;
        }

        private void RebuildManifest()
        {
            List<CustomClothingManifestEntry> entries = _sourcesById.Values
                .Select(source => source.RuntimeEntry.ManifestEntry)
                .OrderBy(entry => entry.Id, StringComparer.Ordinal)
                .ToList();

            _manifest = new CustomClothingManifest
            {
                Version = 1,
                ManifestHash = CustomClothingHashUtility.ComputeManifestHash(entries),
                Entries = entries
            };
        }

        private static string Slugify(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            List<char> characters = new List<char>(value.Length);
            bool lastWasUnderscore = false;
            foreach (char character in value.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(character))
                {
                    characters.Add(character);
                    lastWasUnderscore = false;
                }
                else if (!lastWasUnderscore)
                {
                    characters.Add('_');
                    lastWasUnderscore = true;
                }
            }

            return new string(characters.ToArray()).Trim('_');
        }

        private static string Titleize(string slug)
        {
            string spaced = (slug ?? string.Empty).Replace('_', ' ').Trim();
            if (string.IsNullOrWhiteSpace(spaced))
            {
                return string.Empty;
            }

            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(spaced);
        }

        private sealed class ServerCustomClothingSource
        {
            public ServerCustomClothingSource(CustomClothingRuntimeEntry runtimeEntry, bool hasExplicitShopNames, IEnumerable<string> desiredShopNames)
            {
                RuntimeEntry = runtimeEntry;
                HasExplicitShopNames = hasExplicitShopNames;
                DesiredShopNames = (desiredShopNames ?? Enumerable.Empty<string>())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            public CustomClothingRuntimeEntry RuntimeEntry { get; }

            public bool HasExplicitShopNames { get; }

            public List<string> DesiredShopNames { get; }

            public bool ShopsResolved { get; set; }
        }

        private sealed class CustomClothingTomlOverrides
        {
            public bool Enabled { get; private set; } = true;

            public bool HasExplicitShopNames { get; private set; }

            public List<string> ShopNames { get; private set; } = new List<string>();

            public string Id { get; private set; } = string.Empty;

            public string Name { get; private set; } = string.Empty;

            public string Description { get; private set; } = string.Empty;

            public string IconPath { get; private set; } = string.Empty;

            public double? PriceOverride { get; private set; }

            public bool? ColorableOverride { get; private set; }

            public static CustomClothingTomlOverrides Load(string texturePath, MelonLogger.Instance logger)
            {
                CustomClothingTomlOverrides overrides = new CustomClothingTomlOverrides();
                string tomlPath = Path.ChangeExtension(texturePath, ".toml");
                if (!File.Exists(tomlPath))
                {
                    return overrides;
                }

                try
                {
                    TomlReadResult readResult = TomlParser.ParseFile(tomlPath);
                    foreach (TomlDiagnostic diagnostic in readResult.Diagnostics)
                    {
                        logger.Warning($"Custom clothing TOML warning in '{tomlPath}' line {diagnostic.LineNumber}: {diagnostic.Message}");
                    }

                    TomlTable table = readResult.Document.Root;
                    if (table.TryGetString("id", out string id))
                    {
                        overrides.Id = id;
                    }

                    if (table.TryGetString("name", out string name))
                    {
                        overrides.Name = name;
                    }

                    if (table.TryGetString("description", out string description))
                    {
                        overrides.Description = description;
                    }

                    if (table.TryGetString("iconPath", out string iconPath))
                    {
                        overrides.IconPath = iconPath;
                    }

                    if (table.TryGetBoolean("enabled", out bool enabled))
                    {
                        overrides.Enabled = enabled;
                    }

                    if (table.TryGetBoolean("colorable", out bool colorable))
                    {
                        overrides.ColorableOverride = colorable;
                    }

                    if (table.TryGetDouble("price", out double price) && price >= 0d)
                    {
                        overrides.PriceOverride = price;
                    }

                    if (table.TryGetArray("shopNames", out IReadOnlyList<TomlValue> values))
                    {
                        overrides.HasExplicitShopNames = true;
                        overrides.ShopNames = values
                            .Where(value => value != null && value.Kind == TomlValueKind.String)
                            .Select(value => value.GetString())
                            .Where(value => !string.IsNullOrWhiteSpace(value))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                    }
                }
                catch (Exception ex)
                {
                    logger.Warning($"Failed to parse custom clothing TOML '{tomlPath}': {ex.Message}");
                }

                return overrides;
            }

            public byte[] TryLoadIconBytes(string texturePath, MelonLogger.Instance logger)
            {
                if (string.IsNullOrWhiteSpace(IconPath))
                {
                    return null;
                }

                string candidatePath = IconPath;
                if (!Path.IsPathRooted(candidatePath))
                {
                    string textureDirectory = Path.GetDirectoryName(texturePath) ?? string.Empty;
                    candidatePath = Path.Combine(textureDirectory, candidatePath);
                }

                try
                {
                    return File.Exists(candidatePath) ? File.ReadAllBytes(candidatePath) : null;
                }
                catch (Exception ex)
                {
                    logger.Warning($"Failed to load custom clothing icon '{candidatePath}': {ex.Message}");
                    return null;
                }
            }
        }
    }
}
