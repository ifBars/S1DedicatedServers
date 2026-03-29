using System;
using System.Collections.Generic;
using System.Linq;
using DedicatedServerMod.Client.Permissions;
using MelonLoader;
#if IL2CPP
using Il2CppFishNet;
#else
using FishNet;
#endif

namespace DedicatedServerMod.Client.Managers
{
    /// <summary>
    /// Compatibility facade over the server-authored client permission snapshot.
    /// </summary>
    public static class AdminStatusManager
    {
        private static readonly HashSet<string> BuiltInServerCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "help",
            "serverinfo",
            "save",
            "reloadconfig",
            "shutdown",
            "listplayers",
            "kick",
            "ban",
            "unban",
            "reloadpermissions",
            "perm",
            "group",
            "op",
            "admin",
            "deop",
            "deadmin",
            "listops",
            "listadmins"
        };

        private static MelonLogger.Instance _logger;

        /// <summary>
        /// Initializes the local permission snapshot facade.
        /// </summary>
        /// <param name="loggerInstance">The logger instance.</param>
        public static void Initialize(MelonLogger.Instance loggerInstance)
        {
            _logger = loggerInstance;
            PermissionSnapshotStore.Initialize(loggerInstance);
            _logger?.Msg("AdminStatusManager initialized from permission snapshots");
        }

        /// <summary>
        /// Gets whether the local player has elevated console access.
        /// </summary>
        public static bool IsLocalPlayerAdmin()
        {
            return InstanceFinder.IsHost || PermissionSnapshotStore.Current?.CanOpenConsole == true;
        }

        /// <summary>
        /// Gets whether the local player effectively has wildcard remote console access.
        /// </summary>
        public static bool IsLocalPlayerOperator()
        {
            return InstanceFinder.IsHost || HasWildcardConsoleAccess();
        }

        /// <summary>
        /// Legacy no-op compatibility entry point.
        /// </summary>
        public static void UpdateAdminStatus(string playerId, bool isAdmin, bool isOperator)
        {
            _logger?.Warning("UpdateAdminStatus is deprecated. Client permissions now come from server snapshots.");
        }

        /// <summary>
        /// Clears the cached capability snapshot.
        /// </summary>
        public static void ClearCache()
        {
            PermissionSnapshotStore.Reset();
        }

        /// <summary>
        /// Invalidates the cached capability snapshot.
        /// </summary>
        public static void InvalidateCache()
        {
            PermissionSnapshotStore.Reset();
        }

        /// <summary>
        /// Checks whether the local player can use a remote console command according to the latest snapshot.
        /// </summary>
        /// <param name="command">The command word to evaluate.</param>
        /// <returns><see langword="true"/> when the command is allowed.</returns>
        public static bool CanUseCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return false;
            }

            if (InstanceFinder.IsHost)
            {
                return true;
            }

            if (!IsLocalPlayerAdmin())
            {
                return false;
            }

            IReadOnlyList<string> allowedCommands = PermissionSnapshotStore.Current?.AllowedRemoteCommands;
            if (allowedCommands == null)
            {
                allowedCommands = Array.Empty<string>();
            }

            string normalizedCommand = command.Trim().ToLowerInvariant();
            return BuiltInServerCommands.Contains(normalizedCommand) ||
                   allowedCommands.Contains("*") ||
                   allowedCommands.Contains(normalizedCommand);
        }

        /// <summary>
        /// Gets a human-readable description of the local snapshot.
        /// </summary>
        /// <returns>The permission summary text.</returns>
        public static string GetPermissionInfo()
        {
            if (InstanceFinder.IsHost)
            {
                return "HOST (Full console access)";
            }

            if (HasWildcardConsoleAccess())
            {
                return "STAFF (Full remote console access)";
            }

            if (IsLocalPlayerAdmin())
            {
                return "STAFF (Scoped remote console access)";
            }

            return "PLAYER (No remote console access)";
        }

        /// <summary>
        /// Gets whether the latest snapshot allows opening the remote console.
        /// </summary>
        public static bool CanOpenConsole()
        {
            return IsLocalPlayerAdmin();
        }

        private static bool HasWildcardConsoleAccess()
        {
            IReadOnlyList<string> allowedCommands = PermissionSnapshotStore.Current?.AllowedRemoteCommands;
            if (allowedCommands == null)
            {
                allowedCommands = Array.Empty<string>();
            }
            return allowedCommands.Contains("*");
        }
    }
}
