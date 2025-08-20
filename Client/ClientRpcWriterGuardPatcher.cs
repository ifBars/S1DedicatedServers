using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using FishNet;

namespace DedicatedServerMod.Client
{
	/// <summary>
	/// Global client-side guard for all RpcWriter___Observers_* wrappers across the game.
	/// Prevents clients from attempting to send server-only Observers RPCs, which spam logs when the server isn't active.
	/// Still allows the paired logic methods (RunLocally) to execute normally.
	/// </summary>
	internal static class ClientRpcWriterGuardPatcher
	{
		private static MelonLogger.Instance _logger;

		public static void Apply(HarmonyLib.Harmony harmony, MelonLogger.Instance logger)
		{
			_logger = logger;
			try
			{
				var assemblies = AppDomain.CurrentDomain.GetAssemblies()
					.Where(a => a.GetName().Name == "Assembly-CSharp")
					.ToArray();

				int total = 0;
				var prefix = new HarmonyMethod(typeof(ClientRpcWriterGuardPatcher).GetMethod(nameof(Prefix_BlockObserverWriterOnClient), BindingFlags.Static | BindingFlags.NonPublic));
				foreach (var asm in assemblies)
				{
					Type[] types;
					try { types = asm.GetTypes(); }
					catch (ReflectionTypeLoadException rtle) { types = rtle.Types.Where(t => t != null).ToArray(); }
					foreach (var t in types)
					{
						MethodInfo[] methods;
						try { methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance); }
						catch { continue; }
						foreach (var mi in methods)
						{
							if (!mi.Name.StartsWith("RpcWriter___Observers_", StringComparison.Ordinal))
								continue;
							try
							{
								harmony.Patch(mi, prefix: prefix);
								total++;
							}
							catch (Exception ex)
							{
								_logger?.Warning($"Client RPC writer guard: Failed to patch {t.FullName}.{mi.Name}: {ex.Message}");
							}
						}
					}
				}

				_logger?.Msg($"Client RPC writer guards applied: {total} methods");
			}
			catch (Exception ex)
			{
				_logger?.Error($"Client RPC writer guard install failed: {ex}");
			}
		}

		private static int _debugBlockedCount = 0;
		private const int DebugBlockLogLimit = 10;

		private static bool Prefix_BlockObserverWriterOnClient(System.Reflection.MethodBase __originalMethod)
		{
			// Only allow the writer to run on server; on clients skip it to avoid spam and invalid sends.
			bool allow = InstanceFinder.IsServer;
			if (!allow && _debugBlockedCount < DebugBlockLogLimit)
			{
				_logger?.Msg($"Client blocked Observers writer: {__originalMethod?.DeclaringType?.FullName}.{__originalMethod?.Name}");
				_debugBlockedCount++;
			}
			return allow;
		}
	}
}


