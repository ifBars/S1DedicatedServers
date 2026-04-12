using HarmonyLib;
#if IL2CPP
using BehaviourType = Il2CppScheduleOne.NPCs.Behaviour.Behaviour;
using BehaviourListType = Il2CppSystem.Collections.Generic.List<Il2CppScheduleOne.NPCs.Behaviour.Behaviour>;
using NpcBehaviourType = Il2CppScheduleOne.NPCs.Behaviour.NPCBehaviour;
#else
using BehaviourType = ScheduleOne.NPCs.Behaviour.Behaviour;
using BehaviourListType = System.Collections.Generic.List<ScheduleOne.NPCs.Behaviour.Behaviour>;
using NpcBehaviourType = ScheduleOne.NPCs.Behaviour.NPCBehaviour;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Gameplay
{
    /// <summary>
    /// Removes per-frame LINQ allocation and reflection overhead from NPC behavior selection on dedicated headless servers.
    /// Uses the Krafs-publicized <c>enabledBehaviours</c> field directly so the hot path stays out of <see cref="Utils.SafeReflection"/>.
    /// </summary>
    [HarmonyPatch(typeof(NpcBehaviourType), "Update")]
    internal static class NpcBehaviourUpdatePatches
    {
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
                __instance.activeBehaviour.BehaviourUpdate();
            }

            return false;
        }
    }
}
