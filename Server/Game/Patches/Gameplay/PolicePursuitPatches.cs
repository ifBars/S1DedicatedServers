using DedicatedServerMod.Server.Game.Patches.Common;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
#if IL2CPP
using InstanceFinderType = Il2CppFishNet.InstanceFinder;
using CombatBehaviourType = Il2CppScheduleOne.Combat.CombatBehaviour;
using DriveFlagsType = Il2CppScheduleOne.Vehicles.AI.DriveFlags;
using PoliceStationType = Il2CppScheduleOne.Map.PoliceStation;
using PoliceOfficerType = Il2CppScheduleOne.Police.PoliceOfficer;
using PlayerCrimeDataType = Il2CppScheduleOne.PlayerScripts.PlayerCrimeData;
using PlayerType = Il2CppScheduleOne.PlayerScripts.Player;
using PursuitBehaviourType = Il2CppScheduleOne.NPCs.Behaviour.PursuitBehaviour;
using VehiclePursuitBehaviourType = Il2CppScheduleOne.NPCs.Behaviour.VehiclePursuitBehaviour;
using VisionEventReceiptType = Il2CppScheduleOne.Vision.VisionEventReceipt;
#else
using FishNet;
using CombatBehaviourType = ScheduleOne.Combat.CombatBehaviour;
using DriveFlagsType = ScheduleOne.Vehicles.AI.DriveFlags;
using PoliceStationType = ScheduleOne.Map.PoliceStation;
using PoliceOfficerType = ScheduleOne.Police.PoliceOfficer;
using PlayerCrimeDataType = ScheduleOne.PlayerScripts.PlayerCrimeData;
using PlayerType = ScheduleOne.PlayerScripts.Player;
using PursuitBehaviourType = ScheduleOne.NPCs.Behaviour.PursuitBehaviour;
using VehiclePursuitBehaviourType = ScheduleOne.NPCs.Behaviour.VehiclePursuitBehaviour;
using VisionEventReceiptType = ScheduleOne.Vision.VisionEventReceipt;
using InstanceFinderType = FishNet.InstanceFinder;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Gameplay
{
    internal static class DedicatedPolicePursuitAuthority
    {
        private static readonly MethodInfo CombatIsTargetVisibleThisFrameMethod = AccessTools.Method(typeof(CombatBehaviourType), "IsTargetVisibleThisFrame");
        private static readonly MethodInfo VehicleIsTargetVisibleMethod = AccessTools.Method(typeof(VehiclePursuitBehaviourType), "IsTargetVisible");

        internal static bool ShouldRunForPlayer(PlayerType player)
        {
            return DedicatedServerPatchCommon.IsDedicatedHeadlessServer()
                && InstanceFinderType.IsServer
                && player != null
                && !DedicatedServerPatchCommon.IsGhostOrLoopbackPlayer(player);
        }

        internal static bool ShouldRunServerAuthority(PlayerCrimeDataType crimeData)
        {
            return crimeData != null
                && ShouldRunForPlayer(crimeData.Player)
                && !crimeData.Player.IsOwner;
        }

        internal static bool IsInvalidPursuitTarget(PlayerType player)
        {
            return player == null
                || player.CrimeData == null
                || player.IsArrested
                || player.IsUnconscious
                || player.CrimeData.CurrentPursuitLevel == PlayerCrimeDataType.EPursuitLevel.None;
        }

        internal static void ClearPoliceTargeting(PlayerType player)
        {
            if (!ShouldRunForPlayer(player) || player.NetworkObject == null)
            {
                return;
            }

            for (int i = 0; i < PoliceOfficerType.Officers.Count; i++)
            {
                PoliceOfficerType officer = PoliceOfficerType.Officers[i];
                if (officer == null)
                {
                    continue;
                }

                if (officer.BodySearchBehaviour != null
                    && officer.BodySearchBehaviour.Enabled
                    && officer.BodySearchBehaviour.TargetPlayer == player)
                {
                    officer.BodySearchBehaviour.Disable_Networked(null);
                }

                if (officer.PursuitBehaviour != null
                    && officer.PursuitBehaviour.Enabled
                    && officer.PursuitBehaviour.TargetPlayer == player)
                {
                    officer.PursuitBehaviour.Disable_Networked(null);
                }

                if (officer.VehiclePursuitBehaviour != null
                    && officer.VehiclePursuitBehaviour.Enabled
                    && officer.VehiclePursuitBehaviour.Target == player)
                {
                    officer.VehiclePursuitBehaviour.Disable_Networked(null);
                }
            }
        }

        internal static void RefreshServerSight(PlayerType player)
        {
            if (!ShouldRunForPlayer(player) || player.CrimeData == null)
            {
                return;
            }

            player.CrimeData.RecordLastKnownPosition(resetTimeSinceSighted: true);
        }

        internal static bool IsTargetVisible(PursuitBehaviourType pursuit)
        {
            return InvokeVisibilityProbe(CombatIsTargetVisibleThisFrameMethod, pursuit);
        }

        internal static bool IsTargetVisible(VehiclePursuitBehaviourType pursuit)
        {
            return InvokeVisibilityProbe(VehicleIsTargetVisibleMethod, pursuit);
        }

        internal static void UpdateServerPursuitState(PlayerCrimeDataType crimeData)
        {
            if (!ShouldRunServerAuthority(crimeData))
            {
                return;
            }

            PlayerCrimeDataType.EPursuitLevel level = crimeData.CurrentPursuitLevel;
            if (level == PlayerCrimeDataType.EPursuitLevel.None)
            {
                return;
            }

            if (level != PlayerCrimeDataType.EPursuitLevel.Lethal && crimeData.TimeSinceSighted <= 1f)
            {
                if (level == PlayerCrimeDataType.EPursuitLevel.Arresting && crimeData.CurrentPursuitLevelDuration > 25f)
                {
                    crimeData.Escalate();
                    level = crimeData.CurrentPursuitLevel;
                }
                else if (level == PlayerCrimeDataType.EPursuitLevel.NonLethal && crimeData.CurrentPursuitLevelDuration > 120f)
                {
                    crimeData.Escalate();
                    level = crimeData.CurrentPursuitLevel;
                }
            }

            if (level != PlayerCrimeDataType.EPursuitLevel.None && crimeData.TimeSinceSighted > GetSearchTime(level) + 3f)
            {
                // Dedicated has no authoritative local-player audio path to preserve here.
                crimeData.SetPursuitLevel(PlayerCrimeDataType.EPursuitLevel.None);
            }
        }

        private static float GetSearchTime(PlayerCrimeDataType.EPursuitLevel level)
        {
            return level switch
            {
                PlayerCrimeDataType.EPursuitLevel.Investigating => 60f,
                PlayerCrimeDataType.EPursuitLevel.Arresting => 25f,
                PlayerCrimeDataType.EPursuitLevel.NonLethal => 30f,
                PlayerCrimeDataType.EPursuitLevel.Lethal => 40f,
                _ => 0f
            };
        }

        private static bool InvokeVisibilityProbe(MethodInfo method, object instance)
        {
            if (method == null || instance == null)
            {
                return false;
            }

            return method.Invoke(instance, null) is bool visible && visible;
        }
    }

    /// <summary>
    /// Re-latches foot pursuit visibility on dedicated servers when the target is still
    /// plainly visible but the vanilla vision-event flag has fallen out of sync.
    /// This prevents officers from dropping into search/wander behavior while the
    /// suspect is still in front of them.
    /// </summary>
    [HarmonyPatch(typeof(CombatBehaviourType), "CheckTargetVisibility")]
    internal static class CombatBehaviourCheckTargetVisibilityPatches
    {
        private static void Postfix(CombatBehaviourType __instance, ref bool ___visionEventReceived)
        {
            if (!DedicatedServerPatchCommon.IsDedicatedHeadlessServer() || !InstanceFinderType.IsServer)
            {
                return;
            }

            PursuitBehaviourType pursuit = __instance as PursuitBehaviourType;
            if (pursuit == null || !pursuit.Active || pursuit.IsTargetRecentlyVisible)
            {
                return;
            }

            PlayerType targetPlayer = pursuit.TargetPlayer;
            if (!DedicatedPolicePursuitAuthority.ShouldRunForPlayer(targetPlayer))
            {
                return;
            }

            if (!DedicatedPolicePursuitAuthority.IsTargetVisible(pursuit))
            {
                return;
            }

            ___visionEventReceived = true;
            DedicatedPolicePursuitAuthority.RefreshServerSight(targetPlayer);
        }
    }

    /// <summary>
    /// Suppresses loopback-client dispatch attempts inside the batchmode dedicated process.
    /// The server-side authority patch below performs the real pursuit progression.
    /// </summary>
    [HarmonyPatch(typeof(PoliceStationType), nameof(PoliceStationType.Dispatch))]
    internal static class PoliceStationDispatchPatches
    {
        private static bool Prefix()
        {
            if (!DedicatedServerPatchCommon.IsDedicatedHeadlessServer() || InstanceFinderType.IsServer)
            {
                return true;
            }

            return !Application.isBatchMode;
        }
    }

    /// <summary>
    /// Moves pursuit escalation and timeout maintenance onto the dedicated server for
    /// remote-client-owned players. Vanilla only advances these paths on Player.IsOwner.
    /// </summary>
    [HarmonyPatch(typeof(PlayerCrimeDataType), "Update")]
    internal static class PlayerCrimeDataUpdatePatches
    {
        private static void Postfix(PlayerCrimeDataType __instance)
        {
            DedicatedPolicePursuitAuthority.UpdateServerPursuitState(__instance);
        }
    }

    /// <summary>
    /// Ensures dedicated police behaviours release a player immediately once pursuit is
    /// cleared. Vanilla host play relies on owner-side combat updates to naturally unwind
    /// these targets, but dedicated authority patches can otherwise leave stale target
    /// bindings alive long enough to re-trigger false sight reacquisition.
    /// </summary>
    [HarmonyPatch(typeof(PlayerCrimeDataType), nameof(PlayerCrimeDataType.SetPursuitLevel))]
    internal static class PlayerCrimeDataSetPursuitLevelPatches
    {
        private static void Postfix(PlayerCrimeDataType __instance, PlayerCrimeDataType.EPursuitLevel level)
        {
            if (!DedicatedServerPatchCommon.IsDedicatedHeadlessServer() || !InstanceFinderType.IsServer || __instance?.Player == null)
            {
                return;
            }

            if (level != PlayerCrimeDataType.EPursuitLevel.None)
            {
                return;
            }

            DedicatedPolicePursuitAuthority.ClearPoliceTargeting(__instance.Player);
        }
    }

    /// <summary>
    /// Allows dedicated servers to complete arrests for remote-client-owned players.
    /// Vanilla only reports arrest progress from the owner side.
    /// </summary>
    [HarmonyPatch(typeof(PursuitBehaviourType), "UpdateArrest")]
    internal static class PursuitBehaviourUpdateArrestPatches
    {
        private static void Postfix(PursuitBehaviourType __instance, ref float ___timeWithinArrestRange)
        {
            if (!DedicatedServerPatchCommon.IsDedicatedHeadlessServer() || !InstanceFinderType.IsServer || __instance == null)
            {
                return;
            }

            PlayerType targetPlayer = __instance.TargetPlayer;
            if (targetPlayer == null
                || targetPlayer.IsOwner
                || targetPlayer.IsArrested
                || DedicatedServerPatchCommon.IsGhostOrLoopbackPlayer(targetPlayer)
                || targetPlayer.CrimeData == null
                || targetPlayer.CrimeData.CurrentPursuitLevel == PlayerCrimeDataType.EPursuitLevel.None)
            {
                return;
            }

            float progress = ___timeWithinArrestRange / 1.75f;
            if (progress > targetPlayer.CrimeData.CurrentArrestProgress)
            {
                targetPlayer.CrimeData.SetArrestProgress(progress);
            }
        }
    }

    /// <summary>
    /// Suppresses host-style target-seen ServerRpc dispatch inside the dedicated process.
    /// Server pursuit state is already updated locally and the regular destination refresh
    /// loop can repath without interrupting an in-flight vehicle navigation calculation.
    /// </summary>
    [HarmonyPatch(typeof(VehiclePursuitBehaviourType), "NotifyServerTargetSeen")]
    internal static class VehiclePursuitNotifyServerTargetSeenPatches
    {
        private static bool Prefix()
        {
            return !DedicatedServerPatchCommon.IsDedicatedHeadlessServer() || !InstanceFinderType.IsServer;
        }
    }

    /// <summary>
    /// Re-latches vehicle pursuit visibility on dedicated servers when the target is
    /// still visible but the vanilla target-seen callback did not refresh the chase
    /// state in time. This keeps UpdateDestination working against fresh target data.
    /// </summary>
    [HarmonyPatch(typeof(VehiclePursuitBehaviourType), "CheckTargetVisibility")]
    internal static class VehiclePursuitCheckTargetVisibilityPatches
    {
        private static void Postfix(VehiclePursuitBehaviourType __instance, ref bool ___visionEventReceived)
        {
            if (!DedicatedServerPatchCommon.IsDedicatedHeadlessServer() || !InstanceFinderType.IsServer || __instance == null)
            {
                return;
            }

            PlayerType targetPlayer = __instance.Target;
            if (!__instance.Active
                || __instance.IsTargetRecentlyVisible
                || !DedicatedPolicePursuitAuthority.ShouldRunForPlayer(targetPlayer))
            {
                return;
            }

            if (!DedicatedPolicePursuitAuthority.IsTargetVisible(__instance))
            {
                return;
            }

            ___visionEventReceived = true;
            DedicatedPolicePursuitAuthority.RefreshServerSight(targetPlayer);
        }
    }

    /// <summary>
    /// Promotes dedicated vehicle pursuits on the server when officers regain sight of a
    /// remote-client-owned suspect. Vanilla keys this transition off Target.IsOwner.
    /// </summary>
    [HarmonyPatch(typeof(VehiclePursuitBehaviourType), "ProcessVisionEvent")]
    internal static class VehiclePursuitProcessVisionEventPatches
    {
        private static void Postfix(VehiclePursuitBehaviourType __instance, VisionEventReceiptType visionEventReceipt)
        {
            if (!DedicatedServerPatchCommon.IsDedicatedHeadlessServer() || !InstanceFinderType.IsServer || __instance?.Target == null)
            {
                return;
            }

            if (__instance.Target.IsOwner
                || DedicatedServerPatchCommon.IsGhostOrLoopbackPlayer(__instance.Target)
                || !__instance.Active
                || visionEventReceipt == null
                || visionEventReceipt.Target != __instance.Target.NetworkObject
                || __instance.Target.CrimeData.CurrentPursuitLevel != PlayerCrimeDataType.EPursuitLevel.Investigating)
            {
                return;
            }

            __instance.Target.CrimeData.Escalate();
        }
    }

    /// <summary>
    /// Prevents dedicated vehicle pursuits from handing an already-invalid target back into
    /// foot pursuit when the chase is being torn down after arrest or release.
    /// Without this guard, officers can repeatedly re-arm pursuit reacquisition against a
    /// cleared target and spam local notice popups while never committing to a real chase.
    /// </summary>
    [HarmonyPatch(typeof(VehiclePursuitBehaviourType), "Deactivate")]
    internal static class VehiclePursuitDeactivatePatches
    {
        private static void Postfix(VehiclePursuitBehaviourType __instance)
        {
            if (!DedicatedServerPatchCommon.IsDedicatedHeadlessServer() || !InstanceFinderType.IsServer || __instance == null)
            {
                return;
            }

            PlayerType targetPlayer = __instance.Target;
            if (!DedicatedPolicePursuitAuthority.IsInvalidPursuitTarget(targetPlayer))
            {
                return;
            }

            PoliceOfficerType officer = __instance.Npc as PoliceOfficerType;
            PursuitBehaviourType pursuitBehaviour = officer?.PursuitBehaviour;
            if (pursuitBehaviour == null || pursuitBehaviour.TargetPlayer != targetPlayer || !pursuitBehaviour.Enabled)
            {
                return;
            }

            pursuitBehaviour.Disable_Networked(null);
        }
    }

    /// <summary>
    /// Tones down dedicated police vehicle pursuit overrides that are hard-coded for host
    /// play and otherwise produce excessive speed spikes on overloaded dedicated servers.
    /// </summary>
    [HarmonyPatch(typeof(VehiclePursuitBehaviourType), "SetAggressiveDriving")]
    internal static class VehiclePursuitAggressiveDrivingPatches
    {
        private static void Postfix(VehiclePursuitBehaviourType __instance, bool aggressive)
        {
            if (!DedicatedServerPatchCommon.IsDedicatedHeadlessServer() || !aggressive || __instance?.vehicle?.Agent?.Flags == null)
            {
                return;
            }

            ServerPressureLevel pressureLevel = ServerAdaptivePerformanceTuning.GetSnapshot().PressureLevel;
            __instance.vehicle.Agent.Flags.OverrideSpeed = true;
            __instance.vehicle.Agent.Flags.OverriddenSpeed = pressureLevel switch
            {
                ServerPressureLevel.High => 22f,
                ServerPressureLevel.Medium => 26f,
                _ => 30f
            };

            __instance.vehicle.Agent.Flags.OverriddenReverseSpeed = pressureLevel switch
            {
                ServerPressureLevel.High => 3f,
                ServerPressureLevel.Medium => 4f,
                _ => 5f
            };

            // Keep pursuit responsive, but avoid the host-tuned off-road/no-brake profile
            // that looks erratic on headless dedicated simulation.
            __instance.vehicle.Agent.Flags.AutoBrakeAtDestination = true;
            __instance.vehicle.Agent.Flags.UseRoads = true;
            __instance.vehicle.Agent.Flags.ObstacleMode = DriveFlagsType.EObstacleMode.Default;
        }
    }
}
