#if IL2CPP
using Il2CppFishNet.Object;
using Il2CppScheduleOne.PlayerScripts;
#else
using FishNet.Object;
using ScheduleOne.PlayerScripts;
#endif
using System;
using UnityEngine;

namespace DedicatedServerMod.Utils
{
    /// <summary>
    /// Dedicated-server-specific helpers for identifying the synthetic loopback host player.
    /// </summary>
    internal static class PlayerGhostHostExtensions
    {
        /// <summary>
        /// Determines if a player is the ghost loopback host on a dedicated server.
        /// Uses game object naming and network ownership characteristics.
        /// </summary>
        internal static bool IsGhostHost(this Player player)
        {
            if (player == null)
            {
                return false;
            }

            try
            {
                if (player.gameObject != null && player.gameObject.name == Constants.GhostHostObjectName)
                {
                    return true;
                }

                // Dedicated server runs a local loopback client in batch mode.
                // That local client player should always be treated as the ghost host.
                if (Application.isBatchMode && FishNet.InstanceFinder.IsServer)
                {
                    if (player.Owner != null && player.Owner.IsLocalClient)
                    {
                        return true;
                    }
                }

                var networkObject = player.GetComponent<NetworkObject>();
                if (networkObject?.Owner != null)
                {
                    if (Application.isBatchMode && FishNet.InstanceFinder.IsServer && networkObject.Owner.IsLocalClient)
                    {
                        return true;
                    }

                    return networkObject.Owner.ClientId == 0 && !networkObject.IsOwner;
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }
    }
}
