using DedicatedServerMod.Utils;

namespace DedicatedServerMod.Shared.Configuration
{
    /// <summary>
    /// Server-side persistence and file-format handling for <see cref="ServerConfig"/>.
    /// </summary>
    public sealed partial class ServerConfig
    {
        internal static ServerConfig LoadConfigSnapshot(string path)
        {
            return CreateStore(path, configPathWasExplicit: true).LoadSnapshot(path);
        }

        private static void LoadPersistentConfig()
        {
            try
            {
                ServerConfigStore.ServerConfigStoreLoadResult loadResult = CreateStore().Load();
                ApplyLoadedConfig(loadResult.Config, loadResult.LoadedFromPath);

                if (loadResult.ShouldWriteNormalizedFile)
                {
                    SavePersistentConfig();

                    if (!string.IsNullOrWhiteSpace(loadResult.RewriteReason))
                    {
                        DebugLog.Debug(loadResult.RewriteReason);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Failed to load server config: {ex}");
                ApplyLoadedConfig(new ServerConfig(), ConfigFilePath);
                SavePersistentConfig();
            }
        }

        private static void SavePersistentConfig()
        {
            try
            {
                CreateStore().Save(_instance ?? new ServerConfig());
                Saved?.Invoke();
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Failed to save server config: {ex}");
            }
        }

        private static ServerConfigStore CreateStore()
        {
            return CreateStore(ConfigFilePath, _configPathWasExplicit);
        }

        private static ServerConfigStore CreateStore(string configPath, bool configPathWasExplicit)
        {
            return new ServerConfigStore(configPath, configPathWasExplicit);
        }
    }
}
