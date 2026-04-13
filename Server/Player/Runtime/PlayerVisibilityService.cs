using DedicatedServerMod.Utils;

namespace DedicatedServerMod.Server.Player.Runtime
{
    /// <summary>
    /// Tracks per-player vanish state and applies visibility updates when player objects spawn.
    /// </summary>
    internal sealed class PlayerVisibilityService
    {
        private readonly HashSet<string> _vanishedPlayerIds = new HashSet<string>(StringComparer.Ordinal);

        internal bool IsVanished(ConnectedPlayerInfo targetPlayer)
        {
            if (targetPlayer == null)
            {
                return false;
            }

            return _vanishedPlayerIds.Contains(GetPlayerKey(targetPlayer));
        }

        internal bool SetVanished(ConnectedPlayerInfo targetPlayer, bool isVanished, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (targetPlayer == null)
            {
                errorMessage = "Target player is required.";
                return false;
            }

            string playerKey = GetPlayerKey(targetPlayer);
            if (isVanished)
            {
                _vanishedPlayerIds.Add(playerKey);
            }
            else
            {
                _vanishedPlayerIds.Remove(playerKey);
            }

            return TryApplyVisibility(targetPlayer, isVanished, out errorMessage);
        }

        internal void HandlePlayerSpawned(ConnectedPlayerInfo targetPlayer)
        {
            if (!IsVanished(targetPlayer))
            {
                return;
            }

            if (!TryApplyVisibility(targetPlayer, isVanished: true, out string errorMessage) &&
                !string.IsNullOrWhiteSpace(errorMessage))
            {
                DebugLog.Warning(errorMessage);
            }
        }

        internal void HandlePlayerLeft(ConnectedPlayerInfo targetPlayer)
        {
            if (targetPlayer == null)
            {
                return;
            }

            _vanishedPlayerIds.Remove(GetPlayerKey(targetPlayer));
        }

        private static bool TryApplyVisibility(ConnectedPlayerInfo targetPlayer, bool isVanished, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (targetPlayer?.PlayerInstance == null)
            {
                return true;
            }

            try
            {
                targetPlayer.PlayerInstance.SetVisible(!isVanished, network: true);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Failed to update vanish state for {targetPlayer.DisplayName}: {ex.Message}";
                return false;
            }
        }

        private static string GetPlayerKey(ConnectedPlayerInfo targetPlayer)
        {
            if (!string.IsNullOrWhiteSpace(targetPlayer?.TrustedUniqueId))
            {
                return targetPlayer.TrustedUniqueId;
            }

            return targetPlayer?.ClientId.ToString() ?? string.Empty;
        }
    }
}
