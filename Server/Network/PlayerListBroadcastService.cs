#if SERVER
using System;
using System.Collections;
using System.Collections.Generic;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Shared;
using DedicatedServerMod.Shared.Networking;
using DedicatedServerMod.Utils;
using MelonLoader;
using Newtonsoft.Json;
using UnityEngine;

namespace DedicatedServerMod.Server.Network
{
    /// <summary>
    /// Periodically broadcasts the connected player list — containing each visible player's
    /// display name and last-reported ping — to all clients.
    /// </summary>
    /// <remarks>
    /// Runs a MelonLoader coroutine that fires once per <see cref="BroadcastIntervalSeconds"/>.
    /// The ghost-host loopback connection is always excluded from the snapshot.
    ///
    /// Ping values are client-reported: each client sends its own measured RTT to the server via
    /// <see cref="Constants.Messages.PlayerPingReport"/>; the server stores it on
    /// <see cref="ConnectedPlayerInfo.PingMs"/> and includes it here.
    /// </remarks>
    internal sealed class PlayerListBroadcastService
    {
        private const float BroadcastIntervalSeconds = 1.0f;

        private readonly MelonLogger.Instance _logger;
        private readonly PlayerManager _playerManager;
        private bool _running;

        /// <summary>
        /// Initialises the service with required dependencies.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        /// <param name="playerManager">The server player manager used to enumerate connected players.</param>
        /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
        public PlayerListBroadcastService(MelonLogger.Instance logger, PlayerManager playerManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _playerManager = playerManager ?? throw new ArgumentNullException(nameof(playerManager));
        }

        /// <summary>
        /// Starts the periodic broadcast loop.
        /// </summary>
        public void Start()
        {
            _running = true;
            MelonCoroutines.Start(BroadcastLoop());
            DebugLog.Debug("PlayerListBroadcastService started");
        }

        /// <summary>
        /// Stops the periodic broadcast loop.
        /// </summary>
        public void Stop()
        {
            _running = false;
        }

        // ── Private ──────────────────────────────────────────────────────────

        private IEnumerator BroadcastLoop()
        {
            while (_running)
            {
                yield return new WaitForSeconds(BroadcastIntervalSeconds);
                try
                {
                    BroadcastPlayerList();
                }
                catch (Exception ex)
                {
                    _logger.Warning($"PlayerListBroadcastService: broadcast error: {ex.Message}");
                }
            }
        }

        private void BroadcastPlayerList()
        {
            List<ConnectedPlayerInfo> players = _playerManager.GetConnectedPlayers();

            var snapshot = new PlayerListSnapshot();
            foreach (ConnectedPlayerInfo info in players)
            {
                if (info == null || info.IsLoopbackConnection || !info.IsConnected)
                    continue;

                snapshot.Players.Add(new PlayerListEntry
                {
                    DisplayName = info.DisplayName,
                    PingMs = info.PingMs
                });
            }

            string json = JsonConvert.SerializeObject(snapshot);
            CustomMessaging.BroadcastToClients(Constants.Messages.PlayerListUpdate, json);

            DebugLog.Verbose($"PlayerListBroadcastService: sent {snapshot.Players.Count} player(s)");
        }
    }
}
#endif
