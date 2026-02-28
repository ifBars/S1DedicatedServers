using MelonLoader;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
#if IL2CPP
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.Persistence.Datas;
#else
using ScheduleOne.DevUtilities;
using ScheduleOne.Persistence;
using ScheduleOne.Persistence.Datas;
#endif
using UnityEngine;

namespace DedicatedServerMod.Server.Persistence
{
	/// <summary>
	/// Prepares a save folder for dedicated server usage by copying the DefaultSave,
	/// extracting an embedded Player_0 template, forcing PlayerCode, and ensuring
	/// required JSON files exist with tutorial disabled.
	/// </summary>
	public static class SaveInitializer
	{
		/// <summary>
		/// Ensure a save at <paramref name="saveFolderPath"/> is ready for loading.
		/// - Creates folder if missing
		/// - Copies DefaultSave template if folder is empty or missing key files
		/// - Extracts embedded Player_0.zip into Players
		/// - Sets PlayerCode in Player.json
		/// - Ensures Game.json and Metadata.json exist with sane defaults (PlayTutorial=false)
		/// </summary>
		public static void EnsureSavePrepared(string saveFolderPath, string organisationName, string serverPlayerCode, MelonLogger.Instance logger = null)
		{
			if (string.IsNullOrWhiteSpace(saveFolderPath))
				throw new ArgumentException("Save path is null or empty", nameof(saveFolderPath));

			Directory.CreateDirectory(saveFolderPath);

			// Copy DefaultSave if needed (missing core files)
			bool needsTemplate = NeedsDefaultTemplate(saveFolderPath);
			if (needsTemplate)
			{
				TryCopyDefaultSaveToFolder(saveFolderPath, logger);
			}

			// Extract Player_0.zip into Players
			TryExtractEmbeddedPlayerZip(Path.Combine(saveFolderPath, "Players"), logger);

			// Ensure Player.json's PlayerCode matches server identity
			TrySetPlayerCode(Path.Combine(saveFolderPath, "Players", "Player_0", "Player.json"), serverPlayerCode, logger);

			// Ensure Game.json exists
			var gameJsonPath = Path.Combine(saveFolderPath, "Game.json");
			if (!File.Exists(gameJsonPath))
			{
				var gameData = new GameData
				{
					Seed = UnityEngine.Random.Range(0, int.MaxValue),
					OrganisationName = string.IsNullOrWhiteSpace(organisationName)
						? new DirectoryInfo(saveFolderPath).Name
						: organisationName,
					Settings = new GameSettings(),
					GameVersion = Application.version
				};
				File.WriteAllText(gameJsonPath, gameData.GetJson());
				logger?.Msg($"Wrote default Game.json to {gameJsonPath}");
			}

			// Ensure Metadata.json exists with PlayTutorial=false
			var metaJsonPath = Path.Combine(saveFolderPath, "Metadata.json");
			if (!File.Exists(metaJsonPath))
			{
				MetaData metadata;
#if IL2CPP
				var now = Il2CppSystem.DateTime.Now;
				metadata = new MetaData(new(now), new(now), Application.version, Application.version, false);
#else
				var now = DateTime.Now;
				metadata = new MetaData(new(now), new(now), Application.version, Application.version, false);
#endif
				File.WriteAllText(metaJsonPath, metadata.GetJson());
				logger?.Msg($"Wrote default Metadata.json to {metaJsonPath}");
			}
			else
			{
				// Force tutorial off for dedicated servers
				try
				{
					MetaData metadata = ReadJsonData<MetaData>(metaJsonPath);
					if (metadata == null)
					{
#if IL2CPP
						var now = Il2CppSystem.DateTime.Now;
						metadata = new MetaData(new(now), new(now), Application.version, Application.version, false);
#else
						var now = DateTime.Now;
						metadata = new MetaData(new(now), new(now), Application.version, Application.version, false);
#endif
					}
					metadata.PlayTutorial = false;
					File.WriteAllText(metaJsonPath, metadata.GetJson());
				}
				catch (Exception ex)
				{
					logger?.Warning($"Failed to update Metadata.json (disabling tutorial): {ex.Message}");
				}
			}
		}

		private static bool NeedsDefaultTemplate(string saveFolderPath)
		{
			// If any of the core files/folders are missing, we consider it uninitialized
			bool hasGame = File.Exists(Path.Combine(saveFolderPath, "Game.json"));
			bool hasMeta = File.Exists(Path.Combine(saveFolderPath, "Metadata.json"));
			bool hasPlayers = Directory.Exists(Path.Combine(saveFolderPath, "Players"));
			return !(hasGame && hasMeta && hasPlayers);
		}

		private static void TryCopyDefaultSaveToFolder(string destinationDir, MelonLogger.Instance logger)
		{
			try
			{
				string sourceDir = Path.Combine(Application.streamingAssetsPath, "DefaultSave");
				CopyDirectory(sourceDir, destinationDir, true);
				logger?.Msg($"Copied DefaultSave to {destinationDir}");
			}
			catch (Exception ex)
			{
				logger?.Warning($"Failed to copy DefaultSave: {ex.Message}");
			}
		}

		private static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
		{
			var dir = new DirectoryInfo(sourceDir);
			if (!dir.Exists)
				throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

			Directory.CreateDirectory(destinationDir);

			foreach (var file in dir.GetFiles())
			{
				string targetFilePath = Path.Combine(destinationDir, file.Name);
				if (!File.Exists(targetFilePath))
					file.CopyTo(targetFilePath);
			}

			if (!recursive) return;

			foreach (var subDir in dir.GetDirectories())
			{
				string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
				CopyDirectory(subDir.FullName, newDestinationDir, true);
			}
		}

		private static void TryExtractEmbeddedPlayerZip(string playersDir, MelonLogger.Instance logger)
		{
			try
			{
				Directory.CreateDirectory(playersDir);

				// If Player_0 already exists, do not overwrite
				string player0Dir = Path.Combine(playersDir, "Player_0");
				if (Directory.Exists(player0Dir))
					return;

				using Stream stream = GetEmbeddedPlayerZipStream();
				if (stream == null)
					throw new FileNotFoundException("Embedded resource Player_0.zip not found");

				using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
				archive.ExtractToDirectory(playersDir);
				logger?.Msg($"Extracted Player_0.zip to {playersDir}");
			}
			catch (Exception ex)
			{
				logger?.Warning($"Failed to extract embedded Player_0.zip: {ex.Message}");
			}
		}

		private static Stream GetEmbeddedPlayerZipStream()
		{
			var asm = Assembly.GetExecutingAssembly();
			// Preferred resource name
			const string preferred = "DedicatedServerMod.Assets.Player_0.zip";
			var stream = asm.GetManifestResourceStream(preferred);
			if (stream != null)
				return stream;

			// Fallback: find any resource ending with Player_0.zip
			string name = asm.GetManifestResourceNames()
				.FirstOrDefault(n => n.EndsWith("Player_0.zip", StringComparison.OrdinalIgnoreCase));
			return name != null ? asm.GetManifestResourceStream(name) : null;
		}

		private static void TrySetPlayerCode(string playerJsonPath, string serverPlayerCode, MelonLogger.Instance logger)
		{
			try
			{
				if (!File.Exists(playerJsonPath))
					return;
				var data = ReadJsonData<PlayerData>(playerJsonPath);
				if (data == null)
					return;
				if (!string.IsNullOrWhiteSpace(serverPlayerCode))
				{
					data.PlayerCode = serverPlayerCode;
					File.WriteAllText(playerJsonPath, data.GetJson());
					logger?.Msg("Set loopback PlayerCode in Player.json");
				}
			}
			catch (Exception ex)
			{
				logger?.Warning($"Failed to set PlayerCode in Player.json: {ex.Message}");
			}
		}

		private static T ReadJsonData<T>(string jsonFilePath) where T : class
		{
			try
			{
				if (!File.Exists(jsonFilePath))
					return null;
				var fileText = File.ReadAllText(jsonFilePath);
				return string.IsNullOrEmpty(fileText) ? null : JsonUtility.FromJson<T>(fileText);
			}
			catch
			{
				return null;
			}
		}
	}
}


