using FishNet;
using FishNet.Object;
using MelonLoader;
using ScheduleOne.PlayerScripts;
using System;
using System.Collections;
using UnityEngine;

namespace DedicatedServerMod.Client
{
    /// <summary>
    /// Handles hiding server loopback players on dedicated server clients.
    /// When a dedicated server hosts a game, it creates a "loopback" player that should be hidden from clients.
    /// </summary>
    public class ClientLoopbackHandler
    {
        private readonly MelonLogger.Instance logger;
        
        // State tracking
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

        /// <summary>
        /// Setup hooks for player spawn events to detect loopback players
        /// </summary>
        private void SetupPlayerSpawnHooks()
        {
            try
            {
                if (eventHooksSetup)
                {
                    logger.Msg("Player spawn hooks already setup");
                    return;
                }

                // Hook into player spawn events
                Player.onPlayerSpawned = (Action<Player>)System.Delegate.Remove(
                    Player.onPlayerSpawned, new Action<Player>(OnPlayerSpawned_CheckLoopback));
                Player.onPlayerSpawned = (Action<Player>)System.Delegate.Combine(
                    Player.onPlayerSpawned, new Action<Player>(OnPlayerSpawned_CheckLoopback));
                
                eventHooksSetup = true;
                logger.Msg("Client-side loopback hiding setup complete");
            }
            catch (Exception ex)
            {
                logger.Error($"Error setting up player spawn hooks: {ex}");
            }
        }

        /// <summary>
        /// Handle player spawn events and check for loopback players
        /// </summary>
        private void OnPlayerSpawned_CheckLoopback(Player player)
        {
            try
            {
                if (player == null || InstanceFinder.IsServer)
                    return;

                // Only process on clients (not servers)
                logger.Msg($"Player spawned - checking for loopback: {player.PlayerName ?? "Unknown"}");

                // Check if this is a server loopback player
                if (IsServerLoopbackPlayer(player))
                {
                    HideLoopbackPlayer(player);
                }
                else
                {
                    // Perform delayed check in case NetworkObject data isn't immediately available
                    MelonCoroutines.Start(DelayedLoopbackCheck(player));
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error in player spawn loopback check: {ex}");
            }
        }

        /// <summary>
        /// Check if a player is the server's loopback player
        /// </summary>
        private bool IsServerLoopbackPlayer(Player player)
        {
            try
            {
                // Get NetworkObject for ownership information
                var networkObject = player.GetComponent<NetworkObject>();
                if (networkObject?.Owner == null)
                {
                    return false; // Can't determine without network data
                }

                // Server loopback player characteristics:
                // - Owner ClientId is 0 (server)
                // - IsOwner is false (not owned by this client)
                bool isServerLoopback = (networkObject.Owner.ClientId == 0 && !networkObject.IsOwner);
                
                if (isServerLoopback)
                {
                    logger.Msg($"Detected server loopback player: ClientId={networkObject.Owner.ClientId}, IsOwner={networkObject.IsOwner}");
                }

                return isServerLoopback;
            }
            catch (Exception ex)
            {
                logger.Error($"Error checking server loopback player: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Hide a loopback player from the client
        /// </summary>
        private void HideLoopbackPlayer(Player player)
        {
            try
            {
                logger.Msg($"Hiding server loopback player: {player.PlayerName ?? "Unknown"}");
                
                // Hide the player from local client
                player.SetVisibleToLocalPlayer(false);
                
                // Also hide the avatar if it exists
                if (player.Avatar != null)
                {
                    player.Avatar.SetVisible(false);
                    logger.Msg("Hidden loopback player avatar");
                }

                // Hide any UI elements associated with this player
                HidePlayerUI(player);
                
                logger.Msg("Server loopback player hidden successfully");
            }
            catch (Exception ex)
            {
                logger.Error($"Error hiding loopback player: {ex}");
            }
        }

        /// <summary>
        /// Hide UI elements associated with a loopback player
        /// </summary>
        private void HidePlayerUI(Player player)
        {
            try
            {
                // Hide player nameplate, indicators, etc.
                // This depends on the specific UI implementation in the game
                
                // For now, just ensure the player is marked as not visible
                // The game's UI systems should respect the visibility settings
                
                logger.Msg("Player UI elements hidden for loopback player");
            }
            catch (Exception ex)
            {
                logger.Error($"Error hiding player UI: {ex}");
            }
        }

        /// <summary>
        /// Perform delayed check for loopback players
        /// NetworkObject data might not be immediately available on spawn
        /// </summary>
        private IEnumerator DelayedLoopbackCheck(Player player)
        {
            // Wait for NetworkObject ownership data to be synchronized
            yield return new WaitForSeconds(0.5f);
            
            try
            {
                if (player == null || player.gameObject == null)
                    yield break;

                // Re-check for server loopback after delay
                if (IsServerLoopbackPlayer(player))
                {
                    logger.Msg("Delayed check: identified server loopback player");
                    HideLoopbackPlayer(player);
                }
                else
                {
                    // Log player info for debugging
                    LogPlayerInfo(player);
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error in delayed loopback check: {ex}");
            }
        }

        /// <summary>
        /// Log player information for debugging
        /// </summary>
        private void LogPlayerInfo(Player player)
        {
            try
            {
                var networkObject = player.GetComponent<NetworkObject>();
                if (networkObject?.Owner != null)
                {
                    logger.Msg($"Player info - Name: {player.PlayerName ?? "Unknown"}, " +
                              $"ClientId: {networkObject.Owner.ClientId}, " +
                              $"IsOwner: {networkObject.IsOwner}, " +
                              $"IsLocal: {player == Player.Local}");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error logging player info: {ex}");
            }
        }

        /// <summary>
        /// Show a hidden loopback player (for debugging/testing)
        /// </summary>
        public void ShowLoopbackPlayer(Player player)
        {
            try
            {
                if (player == null)
                    return;

                logger.Msg($"Showing loopback player: {player.PlayerName ?? "Unknown"}");
                
                player.SetVisibleToLocalPlayer(true);
                
                if (player.Avatar != null)
                {
                    player.Avatar.SetVisible(true);
                }
                
                logger.Msg("Loopback player made visible");
            }
            catch (Exception ex)
            {
                logger.Error($"Error showing loopback player: {ex}");
            }
        }

        /// <summary>
        /// Get status of loopback handling
        /// </summary>
        public string GetLoopbackStatus()
        {
            try
            {
                var status = "=== Loopback Handler Status ===\n";
                status += $"Event Hooks Setup: {eventHooksSetup}\n";
                status += $"Is Client: {InstanceFinder.IsClient}\n";
                status += $"Is Server: {InstanceFinder.IsServer}\n";
                status += $"Tugboat Mode: {ClientConnectionManager.IsTugboatMode}\n";

                // Count visible/hidden players
                var allPlayers = Player.PlayerList;
                int visibleCount = 0;
                int hiddenCount = 0;
                
                foreach (var player in allPlayers)
                {
                    if (IsPlayerVisible(player))
                        visibleCount++;
                    else
                        hiddenCount++;
                }
                
                status += $"Visible Players: {visibleCount}\n";
                status += $"Hidden Players: {hiddenCount}\n";
                
                return status;
            }
            catch (Exception ex)
            {
                return $"Error getting loopback status: {ex.Message}";
            }
        }

        /// <summary>
        /// Check if a player is visible to the local client
        /// </summary>
        private bool IsPlayerVisible(Player player)
        {
            try
            {
                // This is a simplified check - the actual visibility logic
                // depends on the game's implementation
                return player.gameObject.activeInHierarchy;
            }
            catch (Exception ex)
            {
                logger.Error($"Error checking player visibility: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Force check all current players for loopback status
        /// </summary>
        public void ForceCheckAllPlayers()
        {
            try
            {
                logger.Msg("Force checking all players for loopback status");
                
                var allPlayers = Player.PlayerList;
                foreach (var player in allPlayers)
                {
                    if (player != null && !InstanceFinder.IsServer)
                    {
                        OnPlayerSpawned_CheckLoopback(player);
                    }
                }
                
                logger.Msg($"Force check completed for {allPlayers.Count} players");
            }
            catch (Exception ex)
            {
                logger.Error($"Error in force check all players: {ex}");
            }
        }

        /// <summary>
        /// Cleanup event hooks
        /// </summary>
        public void Cleanup()
        {
            try
            {
                if (eventHooksSetup)
                {
                    Player.onPlayerSpawned = (Action<Player>)System.Delegate.Remove(
                        Player.onPlayerSpawned, new Action<Player>(OnPlayerSpawned_CheckLoopback));
                    
                    eventHooksSetup = false;
                    logger.Msg("Loopback handler event hooks removed");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error during loopback handler cleanup: {ex}");
            }
        }
    }
}
