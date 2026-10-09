using DedicatedServerMod.Server.Game.Patches.Common;
using HarmonyLib;
#if IL2CPP
using NpcActionsType = Il2CppScheduleOne.NPCs.Actions.NPCActions;
#else
using NpcActionsType = ScheduleOne.NPCs.Actions.NPCActions;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Weather
{
    /// <summary>
    /// Defers weather actions for pooled special customers until their NPC data is assigned.
    /// </summary>
    [HarmonyPatch(typeof(NpcActionsType), "UpdateUmbrellaUse")]
    internal static class NpcWeatherPatches
    {
        private static bool Prefix(NpcActionsType __instance)
        {
            // Prewarmed NPCs subscribe to minute ticks before InitializeNPC. Their movement
            // speed and weather preferences are unavailable until the pool assigns NPCData.
            return !DedicatedServerPatchCommon.IsDedicatedHeadlessServer()
                || (__instance.npc != null && __instance.npc.HasNPCData);
        }
    }
}
