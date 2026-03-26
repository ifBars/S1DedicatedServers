using System;
using System.Collections.Generic;
using DedicatedServerMod.Shared;
using DedicatedServerMod.Utils;

namespace DedicatedServerMod.Client.Managers
{
    /// <summary>
    /// Client-side cache of the most recently received player list broadcast from the server.
    /// Updated every ~1 second via <see cref="Utils.Constants.Messages.PlayerListUpdate"/>.
    /// </summary>
    internal static class PlayerListStore
    {
        private static List<PlayerListEntry> _current = new List<PlayerListEntry>();

        /// <summary>
        /// Gets the most recently received list of connected players.
        /// Empty until the first broadcast arrives.
        /// </summary>
        internal static IReadOnlyList<PlayerListEntry> Current => _current;

        /// <summary>
        /// Raised on the main thread whenever the server sends an updated player list.
        /// </summary>
        internal static event Action<IReadOnlyList<PlayerListEntry>> OnUpdated;

        /// <summary>
        /// Replaces the cached list with the contents of <paramref name="snapshot"/> and
        /// fires <see cref="OnUpdated"/>.
        /// </summary>
        /// <param name="snapshot">The deserialized payload from the server.</param>
        internal static void Update(PlayerListSnapshot snapshot)
        {
            if (snapshot == null) return;
            _current = snapshot.Players ?? new List<PlayerListEntry>();
            try
            {
                OnUpdated?.Invoke(_current);
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"PlayerListStore.OnUpdated handler threw: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears the cached list (called on disconnect).
        /// </summary>
        internal static void Reset()
        {
            _current = new List<PlayerListEntry>();
        }
    }
}
