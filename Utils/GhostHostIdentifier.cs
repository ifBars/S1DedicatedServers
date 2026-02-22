using FishNet.Object;
using ScheduleOne.PlayerScripts;
using System;

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
                if (player.gameObject.name == Constants.GhostHostObjectName)
                    return true;

                var networkObject = player.GetComponent<NetworkObject>();
                if (networkObject?.Owner != null)
                    return networkObject.Owner.ClientId == 0 && !networkObject.IsOwner;
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }
    }
}
