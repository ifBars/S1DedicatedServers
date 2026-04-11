using System;
using System.IO;
using DedicatedServerMod.Client.Core;
using UnityEngine;
#if IL2CPP
using AssetBundleType = UnityEngine.Il2CppAssetBundle;
#else
using AssetBundleType = UnityEngine.AssetBundle;
#endif

namespace DedicatedServerMod.Assets
{
	public static class AssetBundleLoader
	{
		public static AssetBundleType LoadEmbeddedBundle(string resourceName, Action<string> logError, Action<string> logInfo)
		{
			try
			{
				using (var stream = typeof(ClientBootstrap).Assembly.GetManifestResourceStream(resourceName))
				{
					if (stream == null)
					{
						logError?.Invoke($"Embedded AssetBundle resource not found: {resourceName}");
						return null;
					}

					byte[] bundleData;
					using (MemoryStream buffer = new MemoryStream())
					{
						stream.CopyTo(buffer);
						bundleData = buffer.ToArray();
					}

					AssetBundleType bundle = LoadBundleFromMemory(bundleData);
					if (bundle == null)
					{
						logError?.Invoke("Failed to load AssetBundle from embedded resource bytes");
						return null;
					}
					logInfo?.Invoke("AssetBundle loaded successfully from embedded resources");
					return bundle;
				}
			}
			catch (Exception ex)
			{
				logError?.Invoke($"Error loading embedded AssetBundle '{resourceName}': {ex}");
				return null;
			}
		}

		public static T LoadAsset<T>(AssetBundleType bundle, string name, Action<string> logError) where T : UnityEngine.Object
		{
			try
			{
				var asset = bundle.LoadAsset<T>(name);
				if (asset == null)
				{
					logError?.Invoke($"Asset '{name}' not found in bundle");
				}
				return asset;
			}
			catch (Exception ex)
			{
				logError?.Invoke($"Error loading asset '{name}': {ex.Message}");
				return null;
			}
		}

		private static AssetBundleType LoadBundleFromMemory(byte[] bundleData)
		{
#if IL2CPP
			return UnityEngine.Il2CppAssetBundleManager.LoadFromMemory(bundleData);
#else
			return AssetBundle.LoadFromMemory(bundleData);
#endif
		}
	}
}


