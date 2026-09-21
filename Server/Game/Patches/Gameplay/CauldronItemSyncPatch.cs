#if SERVER
using HarmonyLib;
#if IL2CPP
using Il2CppFishNet.Connection;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.ObjectScripts;
#else
using FishNet.Connection;
using ScheduleOne.ItemFramework;
using ScheduleOne.ObjectScripts;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Gameplay
{
    /// <summary>
    /// Sends the cauldron's authoritative item slots to joining and reconnecting clients.
    /// </summary>
    /// <remarks>
    /// The native spawn callback sends cooking and configuration state but omits the item-slot
    /// snapshot used by other stations. Reuse the native sender to preserve product-data ordering,
    /// slot locks, and filters. Root-cause research and original fix by Krazyfoxx (mattisok3):
    /// https://github.com/ifBars/S1DedicatedServers/issues/58#issuecomment-5747024861.
    /// </remarks>
    [HarmonyPatch(typeof(Cauldron), nameof(Cauldron.OnSpawnServer))]
    internal static class CauldronItemSyncPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Cauldron __instance, NetworkConnection connection)
        {
            if (connection == null || connection.IsHost)
            {
                return;
            }

#if IL2CPP
            __instance.Cast<IItemSlotOwner>().SendItemSlotDataToClient(connection);
#else
            ((IItemSlotOwner)__instance).SendItemSlotDataToClient(connection);
#endif
        }
    }
}
#endif
