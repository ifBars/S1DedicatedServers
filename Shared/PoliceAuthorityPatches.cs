using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using FishNet;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Police;
using ScheduleOne.Law;
using ScheduleOne.Vehicles;
using ScheduleOne.NPCs;

namespace DedicatedServerMod.Shared
{
	/// <summary>
	/// Centralized Harmony patches to make police behavior authoritative on the server
	/// and to suppress client-side observer RPC writes. This addresses ownership checks
	/// which break on dedicated servers where the server owns player objects.
	/// </summary>
	public static class PoliceAuthorityPatches
	{
		public static void ApplyServer(HarmonyLib.Harmony harmony, MelonLogger.Instance logger)
		{
			try
			{
				var npcRespType = typeof(NPCResponses_Police);
				// Patch each response that previously gated on player.IsOwner
				PatchPostfix(harmony, npcRespType, nameof(NPCResponses_Police.NoticedDrugDeal), new Type[] { typeof(Player) }, typeof(ServerPostfixes), nameof(ServerPostfixes.NoticedDrugDeal_Postfix));
				PatchPostfix(harmony, npcRespType, nameof(NPCResponses_Police.NoticedPettyCrime), new Type[] { typeof(Player) }, typeof(ServerPostfixes), nameof(ServerPostfixes.NoticedPettyCrime_Postfix));
				PatchPostfix(harmony, npcRespType, nameof(NPCResponses_Police.NoticedVandalism), new Type[] { typeof(Player) }, typeof(ServerPostfixes), nameof(ServerPostfixes.NoticedVandalism_Postfix));
				PatchPostfix(harmony, npcRespType, nameof(NPCResponses_Police.SawPickpocketing), new Type[] { typeof(Player) }, typeof(ServerPostfixes), nameof(ServerPostfixes.SawPickpocketing_Postfix));
				PatchPostfix(harmony, npcRespType, nameof(NPCResponses_Police.PlayerFailedPickpocket), new Type[] { typeof(Player) }, typeof(ServerPostfixes), nameof(ServerPostfixes.PlayerFailedPickpocket_Postfix));
				PatchPostfix(harmony, npcRespType, nameof(NPCResponses_Police.NoticePlayerBrandishingWeapon), new Type[] { typeof(Player) }, typeof(ServerPostfixes), nameof(ServerPostfixes.NoticePlayerBrandishingWeapon_Postfix));
				PatchPostfix(harmony, npcRespType, nameof(NPCResponses_Police.NoticePlayerDischargingWeapon), new Type[] { typeof(Player) }, typeof(ServerPostfixes), nameof(ServerPostfixes.NoticePlayerDischargingWeapon_Postfix));
				PatchPostfix(harmony, npcRespType, nameof(NPCResponses_Police.NoticedWantedPlayer), new Type[] { typeof(Player) }, typeof(ServerPostfixes), nameof(ServerPostfixes.NoticedWantedPlayer_Postfix));
				PatchPostfix(harmony, npcRespType, nameof(NPCResponses_Police.NoticedSuspiciousPlayer), new Type[] { typeof(Player) }, typeof(ServerPostfixes), nameof(ServerPostfixes.NoticedSuspiciousPlayer_Postfix));
				PatchPostfix(harmony, npcRespType, nameof(NPCResponses_Police.NoticedViolatingCurfew), new Type[] { typeof(Player) }, typeof(ServerPostfixes), nameof(ServerPostfixes.NoticedViolatingCurfew_Postfix));
				PatchPostfix(harmony, npcRespType, nameof(NPCResponses_Police.HitByCar), new Type[] { typeof(LandVehicle) }, typeof(ServerPostfixes), nameof(ServerPostfixes.HitByCar_Postfix));
				logger?.Msg("Server police authority patches applied");
			}
			catch (Exception ex)
			{
				logger?.Error($"Failed to apply server police patches: {ex}");
			}
		}

		public static void ApplyClient(HarmonyLib.Harmony harmony, MelonLogger.Instance logger)
		{
			try
			{
				// Guard all Observers RPC writers on client for police-related types
				GuardObserverWriters(harmony, typeof(PoliceOfficer), logger);
				GuardObserverWriters(harmony, AccessTools.TypeByName("ScheduleOne.Police.RoadCheckpoint"), logger);
				GuardObserverWriters(harmony, AccessTools.TypeByName("ScheduleOne.NPCs.Behaviour.CallPoliceBehaviour"), logger);
				logger?.Msg("Client observer-writer guards for police types applied");
			}
			catch (Exception ex)
			{
				logger?.Error($"Failed to apply client police patches: {ex}");
			}
		}

		private static void PatchPostfix(HarmonyLib.Harmony harmony, Type declaringType, string methodName, Type[] parameters, Type patchType, string postfixName)
		{
			var mi = declaringType.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, parameters, null);
			if (mi == null) return;
			var postfix = new HarmonyMethod(patchType.GetMethod(postfixName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static));
			harmony.Patch(mi, postfix: postfix);
		}

		private static void GuardObserverWriters(HarmonyLib.Harmony harmony, Type type, MelonLogger.Instance logger)
		{
			if (type == null) return;
			var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			var prefix = new HarmonyMethod(typeof(ClientPrefixes).GetMethod(nameof(ClientPrefixes.BlockObserverWriterOnClient), BindingFlags.Static | BindingFlags.Public));
			int count = 0;
			foreach (var m in methods)
			{
				if (!m.Name.StartsWith("RpcWriter___Observers_", StringComparison.Ordinal)) continue;
				try { harmony.Patch(m, prefix: prefix); count++; }
				catch (Exception ex) { logger?.Warning($"Failed to guard {type.FullName}.{m.Name}: {ex.Message}"); }
			}
			if (count > 0) logger?.Msg($"Guarded {count} Observers RPC writers on {type.FullName}");
		}

		public static class ClientPrefixes
		{
			public static bool BlockObserverWriterOnClient(object __instance)
			{
				// Only server should send Observers RPCs
				return InstanceFinder.IsServer;
			}
		}

		private static class ServerPostfixes
		{
			private static PoliceOfficer GetOfficer(NPCResponses_Police instance)
			{
				try
				{
					// NPCResponses has a protected field 'npc'
					var baseType = instance.GetType().BaseType; // NPCResponses
					var f = baseType.GetField("npc", BindingFlags.NonPublic | BindingFlags.Instance);
					var npc = f?.GetValue(instance) as NPC;
					return npc as PoliceOfficer;
				}
				catch { return null; }
			}

			private static bool ShouldRunFor(Player player)
			{
				return InstanceFinder.IsServer && player != null && !player.IsOwner;
			}

			public static void NoticedDrugDeal_Postfix(NPCResponses_Police __instance, Player player)
			{
				if (!ShouldRunFor(player)) return;
				player.CrimeData.AddCrime(new DrugTrafficking());
				player.CrimeData.SetPursuitLevel(PlayerCrimeData.EPursuitLevel.Arresting);
				GetOfficer(__instance)?.BeginFootPursuit_Networked(player.PlayerCode);
			}

			public static void NoticedPettyCrime_Postfix(NPCResponses_Police __instance, Player player)
			{
				if (!ShouldRunFor(player)) return;
				player.CrimeData.SetPursuitLevel(PlayerCrimeData.EPursuitLevel.Arresting);
				GetOfficer(__instance)?.BeginFootPursuit_Networked(player.PlayerCode);
			}

			public static void NoticedVandalism_Postfix(NPCResponses_Police __instance, Player player)
			{
				if (!ShouldRunFor(player)) return;
				player.CrimeData.AddCrime(new Vandalism());
				player.CrimeData.SetPursuitLevel(PlayerCrimeData.EPursuitLevel.Arresting);
				GetOfficer(__instance)?.BeginFootPursuit_Networked(player.PlayerCode);
			}

			public static void SawPickpocketing_Postfix(NPCResponses_Police __instance, Player player)
			{
				if (!ShouldRunFor(player)) return;
				player.CrimeData.AddCrime(new Theft());
				player.CrimeData.SetPursuitLevel(PlayerCrimeData.EPursuitLevel.Arresting);
				GetOfficer(__instance)?.BeginFootPursuit_Networked(player.PlayerCode);
			}

			public static void PlayerFailedPickpocket_Postfix(NPCResponses_Police __instance, Player player)
			{
				if (!ShouldRunFor(player)) return;
				player.CrimeData.AddCrime(new Theft());
				player.CrimeData.SetPursuitLevel(PlayerCrimeData.EPursuitLevel.Arresting);
				GetOfficer(__instance)?.BeginFootPursuit_Networked(player.PlayerCode);
			}

			public static void NoticePlayerBrandishingWeapon_Postfix(NPCResponses_Police __instance, Player player)
			{
				if (!ShouldRunFor(player)) return;
				player.CrimeData.AddCrime(new BrandishingWeapon());
				player.CrimeData.SetPursuitLevel(PlayerCrimeData.EPursuitLevel.NonLethal);
				GetOfficer(__instance)?.BeginFootPursuit_Networked(player.PlayerCode);
			}

			public static void NoticePlayerDischargingWeapon_Postfix(NPCResponses_Police __instance, Player player)
			{
				if (!ShouldRunFor(player)) return;
				player.CrimeData.AddCrime(new DischargeFirearm());
				player.CrimeData.SetPursuitLevel(PlayerCrimeData.EPursuitLevel.NonLethal);
				GetOfficer(__instance)?.BeginFootPursuit_Networked(player.PlayerCode);
			}

			public static void NoticedWantedPlayer_Postfix(NPCResponses_Police __instance, Player player)
			{
				if (!ShouldRunFor(player)) return;
				var officer = GetOfficer(__instance);
				player.CrimeData.RecordLastKnownPosition(true);
				if (officer?.CurrentVehicle != null)
				{
					officer.BeginFootPursuit_Networked(player.PlayerCode, includeColleagues: false);
					officer.BeginVehiclePursuit_Networked(player.PlayerCode, officer.CurrentVehicle.NetworkObject, beginAsSighted: true);
				}
				else
				{
					officer?.BeginFootPursuit_Networked(player.PlayerCode);
				}
			}

			public static void NoticedSuspiciousPlayer_Postfix(NPCResponses_Police __instance, Player player)
			{
				if (!ShouldRunFor(player)) return;
				GetOfficer(__instance)?.BeginBodySearch_Networked(player.PlayerCode);
			}

			public static void NoticedViolatingCurfew_Postfix(NPCResponses_Police __instance, Player player)
			{
				if (!ShouldRunFor(player)) return;
				var officer = GetOfficer(__instance);
				player.CrimeData.AddCrime(new ViolatingCurfew());
				player.CrimeData.SetPursuitLevel(PlayerCrimeData.EPursuitLevel.Arresting);
				if (officer?.CurrentVehicle != null)
				{
					officer.BeginFootPursuit_Networked(player.PlayerCode, includeColleagues: false);
					officer.BeginVehiclePursuit_Networked(player.PlayerCode, officer.CurrentVehicle.NetworkObject, beginAsSighted: true);
				}
				else
				{
					officer?.BeginFootPursuit_Networked(player.PlayerCode);
				}
			}

			public static void HitByCar_Postfix(NPCResponses_Police __instance, LandVehicle vehicle)
			{
				if (!InstanceFinder.IsServer) return;
				var driver = vehicle?.DriverPlayer;
				if (driver == null || driver.IsOwner) return; // already handled in original when IsOwner
				driver.CrimeData.AddCrime(new VehicularAssault());
				if (driver.CrimeData.CurrentPursuitLevel == PlayerCrimeData.EPursuitLevel.None)
					driver.CrimeData.SetPursuitLevel(PlayerCrimeData.EPursuitLevel.NonLethal);
				else
					driver.CrimeData.Escalate();
			}
		}
	}
}


