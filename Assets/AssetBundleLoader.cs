using System;
using System.IO;
using DedicatedServerMod.Client.Core;
using MelonLoader;
using UnityEngine;

namespace DedicatedServerMod.Assets
{
	public static class AssetBundleLoader
	{
		public static AssetBundle LoadEmbeddedBundle(string resourceName, Action<string> logError, Action<string> logInfo)
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
					byte[] bundleData = new byte[stream.Length];
					stream.Read(bundleData, 0, bundleData.Length);
					var bundle = AssetBundle.LoadFromMemory(bundleData);
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
				logError?.Invoke($"Error loading embedded AssetBundle: {ex.Message}");
				return null;
			}
		}

		public static T LoadAsset<T>(AssetBundle bundle, string name, Action<string> logError) where T : UnityEngine.Object
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
	}
}


