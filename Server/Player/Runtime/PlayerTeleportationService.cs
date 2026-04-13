#if IL2CPP
using PlayerScript = Il2CppScheduleOne.PlayerScripts.Player;
using PlayerMovementType = Il2CppScheduleOne.PlayerScripts.PlayerMovement;
#else
using PlayerScript = ScheduleOne.PlayerScripts.Player;
using PlayerMovementType = ScheduleOne.PlayerScripts.PlayerMovement;
#endif
using UnityEngine;

namespace DedicatedServerMod.Server.Player.Runtime
{
    /// <summary>
    /// Tracks teleport history for connected players and applies server-authoritative teleports.
    /// </summary>
    internal sealed class PlayerTeleportationService
    {
        private const int MaxReturnHistoryEntriesPerPlayer = 8;

        private readonly Dictionary<string, List<PlayerTransformSnapshot>> _returnHistoryByPlayerId =
            new Dictionary<string, List<PlayerTransformSnapshot>>(StringComparer.Ordinal);

        internal bool Teleport(
            ConnectedPlayerInfo targetPlayer,
            Vector3 destinationPosition,
            Quaternion destinationRotation,
            bool alignFeetToPosition,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            if (!TryCaptureCurrentTransform(targetPlayer, out PlayerTransformSnapshot currentSnapshot, out errorMessage))
            {
                return false;
            }

            AddReturnSnapshot(targetPlayer, currentSnapshot);
            return TryApplyTransform(targetPlayer, destinationPosition, destinationRotation, alignFeetToPosition, out errorMessage);
        }

        internal bool ReturnToPreviousPosition(ConnectedPlayerInfo targetPlayer, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (targetPlayer == null)
            {
                errorMessage = "Target player is required.";
                return false;
            }

            string playerKey = GetPlayerKey(targetPlayer);
            if (!_returnHistoryByPlayerId.TryGetValue(playerKey, out List<PlayerTransformSnapshot> history) ||
                history == null ||
                history.Count == 0)
            {
                errorMessage = $"No previous position is recorded for {targetPlayer.DisplayName}.";
                return false;
            }

            PlayerTransformSnapshot snapshot = history[history.Count - 1];
            history.RemoveAt(history.Count - 1);
            if (history.Count == 0)
            {
                _returnHistoryByPlayerId.Remove(playerKey);
            }

            return TryApplyTransform(targetPlayer, snapshot.Position, snapshot.Rotation, alignFeetToPosition: true, out errorMessage);
        }

        internal bool HasReturnPosition(ConnectedPlayerInfo targetPlayer)
        {
            if (targetPlayer == null)
            {
                return false;
            }

            string playerKey = GetPlayerKey(targetPlayer);
            return _returnHistoryByPlayerId.TryGetValue(playerKey, out List<PlayerTransformSnapshot> history) &&
                   history != null &&
                   history.Count > 0;
        }

        internal void ClearPlayerState(ConnectedPlayerInfo targetPlayer)
        {
            if (targetPlayer == null)
            {
                return;
            }

            _returnHistoryByPlayerId.Remove(GetPlayerKey(targetPlayer));
        }

        private void AddReturnSnapshot(ConnectedPlayerInfo targetPlayer, PlayerTransformSnapshot snapshot)
        {
            string playerKey = GetPlayerKey(targetPlayer);
            if (!_returnHistoryByPlayerId.TryGetValue(playerKey, out List<PlayerTransformSnapshot> history) || history == null)
            {
                history = new List<PlayerTransformSnapshot>();
                _returnHistoryByPlayerId[playerKey] = history;
            }

            if (history.Count >= MaxReturnHistoryEntriesPerPlayer)
            {
                history.RemoveAt(0);
            }

            history.Add(snapshot);
        }

        private static bool TryCaptureCurrentTransform(
            ConnectedPlayerInfo targetPlayer,
            out PlayerTransformSnapshot snapshot,
            out string errorMessage)
        {
            snapshot = default;
            errorMessage = string.Empty;

            if (!TryGetSpawnedPlayer(targetPlayer, out var player, out errorMessage))
            {
                return false;
            }

            snapshot = new PlayerTransformSnapshot(player.transform.position, player.transform.rotation);
            return true;
        }

        private static bool TryApplyTransform(
            ConnectedPlayerInfo targetPlayer,
            Vector3 destinationPosition,
            Quaternion destinationRotation,
            bool alignFeetToPosition,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            if (!TryGetSpawnedPlayer(targetPlayer, out var player, out errorMessage))
            {
                return false;
            }

            try
            {
                PlayerMovementType movement = player.GetComponent<PlayerMovementType>();
                if (movement != null)
                {
                    movement.Teleport(destinationPosition, alignFeetToPosition);
                    movement.SetPlayerRotation(destinationRotation);
                }
                else
                {
                    player.transform.position = destinationPosition;
                    player.transform.rotation = destinationRotation;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Failed to teleport {targetPlayer.DisplayName}: {ex.Message}";
                return false;
            }
        }

        private static bool TryGetSpawnedPlayer(ConnectedPlayerInfo targetPlayer, out PlayerScript player, out string errorMessage)
        {
            player = targetPlayer?.PlayerInstance;
            if (targetPlayer == null)
            {
                errorMessage = "Target player is required.";
                return false;
            }

            if (player == null)
            {
                errorMessage = $"{targetPlayer.DisplayName} is not spawned.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private static string GetPlayerKey(ConnectedPlayerInfo targetPlayer)
        {
            if (!string.IsNullOrWhiteSpace(targetPlayer?.TrustedUniqueId))
            {
                return targetPlayer.TrustedUniqueId;
            }

            return targetPlayer?.ClientId.ToString() ?? string.Empty;
        }

        private readonly struct PlayerTransformSnapshot
        {
            internal PlayerTransformSnapshot(Vector3 position, Quaternion rotation)
            {
                Position = position;
                Rotation = rotation;
            }

            internal Vector3 Position { get; }

            internal Quaternion Rotation { get; }
        }
    }
}
