using System;
using System.Collections.Generic;
using DedicatedServerMod.Shared.Permissions;
using MelonLoader;

namespace DedicatedServerMod.Client.Permissions
{
    /// <summary>
    /// Stores the latest server-authored permission capability snapshot for the local player.
    /// </summary>
    public static class PermissionSnapshotStore
    {
        private static MelonLogger.Instance _logger;
        private static PermissionCapabilitySnapshot _current;

        /// <summary>
        /// Raised after the local snapshot changes.
        /// </summary>
        public static event Action<PermissionCapabilitySnapshot> SnapshotUpdated;

        /// <summary>
        /// Gets the current local snapshot.
        /// </summary>
        public static PermissionCapabilitySnapshot Current => _current;

        /// <summary>
        /// Initializes the snapshot store.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        public static void Initialize(MelonLogger.Instance logger)
        {
            _logger = logger;
            Reset();
        }

        /// <summary>
        /// Replaces the current snapshot.
        /// </summary>
        /// <param name="snapshot">The latest snapshot from the server.</param>
        public static void Update(PermissionCapabilitySnapshot snapshot)
        {
            _current = snapshot ?? new PermissionCapabilitySnapshot
            {
                AllowedRemoteCommands = new List<string>()
            };

            _logger?.Msg($"Permission capability snapshot updated: open={_current.CanOpenConsole}, remote={_current.CanUseRemoteConsole}");
            SnapshotUpdated?.Invoke(_current);
        }

        /// <summary>
        /// Clears the current snapshot.
        /// </summary>
        public static void Reset()
        {
            _current = new PermissionCapabilitySnapshot
            {
                AllowedRemoteCommands = new List<string>()
            };
            SnapshotUpdated?.Invoke(_current);
        }
    }
}
