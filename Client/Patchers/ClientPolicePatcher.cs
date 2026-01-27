using System;
using System.Linq;
using System.Reflection;
using FishNet;
using FishNet.Connection;
using HarmonyLib;
using MelonLoader;

namespace DedicatedServerMod.Client.Patchers
{
	/// <summary>
	/// Guards RunLocally Observers RPC wrappers in police-related classes so that on clients
	/// we execute only the local logic and do not attempt to send Observers RPCs (which the server should do).
	/// This removes client log spam and prevents unintended client-side network sends.
	/// </summary>
	internal static class ClientPolicePatcher
	{
		private static MelonLogger.Instance _logger;

		public static void Apply(HarmonyLib.Harmony harmony, MelonLogger.Instance logger)
		{
			_logger = logger;
			// Delegate to shared implementation to reduce duplication.
			DedicatedServerMod.Shared.PoliceAuthorityPatches.ApplyClient(harmony, logger);
		}

		private static void PatchPoliceOfficer(HarmonyLib.Harmony harmony)
		{
			var type = AccessTools.TypeByName("ScheduleOne.Police.PoliceOfficer");
			if (type == null)
			{
				_logger?.Warning("Client patch: PoliceOfficer type not found; skipping police RPC guards.");
				return;
			}

			TryPatch(harmony, type, "BeginFootPursuitTest", new Type[] { typeof(string) }, typeof(ClientPolicePatcher), nameof(Prefix_BeginFootPursuitTest));
			TryPatch(harmony, type, "BeginVehiclePursuit", new Type[] { AccessTools.TypeByName("FishNet.Object.NetworkObject"), AccessTools.TypeByName("FishNet.Object.NetworkObject"), typeof(bool) }, typeof(ClientPolicePatcher), nameof(Prefix_BeginVehiclePursuit));
			TryPatch(harmony, type, "BeginBodySearch", new Type[] { AccessTools.TypeByName("FishNet.Object.NetworkObject") }, typeof(ClientPolicePatcher), nameof(Prefix_BeginBodySearch));
			TryPatch(harmony, type, "AssignToCheckpoint", new Type[] { AccessTools.TypeByName("ScheduleOne.Law.CheckpointManager+ECheckpointLocation") }, typeof(ClientPolicePatcher), nameof(Prefix_AssignToCheckpoint));
		}

		private static void PatchRoadCheckpoint(HarmonyLib.Harmony harmony)
		{
			var type = AccessTools.TypeByName("ScheduleOne.Police.RoadCheckpoint");
			if (type == null)
			{
				return;
			}
			TryPatch(harmony, type, "Enable", new Type[] { typeof(NetworkConnection) }, typeof(ClientPolicePatcher), nameof(Prefix_RoadCheckpoint_Enable));
			TryPatch(harmony, type, "Disable", Type.EmptyTypes, typeof(ClientPolicePatcher), nameof(Prefix_RoadCheckpoint_Disable));
		}

		private static void PatchCallPoliceBehaviour(HarmonyLib.Harmony harmony)
		{
			var type = AccessTools.TypeByName("ScheduleOne.NPCs.Behaviour.CallPoliceBehaviour");
			if (type == null)
			{
				return;
			}
			TryPatch(harmony, type, "FinalizeCall", Type.EmptyTypes, typeof(ClientPolicePatcher), nameof(Prefix_CallPolice_FinalizeCall));
		}

		private static void TryPatch(HarmonyLib.Harmony harmony, Type declaringType, string methodName, Type[] parameters, Type patchType, string prefixName)
		{
			try
			{
				var original = declaringType.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, parameters, null);
				if (original == null)
				{
					_logger?.Warning($"Client patch: Could not find {declaringType.FullName}.{methodName}({string.Join(",", parameters.Select(p => p?.Name ?? "null"))})");
					return;
				}
				var prefix = new HarmonyMethod(patchType.GetMethod(prefixName, BindingFlags.Static | BindingFlags.NonPublic));
				harmony.Patch(original, prefix: prefix);
			}
			catch (Exception ex)
			{
				_logger?.Error($"Client patch failed for {declaringType.FullName}.{methodName}: {ex}");
			}
		}

		// Legacy per-method observer guards removed in favor of shared implementation

		// Prefixes

		private static bool Prefix_BeginFootPursuitTest(object __instance, string playerCode)
		{
			if (InstanceFinder.IsServer)
			{
				return true; // allow server to run original (broadcast + logic)
			}
			// Client: run only the logic method, skip writer/broadcast
			InvokeLogic(__instance, "RpcLogic___BeginFootPursuitTest_", new object[] { playerCode });
			return false;
		}

		private static bool Prefix_BeginVehiclePursuit(object __instance, object target, object vehicle, bool beginAsSighted)
		{
			if (InstanceFinder.IsServer)
			{
				return true;
			}
			InvokeLogic(__instance, "RpcLogic___BeginVehiclePursuit_", new object[] { target, vehicle, beginAsSighted });
			return false;
		}

		private static bool Prefix_BeginBodySearch(object __instance, object target)
		{
			if (InstanceFinder.IsServer)
			{
				return true;
			}
			InvokeLogic(__instance, "RpcLogic___BeginBodySearch_", new object[] { target });
			return false;
		}

		private static bool Prefix_AssignToCheckpoint(object __instance, object location)
		{
			if (InstanceFinder.IsServer)
			{
				return true;
			}
			InvokeLogic(__instance, "RpcLogic___AssignToCheckpoint_", new object[] { location });
			return false;
		}

		private static bool Prefix_RoadCheckpoint_Enable(object __instance, NetworkConnection __0)
		{
			if (InstanceFinder.IsServer)
			{
				return true;
			}
			InvokeLogic(__instance, "RpcLogic___Enable_", new object[] { null });
			return false;
		}

		private static bool Prefix_RoadCheckpoint_Disable(object __instance)
		{
			if (InstanceFinder.IsServer)
			{
				return true;
			}
			InvokeLogic(__instance, "RpcLogic___Disable_", Array.Empty<object>());
			return false;
		}

		private static bool Prefix_CallPolice_FinalizeCall(object __instance)
		{
			if (InstanceFinder.IsServer)
			{
				return true;
			}
			InvokeLogic(__instance, "RpcLogic___FinalizeCall_", Array.Empty<object>());
			return false;
		}

		private static void InvokeLogic(object instance, string logicPrefix, object[] args)
		{
			try
			{
				var type = instance.GetType();
				var method = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
					.FirstOrDefault(m => m.Name.StartsWith(logicPrefix, StringComparison.Ordinal));
				if (method == null)
				{
					_logger?.Warning($"Client patch: Could not find logic method with prefix '{logicPrefix}' on {type.FullName}");
					return;
				}
				method.Invoke(instance, args);
			}
			catch (Exception ex)
			{
				_logger?.Error($"Client patch: Exception invoking logic '{logicPrefix}*': {ex}");
			}
		}
	}
}


