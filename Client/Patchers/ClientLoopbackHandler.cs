#if IL2CPP
using Il2CppFishNet;
#else
using FishNet;
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
    /// Uses <see cref="GhostHostIdentifier"/> for centralized detection.
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
                Player.onPlayerSpawned = (Action<Player>)Delegate.Remove(
                    Player.onPlayerSpawned, new Action<Player>(OnPlayerSpawned_CheckLoopback));
                Player.onPlayerSpawned = (Action<Player>)Delegate.Combine(
                    Player.onPlayerSpawned, new Action<Player>(OnPlayerSpawned_CheckLoopback));

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
                if (GhostHostIdentifier.IsGhostHost(player))
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
                DebugLog.Debug($"Hiding ghost host player: {player.PlayerName ?? "Unknown"}");

                player.SetVisibleToLocalPlayer(false);

                if (player.Avatar != null)
                    player.Avatar.SetVisible(false);
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
                if (GhostHostIdentifier.IsGhostHost(player))
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

        internal string GetLoopbackStatus()
        {
            try
            {
                var status = "=== Loopback Handler Status ===\n";
                status += $"Event Hooks Setup: {eventHooksSetup}\n";
                status += $"Is Client: {InstanceFinder.IsClient}\n";
                status += $"Tugboat Mode: {ClientConnectionManager.IsTugboatMode}\n";

                int visibleCount = 0;
                int hiddenCount = 0;
                foreach (var player in Player.PlayerList)
                {
                    if (GhostHostIdentifier.IsGhostHost(player))
                        hiddenCount++;
                    else
                        visibleCount++;
                }

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
                Player.onPlayerSpawned = (Action<Player>)Delegate.Remove(
                    Player.onPlayerSpawned, new Action<Player>(OnPlayerSpawned_CheckLoopback));
                eventHooksSetup = false;
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error during loopback handler cleanup", ex);
            }
        }
    }
}
