using System.Collections;
using DedicatedServerMod.Client.Managers;
using DedicatedServerMod.Shared.CustomClothing;
using MelonLoader;
using S1API.Items;
using S1API.Shops;
using DedicatedServerMod.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DedicatedServerMod.Client.CustomClothing
{
    internal sealed class ClientCustomClothingManager
    {
        private readonly MelonLogger.Instance _logger;
        private readonly ServerStatusQueryService _queryService;
        private readonly CustomClothingRuntimeRegistrar _runtimeRegistrar;

        private CustomClothingManifest _activeManifest;
        private CustomClothingManifest _preparedManifest;
        private string _preparedCacheDirectory = string.Empty;
        private string _shopManifestApplied = string.Empty;

        public ClientCustomClothingManager(MelonLogger.Instance logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _queryService = new ServerStatusQueryService();
            _runtimeRegistrar = new CustomClothingRuntimeRegistrar(_logger);
        }

        public string LastSyncError { get; private set; }

        public void Initialize()
        {
            Directory.CreateDirectory(CustomClothingPaths.CacheRoot);
        }

        public IEnumerator PrepareForConnection(string host, int port)
        {
            LastSyncError = null;
            _activeManifest = null;
            _preparedManifest = null;
            _preparedCacheDirectory = string.Empty;
            _shopManifestApplied = string.Empty;

            Task<CustomClothingManifest> manifestTask = _queryService.FetchCustomClothingManifestAsync(host, port);
            while (!manifestTask.IsCompleted)
            {
                yield return null;
            }

            if (manifestTask.IsFaulted)
            {
                LastSyncError = manifestTask.Exception?.GetBaseException().Message ?? "Custom clothing manifest request failed.";
                yield break;
            }

            CustomClothingManifest manifest = manifestTask.Result ?? new CustomClothingManifest();
            string cacheDirectory = CustomClothingPaths.BuildServerCacheDirectory(host, port, manifest.ManifestHash);
            Directory.CreateDirectory(cacheDirectory);
            PersistManifest(cacheDirectory, manifest);

            foreach (CustomClothingManifestEntry entry in manifest.Entries.OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                byte[] textureBytes = LoadCachedBytes(cacheDirectory, entry.ContentHash);
                byte[] iconBytes = LoadCachedBytes(cacheDirectory, entry.IconContentHash);

                if (textureBytes == null || (HasIcon(entry) && iconBytes == null))
                {
                    Task<CustomClothingAssetPayload> assetTask = _queryService.FetchCustomClothingAssetAsync(host, port, entry.Id);
                    while (!assetTask.IsCompleted)
                    {
                        yield return null;
                    }

                    if (assetTask.IsFaulted)
                    {
                        LastSyncError = assetTask.Exception?.GetBaseException().Message ?? $"Failed to download custom clothing asset '{entry.Id}'.";
                        yield break;
                    }

                    CustomClothingAssetPayload payload = assetTask.Result;
                    if (payload == null)
                    {
                        LastSyncError = $"Server returned an empty custom clothing asset payload for '{entry.Id}'.";
                        yield break;
                    }

                    textureBytes = Convert.FromBase64String(payload.TextureBase64 ?? string.Empty);
                    iconBytes = !string.IsNullOrWhiteSpace(payload.IconBase64)
                        ? Convert.FromBase64String(payload.IconBase64)
                        : null;

                    PersistCachedBytes(cacheDirectory, payload.ContentHash, textureBytes);
                    if (!string.IsNullOrWhiteSpace(payload.IconContentHash) && iconBytes != null)
                    {
                        PersistCachedBytes(cacheDirectory, payload.IconContentHash, iconBytes);
                    }
                }

                if (!ValidateHash(textureBytes, entry.ContentHash))
                {
                    LastSyncError = $"Custom clothing texture hash mismatch for '{entry.Id}'.";
                    yield break;
                }

                if (HasIcon(entry) && !ValidateHash(iconBytes, entry.IconContentHash))
                {
                    LastSyncError = $"Custom clothing icon hash mismatch for '{entry.Id}'.";
                    yield break;
                }

            }

            _preparedManifest = manifest;
            _preparedCacheDirectory = cacheDirectory;
            DebugLog.Info($"Downloaded and cached {manifest.Entries.Count} custom clothing item(s) for {host}:{port} before connection start.");
        }

        public bool RegisterPreparedContent()
        {
            LastSyncError = null;

            if (_preparedManifest == null || _preparedManifest.Entries.Count == 0)
            {
                _activeManifest = _preparedManifest;
                return true;
            }

            foreach (CustomClothingManifestEntry entry in _preparedManifest.Entries.OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                byte[] textureBytes = LoadCachedBytes(_preparedCacheDirectory, entry.ContentHash);
                if (!ValidateHash(textureBytes, entry.ContentHash))
                {
                    LastSyncError = $"Prepared custom clothing texture is missing or invalid for '{entry.Id}'.";
                    return false;
                }

                byte[] iconBytes = null;
                if (HasIcon(entry))
                {
                    iconBytes = LoadCachedBytes(_preparedCacheDirectory, entry.IconContentHash);
                    if (!ValidateHash(iconBytes, entry.IconContentHash))
                    {
                        LastSyncError = $"Prepared custom clothing icon is missing or invalid for '{entry.Id}'.";
                        return false;
                    }
                }

                if (!_runtimeRegistrar.Register(new CustomClothingRuntimeEntry(entry, textureBytes, iconBytes)))
                {
                    LastSyncError = $"Failed to register custom clothing '{entry.Id}' on the client.";
                    return false;
                }
            }

            _activeManifest = _preparedManifest;
            _shopManifestApplied = string.Empty;

            if (string.Equals(SceneManager.GetActiveScene().name, "Main", StringComparison.Ordinal))
            {
                MelonCoroutines.Start(ApplyShopListingsWhenReady(_activeManifest));
            }

            DebugLog.Info($"Registered {_activeManifest.Entries.Count} custom clothing item(s) during client pre-load.");
            return true;
        }

        public void OnSceneLoaded(string sceneName)
        {
            if (!string.Equals(sceneName, "Main", StringComparison.Ordinal) ||
                _activeManifest == null ||
                _activeManifest.Entries.Count == 0 ||
                string.Equals(_shopManifestApplied, _activeManifest.ManifestHash, StringComparison.Ordinal))
            {
                return;
            }

            MelonCoroutines.Start(ApplyShopListingsWhenReady(_activeManifest));
        }

        public void OnDisconnected()
        {
            _activeManifest = null;
            _preparedManifest = null;
            _preparedCacheDirectory = string.Empty;
            _shopManifestApplied = string.Empty;
        }

        private IEnumerator ApplyShopListingsWhenReady(CustomClothingManifest manifest)
        {
            float timeout = 15f;
            float elapsed = 0f;
            while (elapsed < timeout && ShopManager.GetAllShops().Length == 0)
            {
                yield return new WaitForSeconds(0.25f);
                elapsed += 0.25f;
            }

            foreach (CustomClothingManifestEntry entry in manifest.Entries)
            {
                ItemDefinition item = ItemManager.GetItemDefinition(entry.Id);
                if (item == null)
                {
                    continue;
                }

                if (entry.ShopNames != null && entry.ShopNames.Count > 0)
                {
                    ShopManager.AddToShops(item, entry.BasePurchasePrice, entry.ShopNames.ToArray());
                }
                else
                {
                    ShopManager.AddToCompatibleShops(item, entry.BasePurchasePrice);
                }
            }

            _shopManifestApplied = manifest.ManifestHash;
        }

        private static bool HasIcon(CustomClothingManifestEntry entry)
        {
            return !string.IsNullOrWhiteSpace(entry?.IconContentHash);
        }

        private static string GetCacheFilePath(string cacheDirectory, string hash)
        {
            return string.IsNullOrWhiteSpace(hash)
                ? string.Empty
                : Path.Combine(cacheDirectory, $"{CustomClothingPaths.ShortenHash(hash, 24)}.png");
        }

        private static byte[] LoadCachedBytes(string cacheDirectory, string hash)
        {
            string path = GetCacheFilePath(cacheDirectory, hash);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            byte[] bytes = File.ReadAllBytes(path);
            return ValidateHash(bytes, hash) ? bytes : null;
        }

        private static void PersistCachedBytes(string cacheDirectory, string hash, byte[] bytes)
        {
            if (string.IsNullOrWhiteSpace(hash) || bytes == null || bytes.Length == 0)
            {
                return;
            }

            string path = GetCacheFilePath(cacheDirectory, hash);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(path, bytes);
        }

        private static void PersistManifest(string cacheDirectory, CustomClothingManifest manifest)
        {
            Directory.CreateDirectory(cacheDirectory);
            string manifestPath = Path.Combine(cacheDirectory, "manifest.json");
            File.WriteAllText(manifestPath, Newtonsoft.Json.JsonConvert.SerializeObject(manifest, Newtonsoft.Json.Formatting.Indented));
        }

        private static bool ValidateHash(byte[] bytes, string expectedHash)
        {
            if (bytes == null || string.IsNullOrWhiteSpace(expectedHash))
            {
                return false;
            }

            return string.Equals(CustomClothingHashUtility.ComputeSha256(bytes), expectedHash, StringComparison.OrdinalIgnoreCase);
        }
    }
}
