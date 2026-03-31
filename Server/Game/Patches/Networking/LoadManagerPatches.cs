using System.Reflection;
using DedicatedServerMod.Server.Game.Patches.Common;
using DedicatedServerMod.Shared.Configuration;
using DedicatedServerMod.Shared.Networking;
using DedicatedServerMod.Utils;
using HarmonyLib;
#if IL2CPP
using Il2CppFishNet;
using Il2CppFishNet.Transporting;
using Il2CppFishNet.Transporting.Multipass;
using Il2CppFishNet.Transporting.Tugboat;
using Il2CppScheduleOne.Persistence;
#else
using FishNet;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using ScheduleOne.Persistence;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Networking
{
    [HarmonyPatch(typeof(LoadManager), "StartGame")]
    internal static class LoadManagerPatches
    {
        private static void Prefix(LoadManager __instance)
        {
            try
            {
                var networkManager = InstanceFinder.NetworkManager;
                Transport transport = networkManager?.TransportManager?.Transport;
                if (!MultipassTransportResolver.TryResolve(transport, out var multipass))
                {
                    return;
                }

                var tugboat = multipass.gameObject.GetComponent<Tugboat>();
                if (tugboat == null)
                {
                    tugboat = multipass.gameObject.AddComponent<Tugboat>();

                    var clientField = typeof(Multipass).GetField("_clientTransport", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (clientField != null)
                    {
                        clientField.SetValue(multipass, tugboat);
                    }
                }

                tugboat.SetPort((ushort)ServerConfig.Instance.ServerPort);
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"LoadManager.StartGame transport prep failed: {ex.Message}");
            }
        }
    }
}
