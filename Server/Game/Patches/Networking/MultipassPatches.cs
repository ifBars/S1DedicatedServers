using System.Reflection;
using DedicatedServerMod.Server.Game.Patches.Common;
using HarmonyLib;
#if IL2CPP
using Il2CppFishNet.Transporting;
using Il2CppFishNet.Transporting.Multipass;
using Il2CppFishNet.Transporting.Tugboat;
#else
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Networking
{
    [HarmonyPatch(typeof(Multipass), "Initialize")]
    internal static class MultipassPatches
    {
        private static void Prefix(Multipass __instance)
        {
            try
            {
                var tugboat = __instance.gameObject.GetComponent<Tugboat>();
                if (tugboat == null)
                {
                    tugboat = __instance.gameObject.AddComponent<Tugboat>();
                    DedicatedServerPatchCommon.Logger.Msg("Added Tugboat component to Multipass for server");

                    var transportsField = typeof(Multipass).GetField("_transports", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (transportsField?.GetValue(__instance) is List<Transport> transports && !transports.Contains(tugboat))
                    {
                        transports.Add(tugboat);
                    }
                }
            }
            catch (Exception ex)
            {
                DedicatedServerPatchCommon.Logger.Error($"Error in Multipass.Initialize patch: {ex}");
            }
        }
    }
}
