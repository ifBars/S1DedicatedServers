using FishNet;
using MelonLoader;
using ScheduleOne.PlayerScripts;
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
    public class ClientLoopbackHandler
    {
        private readonly MelonLogger.Instance logger;
        private bool eventHooksSetup = false;

        public ClientLoopbackHandler(MelonLogger.Instance logger)
        {
            this.logger = logger;
        }

        public void Initialize()
        {
            try
            {
                logger.Msg("Initializing ClientLoopbackHandler");
                SetupPlayerSpawnHooks();
                logger.Msg("ClientLoopbackHandler initialized");
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to initialize ClientLoopbackHandler: {ex}");
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
                logger.Msg("Client-side loopback hiding hooks active");
            }
            catch (Exception ex)
            {
                logger.Error($"Error setting up player spawn hooks: {ex}");
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
                logger.Error($"Error in player spawn loopback check: {ex}");
            }
        }

        private void HideLoopbackPlayer(Player player)
        {
            try
            {
                logger.Msg($"Hiding ghost host player: {player.PlayerName ?? "Unknown"}");

                player.SetVisibleToLocalPlayer(false);

                if (player.Avatar != null)
                {
                    player.Avatar.SetVisible(false);
                    logger.Msg("Hidden loopback player avatar");
                }

                logger.Msg("Server loopback player hidden successfully");
            }
            catch (Exception ex)
            {
                logger.Error($"Error hiding loopback player: {ex}");
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
                {
                    logger.Msg("Delayed check: identified ghost host player");
                    HideLoopbackPlayer(player);
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error in delayed loopback check: {ex}");
            }
        }

        public void ShowLoopbackPlayer(Player player)
        {
            if (player == null) return;

            try
            {
                player.SetVisibleToLocalPlayer(true);
                if (player.Avatar != null)
                    player.Avatar.SetVisible(true);
                logger.Msg($"Loopback player made visible: {player.PlayerName ?? "Unknown"}");
            }
            catch (Exception ex)
            {
                logger.Error($"Error showing loopback player: {ex}");
            }
        }

        public string GetLoopbackStatus()
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

        public void ForceCheckAllPlayers()
        {
            try
            {
                foreach (var player in Player.PlayerList)
                {
                    if (player != null && !InstanceFinder.IsServer)
                        OnPlayerSpawned_CheckLoopback(player);
                }
                logger.Msg($"Force check completed for {Player.PlayerList.Count} players");
            }
            catch (Exception ex)
            {
                logger.Error($"Error in force check: {ex}");
            }
        }

        public void Cleanup()
        {
            if (!eventHooksSetup) return;

            try
            {
                Player.onPlayerSpawned = (Action<Player>)Delegate.Remove(
                    Player.onPlayerSpawned, new Action<Player>(OnPlayerSpawned_CheckLoopback));
                eventHooksSetup = false;
                logger.Msg("Loopback handler event hooks removed");
            }
            catch (Exception ex)
            {
                logger.Error($"Error during loopback handler cleanup: {ex}");
            }
        }
    }
}
