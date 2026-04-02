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
    /// Removes per-frame LINQ allocation and lookup overhead from NPC behavior selection on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(NpcBehaviourType), "Update")]
    internal static class NpcBehaviourUpdatePatches
    {
        private static bool Prefix(NpcBehaviourType __instance, BehaviourListType ___enabledBehaviours)
        {
            if (__instance == null || __instance.DEBUG_MODE)
            {
                return true;
            }

            if (__instance.IsServerInitialized)
            {
                BehaviourType enabledBehaviour = ___enabledBehaviours != null && ___enabledBehaviours.Count > 0
                    ? ___enabledBehaviours[0]
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
