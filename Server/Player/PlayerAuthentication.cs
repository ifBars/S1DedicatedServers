using System;
using System.Collections.Generic;
using System.Linq;
using DedicatedServerMod.Server.Player.Auth;
using DedicatedServerMod.Shared.Configuration;
using DedicatedServerMod.Shared.Networking;
using DedicatedServerMod.Utils;
using FishNet.Connection;
using MelonLoader;

namespace DedicatedServerMod.Server.Player
{
    /// <summary>
    /// Coordinates player authentication flow, pending state, and provider backend integration.
    /// </summary>
    public class PlayerAuthentication
    {
        private readonly MelonLogger.Instance _logger;
        private readonly Dictionary<NetworkConnection, PendingAuthenticationState> _pendingAuthentications;

        private IPlayerAuthBackend _backend;

        /// <summary>
        /// Initializes a new player authentication coordinator.
        /// </summary>
        /// <param name="loggerInstance">Logger instance used by this coordinator.</param>
        public PlayerAuthentication(MelonLogger.Instance loggerInstance)
        {
            _logger = loggerInstance;
            _pendingAuthentications = new Dictionary<NetworkConnection, PendingAuthenticationState>();
        }

        /// <summary>
        /// Gets the active authentication provider currently serving requests.
        /// </summary>
        public AuthenticationProvider ActiveProvider =>
            _backend?.Provider ?? AuthenticationProvider.None;

        /// <summary>
        /// Initializes authentication backend resources.
        /// </summary>
        public void Initialize()
        {
            _pendingAuthentications.Clear();

            AuthenticationProvider provider = ResolveConfiguredProvider();
            _backend = CreateBackend(provider);

            AuthenticationResult initResult = _backend.Initialize();
            if (!initResult.IsSuccessful)
            {
                _logger.Error($"Authentication backend initialization failed: {initResult.Message}");
            }
            else
            {
                _logger.Msg($"Player authentication system initialized with provider {ActiveProvider}");
            }
        }

        /// <summary>
        /// Creates and records a challenge for an incoming auth handshake.
        /// </summary>
        /// <param name="playerInfo">Player connection info.</param>
        /// <returns>Challenge payload when auth is required; null when no challenge is needed.</returns>
        public AuthChallengeMessage CreateChallenge(ConnectedPlayerInfo playerInfo)
        {
            if (playerInfo == null)
            {
                return null;
            }

            if (!IsAuthenticationRequiredForPlayer(playerInfo))
            {
                if (!playerInfo.IsAuthenticated)
                {
                    AuthenticationResult bypassResult = new AuthenticationResult
                    {
                        IsSuccessful = true,
                        Message = ServerConfig.Instance.RequireAuthentication
                            ? "Authentication bypassed for loopback connection"
                            : "Authentication not required"
                    };

                    ApplyResult(playerInfo, bypassResult);
                }

                return null;
            }

            if (_backend == null)
            {
                Initialize();
            }

            string nonce = Guid.NewGuid().ToString("N");
            DateTime startedAtUtc = DateTime.UtcNow;

            _pendingAuthentications[playerInfo.Connection] = new PendingAuthenticationState
            {
                PlayerInfo = playerInfo,
                Nonce = nonce,
                StartedAtUtc = startedAtUtc
            };

            playerInfo.IsAuthenticated = false;
            playerInfo.IsAuthenticationPending = true;
            playerInfo.AuthStartedAtUtc = startedAtUtc;
            playerInfo.AuthenticationNonce = nonce;
            playerInfo.LastAuthenticationMessage = "Authentication challenge issued";

            return new AuthChallengeMessage
            {
                Provider = ToWireProviderName(ActiveProvider),
                Nonce = nonce,
                TimeoutSeconds = ServerConfig.Instance.AuthTimeoutSeconds,
                WebApiIdentity = ActiveProvider == AuthenticationProvider.SteamWebApi
                    ? (ServerConfig.Instance.SteamWebApiIdentity ?? string.Empty)
                    : string.Empty
            };
        }

        /// <summary>
        /// Processes a submitted auth ticket for a pending player.
        /// </summary>
        /// <param name="playerInfo">Player state being authenticated.</param>
        /// <param name="ticketMessage">Ticket payload from client.</param>
        /// <returns>Immediate begin result. Pending backend results arrive asynchronously.</returns>
        public AuthenticationResult SubmitTicket(ConnectedPlayerInfo playerInfo, AuthTicketMessage ticketMessage)
        {
            if (playerInfo == null)
            {
                return new AuthenticationResult
                {
                    IsSuccessful = false,
                    Message = "Player info is required",
                    ShouldDisconnect = true
                };
            }

            if (!IsAuthenticationRequiredForPlayer(playerInfo))
            {
                AuthenticationResult bypassResult = new AuthenticationResult
                {
                    IsSuccessful = true,
                    Message = "Authentication bypassed"
                };

                ApplyResult(playerInfo, bypassResult);
                return bypassResult;
            }

            if (ticketMessage == null)
            {
                AuthenticationResult missingPayloadResult = new AuthenticationResult
                {
                    IsSuccessful = false,
                    Message = "Authentication payload missing",
                    ShouldDisconnect = true
                };
                ApplyResult(playerInfo, missingPayloadResult);
                return missingPayloadResult;
            }

            if (!_pendingAuthentications.TryGetValue(playerInfo.Connection, out PendingAuthenticationState pendingState))
            {
                AuthenticationResult noChallengeResult = new AuthenticationResult
                {
                    IsSuccessful = false,
                    Message = "Authentication handshake not initialized",
                    ShouldDisconnect = true
                };
                ApplyResult(playerInfo, noChallengeResult);
                return noChallengeResult;
            }

            if (!string.Equals(pendingState.Nonce, ticketMessage.Nonce, StringComparison.Ordinal))
            {
                AuthenticationResult nonceMismatchResult = new AuthenticationResult
                {
                    IsSuccessful = false,
                    Message = "Authentication nonce mismatch",
                    ShouldDisconnect = true
                };
                ApplyResult(playerInfo, nonceMismatchResult);
                return nonceMismatchResult;
            }

            if (!IsProviderPayloadCompatible(ticketMessage.Provider, ActiveProvider))
            {
                AuthenticationResult providerMismatchResult = new AuthenticationResult
                {
                    IsSuccessful = false,
                    Message = "Authentication provider mismatch",
                    ShouldDisconnect = true
                };
                ApplyResult(playerInfo, providerMismatchResult);
                return providerMismatchResult;
            }

            if (IsPlayerBanned(ticketMessage.SteamId))
            {
                AuthenticationResult bannedResult = new AuthenticationResult
                {
                    IsSuccessful = false,
                    Message = "Player is banned",
                    ShouldDisconnect = true
                };
                ApplyResult(playerInfo, bannedResult);
                return bannedResult;
            }

            AuthBeginResult beginResult = _backend.BeginAuthentication(playerInfo.Connection, ticketMessage);
            if (beginResult.IsPending)
            {
                return new AuthenticationResult
                {
                    IsSuccessful = true,
                    IsPending = true,
                    Message = "Authentication pending Steam validation"
                };
            }

            AuthenticationResult immediateResult = beginResult.ImmediateResult ?? new AuthenticationResult
            {
                IsSuccessful = false,
                Message = "Authentication backend did not return a result",
                ShouldDisconnect = true
            };

            if (immediateResult.IsSuccessful && string.IsNullOrEmpty(immediateResult.ExtractedSteamId))
            {
                immediateResult.ExtractedSteamId = ticketMessage.SteamId;
            }

            ApplyResult(playerInfo, immediateResult);
            return immediateResult;
        }

        /// <summary>
        /// Ticks backend callbacks and enforces timeout for pending authentications.
        /// </summary>
        public void Tick()
        {
            if (_backend == null)
            {
                return;
            }

            _backend.Tick();

            IReadOnlyList<AuthCompletion> completions = _backend.DrainCompletions();
            foreach (AuthCompletion completion in completions)
            {
                if (completion?.Connection == null || completion.Result == null)
                {
                    continue;
                }

                if (!_pendingAuthentications.TryGetValue(completion.Connection, out PendingAuthenticationState state))
                {
                    continue;
                }

                AuthenticationResult result = completion.Result;
                if (result.IsSuccessful && IsPlayerBanned(result.ExtractedSteamId))
                {
                    result = new AuthenticationResult
                    {
                        IsSuccessful = false,
                        Message = "Player is banned",
                        ShouldDisconnect = true,
                        ExtractedSteamId = result.ExtractedSteamId
                    };
                }

                ApplyResult(state.PlayerInfo, result);
            }

            if (_pendingAuthentications.Count == 0)
            {
                return;
            }

            DateTime nowUtc = DateTime.UtcNow;
            TimeSpan timeout = TimeSpan.FromSeconds(ServerConfig.Instance.AuthTimeoutSeconds);
            List<PendingAuthenticationState> timedOutStates = _pendingAuthentications.Values
                .Where(state => nowUtc - state.StartedAtUtc > timeout)
                .ToList();

            foreach (PendingAuthenticationState state in timedOutStates)
            {
                AuthenticationResult timeoutResult = new AuthenticationResult
                {
                    IsSuccessful = false,
                    Message = $"Authentication timed out after {ServerConfig.Instance.AuthTimeoutSeconds}s",
                    ShouldDisconnect = true
                };

                ApplyResult(state.PlayerInfo, timeoutResult);
            }
        }

        /// <summary>
        /// Removes pending/session authentication state for a disconnected player.
        /// </summary>
        /// <param name="playerInfo">Disconnected player info.</param>
        public void HandlePlayerDisconnected(ConnectedPlayerInfo playerInfo)
        {
            if (playerInfo == null)
            {
                return;
            }

            if (playerInfo.Connection != null)
            {
                _pendingAuthentications.Remove(playerInfo.Connection);
            }

            if (_backend != null)
            {
                if (!string.IsNullOrEmpty(playerInfo.AuthenticatedSteamId))
                {
                    _backend.EndSession(playerInfo.AuthenticatedSteamId);
                }
                else if (!string.IsNullOrEmpty(playerInfo.SteamId))
                {
                    _backend.EndSession(playerInfo.SteamId);
                }
            }
        }

        /// <summary>
        /// Shuts down authentication backend resources.
        /// </summary>
        public void Shutdown()
        {
            _pendingAuthentications.Clear();
            _backend?.Shutdown();
        }

        /// <summary>
        /// Checks whether authentication is required for a specific player connection.
        /// </summary>
        /// <param name="playerInfo">Player to evaluate.</param>
        /// <returns>True when authentication must be completed.</returns>
        public bool IsAuthenticationRequiredForPlayer(ConnectedPlayerInfo playerInfo)
        {
            if (playerInfo == null)
            {
                return false;
            }

            if (!ServerConfig.Instance.RequireAuthentication)
            {
                return false;
            }

            return !ShouldBypassAuthentication(playerInfo);
        }

        /// <summary>
        /// Determines whether this player should bypass authentication checks.
        /// </summary>
        /// <param name="playerInfo">Player being evaluated.</param>
        /// <returns>True when bypass rules apply.</returns>
        public bool ShouldBypassAuthentication(ConnectedPlayerInfo playerInfo)
        {
            if (playerInfo == null || !ServerConfig.Instance.AuthAllowLoopbackBypass)
            {
                return false;
            }

            if (playerInfo.IsLoopbackConnection)
            {
                return true;
            }

            if (playerInfo.Connection != null && (playerInfo.Connection.IsLocalClient || playerInfo.Connection.ClientId == 0))
            {
                return true;
            }

            if (playerInfo.PlayerInstance != null && GhostHostIdentifier.IsGhostHost(playerInfo.PlayerInstance))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Check if a player should be allowed based on friends requirement.
        /// </summary>
        /// <param name="steamId">SteamID to evaluate.</param>
        /// <param name="hostSteamId">Optional host SteamID context.</param>
        /// <returns>True when player is allowed to join.</returns>
        public bool ShouldAllowPlayer(string steamId, string hostSteamId = null)
        {
            if (IsPlayerBanned(steamId))
            {
                _logger.Msg($"Player {steamId} is banned, denying access");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Check if a player is banned.
        /// </summary>
        /// <param name="steamId">SteamID to check.</param>
        /// <returns>True when the player is banned.</returns>
        public bool IsPlayerBanned(string steamId)
        {
            if (string.IsNullOrEmpty(steamId))
            {
                return false;
            }

            return ServerConfig.Instance.BannedPlayers.Contains(steamId);
        }

        /// <summary>
        /// Raised when authentication reaches a terminal success/failure result.
        /// </summary>
        public event Action<ConnectedPlayerInfo, AuthenticationResult> AuthenticationCompleted;

        private AuthenticationProvider ResolveConfiguredProvider()
        {
            if (!ServerConfig.Instance.RequireAuthentication)
            {
                return AuthenticationProvider.None;
            }

            return ServerConfig.Instance.AuthProvider;
        }

        private IPlayerAuthBackend CreateBackend(AuthenticationProvider provider)
        {
            switch (provider)
            {
                case AuthenticationProvider.None:
                    return new NoAuthenticationBackend(_logger);
                case AuthenticationProvider.SteamWebApi:
                    return new SteamWebApiAuthBackend(_logger);
                case AuthenticationProvider.SteamGameServer:
                default:
                    return new SteamGameServerAuthBackend(_logger);
            }
        }

        private void ApplyResult(ConnectedPlayerInfo playerInfo, AuthenticationResult result)
        {
            if (playerInfo == null || result == null)
            {
                return;
            }

            if (playerInfo.Connection != null)
            {
                _pendingAuthentications.Remove(playerInfo.Connection);
            }

            playerInfo.IsAuthenticationPending = false;
            playerInfo.AuthenticationNonce = null;
            playerInfo.AuthStartedAtUtc = null;
            playerInfo.LastAuthenticationAttemptUtc = DateTime.UtcNow;
            playerInfo.LastAuthenticationMessage = result.Message;

            if (result.IsSuccessful)
            {
                playerInfo.IsAuthenticated = true;

                if (!string.IsNullOrEmpty(result.ExtractedSteamId))
                {
                    playerInfo.AuthenticatedSteamId = result.ExtractedSteamId;
                    playerInfo.SteamId = result.ExtractedSteamId;
                }
            }
            else
            {
                playerInfo.IsAuthenticated = false;
                playerInfo.AuthenticatedSteamId = null;
            }

            try
            {
                AuthenticationCompleted?.Invoke(playerInfo, result);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error in AuthenticationCompleted event handler: {ex}");
            }
        }

        private static string ToWireProviderName(AuthenticationProvider provider)
        {
            switch (provider)
            {
                case AuthenticationProvider.None:
                    return "none";
                case AuthenticationProvider.SteamWebApi:
                    return "steam_web_api";
                case AuthenticationProvider.SteamGameServer:
                default:
                    return "steam_game_server";
            }
        }

        private static bool IsProviderPayloadCompatible(string providerWireValue, AuthenticationProvider activeProvider)
        {
            if (string.IsNullOrWhiteSpace(providerWireValue))
            {
                return activeProvider == AuthenticationProvider.SteamGameServer;
            }

            string normalized = providerWireValue.Trim().ToLowerInvariant();
            switch (activeProvider)
            {
                case AuthenticationProvider.None:
                    return normalized == "none";
                case AuthenticationProvider.SteamWebApi:
                    return normalized == "steam_web_api" || normalized == "steamwebapi";
                case AuthenticationProvider.SteamGameServer:
                default:
                    return normalized == "steam_game_server" || normalized == "steamgameserver";
            }
        }

        private sealed class PendingAuthenticationState
        {
            public ConnectedPlayerInfo PlayerInfo { get; set; }
            public string Nonce { get; set; }
            public DateTime StartedAtUtc { get; set; }
        }
    }

    /// <summary>
    /// Result of a player authentication attempt.
    /// </summary>
    public class AuthenticationResult
    {
        /// <summary>
        /// Whether authentication was successful.
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Whether authentication is still pending asynchronous completion.
        /// </summary>
        public bool IsPending { get; set; }

        /// <summary>
        /// Authentication result message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Whether the player should be disconnected due to auth failure.
        /// </summary>
        public bool ShouldDisconnect { get; set; }

        /// <summary>
        /// SteamID extracted from the authentication process.
        /// </summary>
        public string ExtractedSteamId { get; set; }

        /// <summary>
        /// Returns a readable description of this result.
        /// </summary>
        /// <returns>Result string.</returns>
        public override string ToString()
        {
            string state = IsPending
                ? "Pending"
                : (IsSuccessful ? "Success" : "Failed");

            return $"Auth: {state} - {Message}";
        }
    }
}
