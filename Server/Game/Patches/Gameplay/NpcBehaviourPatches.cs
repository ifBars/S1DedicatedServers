using System;
using HarmonyLib;
using DedicatedServerMod.Utils;
using UnityEngine;
#if IL2CPP
using BehaviourType = Il2CppScheduleOne.NPCs.Behaviour.Behaviour;
using BehaviourListType = Il2CppSystem.Collections.Generic.List<Il2CppScheduleOne.NPCs.Behaviour.Behaviour>;
using NpcBehaviourType = Il2CppScheduleOne.NPCs.Behaviour.NPCBehaviour;
using PursuitBehaviourType = Il2CppScheduleOne.NPCs.Behaviour.PursuitBehaviour;
using VehiclePursuitBehaviourType = Il2CppScheduleOne.NPCs.Behaviour.VehiclePursuitBehaviour;
#else
using BehaviourType = ScheduleOne.NPCs.Behaviour.Behaviour;
using BehaviourListType = System.Collections.Generic.List<ScheduleOne.NPCs.Behaviour.Behaviour>;
using NpcBehaviourType = ScheduleOne.NPCs.Behaviour.NPCBehaviour;
using PursuitBehaviourType = ScheduleOne.NPCs.Behaviour.PursuitBehaviour;
using VehiclePursuitBehaviourType = ScheduleOne.NPCs.Behaviour.VehiclePursuitBehaviour;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Gameplay
{
    /// <summary>
    /// Removes per-frame LINQ allocation and reflection overhead from NPC behavior selection on dedicated headless servers.
    /// Uses the Krafs-publicized <c>enabledBehaviours</c> field directly so the hot path stays out of <see cref="Utils.SafeReflection"/>.
    /// Also throttles repeated stale police-behaviour exceptions behind one shared cooldown so disconnect cleanup can recover
    /// invalid pursuit targets without sweeping every NPC instance or flooding logs every frame.
    /// </summary>
    [HarmonyPatch(typeof(NpcBehaviourType), "Update")]
    internal static class NpcBehaviourUpdatePatches
    {
        private const float STALE_BEHAVIOUR_CLEANUP_COOLDOWN_SECONDS = 3f;

        private static float _lastStaleBehaviourCleanupTime = -STALE_BEHAVIOUR_CLEANUP_COOLDOWN_SECONDS;
        private static int _suppressedStaleBehaviourExceptions;

        private static bool Prefix(NpcBehaviourType __instance)
        {
            if (__instance == null || __instance.DEBUG_MODE)
            {
                return true;
            }

            if (__instance.IsServerInitialized)
            {
                BehaviourListType enabledBehaviours = __instance.enabledBehaviours;
                BehaviourType enabledBehaviour = enabledBehaviours != null && enabledBehaviours.Count > 0
                    ? enabledBehaviours[0]
                    : null;
                if (enabledBehaviour != __instance.activeBehaviour)
                {
                    if (__instance.activeBehaviour != null)
                    {
                        __instance.activeBehaviour.Pause_Server();
                    }

                    if (enabledBehaviour != null)
                    {
                        if (enabledBehaviour.Started)
                        {
                            enabledBehaviour.Resume_Server();
                        }
                        else
                        {
                            enabledBehaviour.Activate_Server(null);
                        }
                    }
                }
            }

            if (__instance.activeBehaviour != null && __instance.activeBehaviour.Active)
            {
                try
                {
                    __instance.activeBehaviour.BehaviourUpdate();
                }
                catch (Exception ex)
                {
                    if (!ShouldRecoverFromStalePoliceTarget(__instance.activeBehaviour))
                    {
                        throw;
                    }

                    _suppressedStaleBehaviourExceptions++;
                    float now = Time.realtimeSinceStartup;
                    if (now - _lastStaleBehaviourCleanupTime < STALE_BEHAVIOUR_CLEANUP_COOLDOWN_SECONDS)
                    {
                        return false;
                    }

                    _lastStaleBehaviourCleanupTime = now;
                    int suppressedCount = _suppressedStaleBehaviourExceptions;
                    _suppressedStaleBehaviourExceptions = 0;

                    DedicatedPolicePursuitAuthority.ClearInvalidPoliceTargets();
                    DebugLog.Warning($"Suppressed {suppressedCount} stale NPC behaviour update exception(s) after dedicated target cleanup: {ex.Message}");
                }
            }

            return false;
        }

        private static bool ShouldRecoverFromStalePoliceTarget(BehaviourType behaviour)
        {
            if (behaviour == null)
            {
                return false;
            }

            if (behaviour is PursuitBehaviourType || behaviour is VehiclePursuitBehaviourType)
            {
                return true;
            }

            string typeName = behaviour.GetType()?.FullName ?? string.Empty;
            return typeName.IndexOf("BodySearchBehaviour", StringComparison.Ordinal) >= 0;
        }
    }
}
