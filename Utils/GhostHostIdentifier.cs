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
    /// Centralized identification of the ghost loopback host player on dedicated servers.
    /// All systems that need to detect or filter the ghost host should use this class
    /// instead of duplicating detection logic.
    /// </summary>
    public static class GhostHostIdentifier
    {
        /// <summary>
        /// Determines if a player is the ghost loopback host on a dedicated server.
        /// Uses two detection methods: game object name and network ownership characteristics.
        /// </summary>
        public static bool IsGhostHost(Player player)
        {
            if (player == null)
                return false;

            try
            {
                if (player.gameObject != null && player.gameObject.name == Constants.GhostHostObjectName)
                    return true;

                // Dedicated server runs a local loopback client in batch mode.
                // That local client player should always be treated as the ghost host.
                if (Application.isBatchMode && FishNet.InstanceFinder.IsServer)
                {
                    if (player.Owner != null && player.Owner.IsLocalClient)
                        return true;
                }

                var networkObject = player.GetComponent<NetworkObject>();
                if (networkObject?.Owner != null)
                {
                    if (Application.isBatchMode && FishNet.InstanceFinder.IsServer && networkObject.Owner.IsLocalClient)
                        return true;

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
