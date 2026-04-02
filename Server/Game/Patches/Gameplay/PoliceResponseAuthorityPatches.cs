using DedicatedServerMod.Server.Game.Patches.Common;
using HarmonyLib;
#if IL2CPP
using InstanceFinderType = Il2CppFishNet.InstanceFinder;
using BrandishingWeaponType = Il2CppScheduleOne.Law.BrandishingWeapon;
using DischargeFirearmType = Il2CppScheduleOne.Law.DischargeFirearm;
using DrugTraffickingType = Il2CppScheduleOne.Law.DrugTrafficking;
using NpcResponsesPoliceType = Il2CppScheduleOne.Police.NPCResponses_Police;
using PlayerCrimeDataType = Il2CppScheduleOne.PlayerScripts.PlayerCrimeData;
using PlayerType = Il2CppScheduleOne.PlayerScripts.Player;
using PoliceOfficerType = Il2CppScheduleOne.Police.PoliceOfficer;
using LandVehicleType = Il2CppScheduleOne.Vehicles.LandVehicle;
using TheftType = Il2CppScheduleOne.Law.Theft;
using VandalismType = Il2CppScheduleOne.Law.Vandalism;
using VehicularAssaultType = Il2CppScheduleOne.Law.VehicularAssault;
using ViolatingCurfewType = Il2CppScheduleOne.Law.ViolatingCurfew;
#else
using FishNet;
using InstanceFinderType = FishNet.InstanceFinder;
using BrandishingWeaponType = ScheduleOne.Law.BrandishingWeapon;
using DischargeFirearmType = ScheduleOne.Law.DischargeFirearm;
using DrugTraffickingType = ScheduleOne.Law.DrugTrafficking;
using NpcResponsesPoliceType = ScheduleOne.Police.NPCResponses_Police;
using PlayerCrimeDataType = ScheduleOne.PlayerScripts.PlayerCrimeData;
using PlayerType = ScheduleOne.PlayerScripts.Player;
using PoliceOfficerType = ScheduleOne.Police.PoliceOfficer;
using LandVehicleType = ScheduleOne.Vehicles.LandVehicle;
using TheftType = ScheduleOne.Law.Theft;
using VandalismType = ScheduleOne.Law.Vandalism;
using VehicularAssaultType = ScheduleOne.Law.VehicularAssault;
using ViolatingCurfewType = ScheduleOne.Law.ViolatingCurfew;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Gameplay
{
    internal static class DedicatedPoliceResponseAuthority
    {
        internal static bool TryGetOfficer(NpcResponsesPoliceType responses, out PoliceOfficerType officer)
        {
            officer = responses?.GetComponentInParent<PoliceOfficerType>();
            return officer != null;
        }

        internal static bool ShouldHandle(PlayerType player, PoliceOfficerType officer)
        {
            return DedicatedServerPatchCommon.IsDedicatedHeadlessServer()
                && InstanceFinderType.IsServer
                && player != null
                && player.CrimeData != null
                && !player.IsOwner
                && !DedicatedServerPatchCommon.IsGhostOrLoopbackPlayer(player)
                && officer != null
                && !officer.IgnorePlayers;
        }

        internal static void RaisePursuitTo(PlayerType player, PlayerCrimeDataType.EPursuitLevel minimumLevel)
        {
            if (player.CrimeData.CurrentPursuitLevel < minimumLevel)
            {
                player.CrimeData.SetPursuitLevel(minimumLevel);
            }
        }

        internal static void BeginFootPursuit(PoliceOfficerType officer, PlayerType player)
        {
            officer.BeginFootPursuit_Networked(player.PlayerCode);
        }

        internal static void BeginWantedPursuit(PoliceOfficerType officer, PlayerType player)
        {
            if (officer.CurrentVehicle != null)
            {
                officer.BeginFootPursuit_Networked(player.PlayerCode, includeColleagues: false);
                officer.BeginVehiclePursuit_Networked(player.PlayerCode, officer.CurrentVehicle.NetworkObject, beginAsSighted: true);
            }
            else
            {
                officer.BeginFootPursuit_Networked(player.PlayerCode);
            }
        }

        internal static void BeginCurfewPursuit(PoliceOfficerType officer, PlayerType player)
        {
            if (officer.CurrentVehicle != null)
            {
                officer.BeginFootPursuit_Networked(player.PlayerCode, includeColleagues: false);
                officer.BeginVehiclePursuit_Networked(player.PlayerCode, officer.CurrentVehicle.NetworkObject, beginAsSighted: true);
            }
            else
            {
                officer.BeginFootPursuit_Networked(player.PlayerCode);
            }
        }
    }

    /// <summary>
    /// Mirrors owner-gated police response transitions for dedicated server authority.
    /// Vanilla police responses only escalate pursuit for Player.IsOwner targets.
    /// </summary>
    [HarmonyPatch(typeof(NpcResponsesPoliceType), "NoticedPettyCrime")]
    internal static class PoliceResponseNoticedPettyCrimePatches
    {
        private static void Postfix(NpcResponsesPoliceType __instance, PlayerType player)
        {
            if (!DedicatedPoliceResponseAuthority.TryGetOfficer(__instance, out PoliceOfficerType officer)
                || !DedicatedPoliceResponseAuthority.ShouldHandle(player, officer))
            {
                return;
            }

            DedicatedPoliceResponseAuthority.RaisePursuitTo(player, PlayerCrimeDataType.EPursuitLevel.Arresting);
            DedicatedPoliceResponseAuthority.BeginFootPursuit(officer, player);
        }
    }

    [HarmonyPatch(typeof(NpcResponsesPoliceType), "NoticedDrugDeal")]
    internal static class PoliceResponseNoticedDrugDealPatches
    {
        private static void Postfix(NpcResponsesPoliceType __instance, PlayerType player)
        {
            if (!DedicatedPoliceResponseAuthority.TryGetOfficer(__instance, out PoliceOfficerType officer)
                || !DedicatedPoliceResponseAuthority.ShouldHandle(player, officer))
            {
                return;
            }

            player.CrimeData.AddCrime(new DrugTraffickingType());
            DedicatedPoliceResponseAuthority.RaisePursuitTo(player, PlayerCrimeDataType.EPursuitLevel.Arresting);
            DedicatedPoliceResponseAuthority.BeginFootPursuit(officer, player);
        }
    }

    [HarmonyPatch(typeof(NpcResponsesPoliceType), "NoticedVandalism")]
    internal static class PoliceResponseNoticedVandalismPatches
    {
        private static void Postfix(NpcResponsesPoliceType __instance, PlayerType player)
        {
            if (!DedicatedPoliceResponseAuthority.TryGetOfficer(__instance, out PoliceOfficerType officer)
                || !DedicatedPoliceResponseAuthority.ShouldHandle(player, officer))
            {
                return;
            }

            player.CrimeData.AddCrime(new VandalismType());
            DedicatedPoliceResponseAuthority.RaisePursuitTo(player, PlayerCrimeDataType.EPursuitLevel.Arresting);
            DedicatedPoliceResponseAuthority.BeginFootPursuit(officer, player);
        }
    }

    [HarmonyPatch(typeof(NpcResponsesPoliceType), "SawPickpocketing")]
    internal static class PoliceResponseSawPickpocketingPatches
    {
        private static void Postfix(NpcResponsesPoliceType __instance, PlayerType player)
        {
            if (!DedicatedPoliceResponseAuthority.TryGetOfficer(__instance, out PoliceOfficerType officer)
                || !DedicatedPoliceResponseAuthority.ShouldHandle(player, officer))
            {
                return;
            }

            player.CrimeData.AddCrime(new TheftType());
            DedicatedPoliceResponseAuthority.RaisePursuitTo(player, PlayerCrimeDataType.EPursuitLevel.Arresting);
            DedicatedPoliceResponseAuthority.BeginFootPursuit(officer, player);
        }
    }

    [HarmonyPatch(typeof(NpcResponsesPoliceType), "PlayerFailedPickpocket")]
    internal static class PoliceResponsePlayerFailedPickpocketPatches
    {
        private static void Postfix(NpcResponsesPoliceType __instance, PlayerType player)
        {
            if (!DedicatedPoliceResponseAuthority.TryGetOfficer(__instance, out PoliceOfficerType officer)
                || !DedicatedPoliceResponseAuthority.ShouldHandle(player, officer))
            {
                return;
            }

            player.CrimeData.AddCrime(new TheftType());
            DedicatedPoliceResponseAuthority.RaisePursuitTo(player, PlayerCrimeDataType.EPursuitLevel.Arresting);
            DedicatedPoliceResponseAuthority.BeginFootPursuit(officer, player);
        }
    }

    [HarmonyPatch(typeof(NpcResponsesPoliceType), "NoticePlayerBrandishingWeapon")]
    internal static class PoliceResponseNoticePlayerBrandishingWeaponPatches
    {
        private static void Postfix(NpcResponsesPoliceType __instance, PlayerType player)
        {
            if (!DedicatedPoliceResponseAuthority.TryGetOfficer(__instance, out PoliceOfficerType officer)
                || !DedicatedPoliceResponseAuthority.ShouldHandle(player, officer))
            {
                return;
            }

            player.CrimeData.AddCrime(new BrandishingWeaponType());
            DedicatedPoliceResponseAuthority.RaisePursuitTo(player, PlayerCrimeDataType.EPursuitLevel.NonLethal);
            DedicatedPoliceResponseAuthority.BeginFootPursuit(officer, player);
        }
    }

    [HarmonyPatch(typeof(NpcResponsesPoliceType), "NoticePlayerDischargingWeapon")]
    internal static class PoliceResponseNoticePlayerDischargingWeaponPatches
    {
        private static void Postfix(NpcResponsesPoliceType __instance, PlayerType player)
        {
            if (!DedicatedPoliceResponseAuthority.TryGetOfficer(__instance, out PoliceOfficerType officer)
                || !DedicatedPoliceResponseAuthority.ShouldHandle(player, officer))
            {
                return;
            }

            player.CrimeData.AddCrime(new DischargeFirearmType());
            DedicatedPoliceResponseAuthority.RaisePursuitTo(player, PlayerCrimeDataType.EPursuitLevel.NonLethal);
            DedicatedPoliceResponseAuthority.BeginFootPursuit(officer, player);
        }
    }

    [HarmonyPatch(typeof(NpcResponsesPoliceType), "NoticedWantedPlayer")]
    internal static class PoliceResponseNoticedWantedPlayerPatches
    {
        private static void Postfix(NpcResponsesPoliceType __instance, PlayerType player)
        {
            if (!DedicatedPoliceResponseAuthority.TryGetOfficer(__instance, out PoliceOfficerType officer)
                || !DedicatedPoliceResponseAuthority.ShouldHandle(player, officer))
            {
                return;
            }

            player.CrimeData.RecordLastKnownPosition(resetTimeSinceSighted: true);
            DedicatedPoliceResponseAuthority.BeginWantedPursuit(officer, player);
        }
    }

    [HarmonyPatch(typeof(NpcResponsesPoliceType), "NoticedSuspiciousPlayer")]
    internal static class PoliceResponseNoticedSuspiciousPlayerPatches
    {
        private static void Postfix(NpcResponsesPoliceType __instance, PlayerType player)
        {
            if (!DedicatedPoliceResponseAuthority.TryGetOfficer(__instance, out PoliceOfficerType officer)
                || !DedicatedPoliceResponseAuthority.ShouldHandle(player, officer))
            {
                return;
            }

            officer.BeginBodySearch_Networked(player.PlayerCode);
        }
    }

    [HarmonyPatch(typeof(NpcResponsesPoliceType), "NoticedViolatingCurfew")]
    internal static class PoliceResponseNoticedViolatingCurfewPatches
    {
        private static void Postfix(NpcResponsesPoliceType __instance, PlayerType player)
        {
            if (!DedicatedPoliceResponseAuthority.TryGetOfficer(__instance, out PoliceOfficerType officer)
                || !DedicatedPoliceResponseAuthority.ShouldHandle(player, officer))
            {
                return;
            }

            player.CrimeData.AddCrime(new ViolatingCurfewType());
            DedicatedPoliceResponseAuthority.RaisePursuitTo(player, PlayerCrimeDataType.EPursuitLevel.Arresting);
            DedicatedPoliceResponseAuthority.BeginCurfewPursuit(officer, player);
        }
    }

    [HarmonyPatch(typeof(NpcResponsesPoliceType), "HitByCar")]
    internal static class PoliceResponseHitByCarPatches
    {
        private static void Postfix(NpcResponsesPoliceType __instance, LandVehicleType vehicle)
        {
            PlayerType player = vehicle?.DriverPlayer;
            if (!DedicatedPoliceResponseAuthority.TryGetOfficer(__instance, out PoliceOfficerType officer)
                || !DedicatedPoliceResponseAuthority.ShouldHandle(player, officer))
            {
                return;
            }

            player.CrimeData.AddCrime(new VehicularAssaultType());
            if (player.CrimeData.CurrentPursuitLevel == PlayerCrimeDataType.EPursuitLevel.None)
            {
                player.CrimeData.SetPursuitLevel(PlayerCrimeDataType.EPursuitLevel.NonLethal);
            }
            else
            {
                player.CrimeData.Escalate();
            }
        }
    }
}
