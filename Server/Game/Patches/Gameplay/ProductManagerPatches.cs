using HarmonyLib;
#if IL2CPP
using Il2CppFishNet;
using Il2CppScheduleOne.DevUtilities;
using ProductManagerType = Il2CppScheduleOne.Product.ProductManager;
using VariableDatabaseType = Il2CppScheduleOne.Variables.VariableDatabase;
#else
using FishNet;
using ScheduleOne.DevUtilities;
using ProductManagerType = ScheduleOne.Product.ProductManager;
using VariableDatabaseType = ScheduleOne.Variables.VariableDatabase;
#endif
using UnityEngine;

namespace DedicatedServerMod.Server.Game.Patches.Gameplay
{
    /// <summary>
    /// Prevents headless dedicated servers from polling local-player inventory discovery variables
    /// before those variables exist. The vanilla path uses these values to unlock the second unique
    /// product tutorial hint, but dedicated servers do not drive the local inventory UI path that
    /// creates the inventory-backed variables, which causes repeated missing-variable log spam.
    /// </summary>
    [HarmonyPatch(typeof(ProductManagerType), "OnMinPass")]
    internal static class ProductManagerOnMinPassPatches
    {
        private static bool Prefix()
        {
            if (!InstanceFinder.IsServer || !Application.isBatchMode || !NetworkSingleton<VariableDatabaseType>.InstanceExists)
            {
                return true;
            }

            VariableDatabaseType variableDatabase = NetworkSingleton<VariableDatabaseType>.Instance;
            if (variableDatabase == null)
            {
                return true;
            }

            return variableDatabase.VariableDict.ContainsKey("inventory_ogkush")
                && variableDatabase.VariableDict.ContainsKey("inventory_weed_count");
        }
    }
}
