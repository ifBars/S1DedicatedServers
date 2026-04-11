#if IL2CPP
using Il2CppFishNet;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.UI.Phone.Map;
#else
using FishNet;
using ScheduleOne.DevUtilities;
using ScheduleOne.Map;
using ScheduleOne.UI.Phone.Map;
#endif
using MelonLoader;
#if IL2CPP
using Il2CppScheduleOne.PlayerScripts;
#else
using ScheduleOne.PlayerScripts;
#endif
using System;
using System.Collections;
using DedicatedServerMod.Client.Managers;
using DedicatedServerMod.Utils;
using UnityEngine;

namespace DedicatedServerMod.Client.Patchers
{
    /// <summary>
    /// Handles hiding server loopback players on dedicated server clients.
    /// Uses centralized ghost host detection.
    /// </summary>
    internal class ClientLoopbackHandler
    {
        private bool eventHooksSetup = false;

        internal void Initialize()
        {
            try
            {
                SetupPlayerSpawnHooks();
                DebugLog.StartupDebug("ClientLoopbackHandler initialized");
            }
            catch (Exception ex)
            {
                DebugLog.Error("Failed to initialize ClientLoopbackHandler", ex);
            }
        }

        private void SetupPlayerSpawnHooks()
        {
            if (eventHooksSetup) return;

            try
            {
#if IL2CPP
                Player.onPlayerSpawned -= new Action<Player>(OnPlayerSpawned_CheckLoopback);
                Player.onPlayerSpawned += new Action<Player>(OnPlayerSpawned_CheckLoopback);
#else
                Player.onPlayerSpawned = (Action<Player>)Delegate.Remove(
                    Player.onPlayerSpawned, new Action<Player>(OnPlayerSpawned_CheckLoopback));
                Player.onPlayerSpawned = (Action<Player>)Delegate.Combine(
                    Player.onPlayerSpawned, new Action<Player>(OnPlayerSpawned_CheckLoopback));
#endif

                eventHooksSetup = true;
            }
            catch (Exception ex)
            {
                DebugLog.Error("Error setting up player spawn hooks", ex);
            }
        }

        private void OnPlayerSpawned_CheckLoopback(Player player)
        {
            if (player == null || InstanceFinder.IsServer)
                return;

            try
            {
                if (player.IsGhostHost())
                {
                    HideLoopbackPlayer(player);
                }
                else
                {
                    MelonCoroutines.Start(DelayedLoopbackCheck(player));
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error("Error in player spawn loopback check", ex);
            }
        }

        private void HideLoopbackPlayer(Player player)
        {
            try
            {
                HideLoopbackPresentation(player);
            }
            catch (Exception ex)
            {
                DebugLog.Error("Error hiding loopback player", ex);
            }
        }

        private IEnumerator DelayedLoopbackCheck(Player player)
        {
            yield return new WaitForSeconds(0.5f);

            if (player == null || player.gameObject == null)
                yield break;

            try
            {
                if (player.IsGhostHost())
                    HideLoopbackPlayer(player);
            }
            catch (Exception ex)
            {
                DebugLog.Error("Error in delayed loopback check", ex);
            }
        }

        internal void ShowLoopbackPlayer(Player player)
        {
            if (player == null) return;

            try
            {
                player.SetVisibleToLocalPlayer(true);
                if (player.Avatar != null)
                    player.Avatar.SetVisible(true);
                DebugLog.Debug($"Loopback player made visible: {player.PlayerName ?? "Unknown"}");
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error showing loopback player", ex);
            }
        }

        internal static void HideLoopbackPresentation(Player player)
        {
            if (player == null)
                return;

            DebugLog.Debug($"Hiding ghost host player presentation: {player.PlayerName ?? "Unknown"}");

            player.SetVisibleToLocalPlayer(false);

            if (player.Avatar != null)
                player.Avatar.SetVisible(false);

            HideLoopbackMapMarker(player);
        }

        internal static bool TryGetGhostHostOwner(POI poi, out Player player)
        {
            player = null;

            if (poi == null)
                return false;

            player = UnityComponentAccess.GetComponentInParent<Player>(poi, includeInactive: true);
            if (player != null && player.IsGhostHost())
                return true;

            foreach (var candidate in Player.PlayerList)
            {
                if (candidate?.PoI == poi && candidate.IsGhostHost())
                {
                    player = candidate;
                    return true;
                }
            }

            foreach (var candidate in UnityComponentAccess.FindObjectsOfType<Player>(includeInactive: true))
            {
                if (candidate?.PoI == poi && candidate.IsGhostHost())
                {
                    player = candidate;
                    return true;
                }
            }

            player = null;
            return false;
        }

        internal static int GetVisiblePlayerCount()
        {
            int count = 0;

            foreach (var player in Player.PlayerList)
            {
                if (player == null || player.IsGhostHost())
                    continue;

                count++;
            }

            return count;
        }

        private static void HideLoopbackMapMarker(Player player)
        {
            var poi = player.PoI;
            if (poi == null || poi.UI == null)
                return;

            try
            {
                if (PlayerSingleton<MapApp>.Instance != null)
                    PlayerSingleton<MapApp>.Instance.TeardownMapItem(poi.UI.gameObject);

                UnityEngine.Object.Destroy(poi.UI.gameObject);
            }
            catch (Exception ex)
            {
                DebugLog.Error("Error hiding loopback map marker", ex);
            }
        }

        internal string GetLoopbackStatus()
        {
            try
            {
                var status = "=== Loopback Handler Status ===\n";
                status += $"Event Hooks Setup: {eventHooksSetup}\n";
                status += $"Is Client: {InstanceFinder.IsClient}\n";
                status += $"Tugboat Mode: {ClientConnectionManager.IsTugboatMode}\n";

                int visibleCount = GetVisiblePlayerCount();
                int hiddenCount = Player.PlayerList.Count - visibleCount;

                status += $"Real Players: {visibleCount}\n";
                status += $"Ghost Host Players: {hiddenCount}\n";
                return status;
            }
            catch (Exception ex)
            {
                return $"Error getting loopback status: {ex.Message}";
            }
        }

        internal void ForceCheckAllPlayers()
        {
            try
            {
                foreach (var player in Player.PlayerList)
                {
                    if (player != null && !InstanceFinder.IsServer)
                        OnPlayerSpawned_CheckLoopback(player);
                }
                DebugLog.Debug($"Force check completed for {Player.PlayerList.Count} players");
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error in force check: {ex}");
            }
        }

        internal void Cleanup()
        {
            if (!eventHooksSetup) return;

            try
            {
#if IL2CPP
                Player.onPlayerSpawned -= new Action<Player>(OnPlayerSpawned_CheckLoopback);
#else
                Player.onPlayerSpawned = (Action<Player>)Delegate.Remove(
                    Player.onPlayerSpawned, new Action<Player>(OnPlayerSpawned_CheckLoopback));
#endif
                eventHooksSetup = false;
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error during loopback handler cleanup", ex);
            }
        }
    }
}
