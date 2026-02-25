using System;
using System.Collections.Generic;
using System.Globalization;
using DedicatedServerMod.Shared.Configuration;
using FishNet.Connection;
using MelonLoader;
using Steamworks;

namespace DedicatedServerMod.Server.Player.Auth
{
    /// <summary>
    /// Steam game server authentication backend using BeginAuthSession and callback validation.
    /// </summary>
    public sealed class SteamGameServerAuthBackend : IPlayerAuthBackend
    {
        private readonly MelonLogger.Instance _logger;
        private readonly Dictionary<ulong, NetworkConnection> _pendingBySteamId;
        private readonly Dictionary<ulong, NetworkConnection> _activeBySteamId;
        private readonly List<AuthCompletion> _completionBuffer;

        private Callback<ValidateAuthTicketResponse_t> _validateAuthCallback;
        private Callback<SteamServersConnected_t> _serversConnectedCallback;
        private Callback<SteamServerConnectFailure_t> _serverConnectFailureCallback;
        private Callback<SteamServersDisconnected_t> _serversDisconnectedCallback;

        private bool _isInitialized;
        private bool _isAdvertisingActive;

        /// <summary>
        /// Initializes a new steam game server authentication backend.
        /// </summary>
        /// <param name="logger">Logger used by this backend.</param>
        public SteamGameServerAuthBackend(MelonLogger.Instance logger)
        {
            _logger = logger;
            _pendingBySteamId = new Dictionary<ulong, NetworkConnection>();
            _activeBySteamId = new Dictionary<ulong, NetworkConnection>();
            _completionBuffer = new List<AuthCompletion>();
        }

        /// <inheritdoc />
        public AuthenticationProvider Provider => AuthenticationProvider.SteamGameServer;

        /// <inheritdoc />
        public bool IsInitialized => _isInitialized;

        /// <inheritdoc />
        public AuthenticationResult Initialize()
        {
            if (_isInitialized)
            {
                return new AuthenticationResult
                {
                    IsSuccessful = true,
                    Message = "Steam game server auth backend already initialized"
                };
            }

            try
            {
                ServerConfig config = ServerConfig.Instance;
                EServerMode serverMode = ToSteamServerMode(config.SteamGameServerMode);

                bool initialized = GameServer.Init(
                    0u,
                    (ushort)config.ServerPort,
                    (ushort)config.SteamGameServerQueryPort,
                    serverMode,
                    config.SteamGameServerVersion);

                if (!initialized)
                {
                    return new AuthenticationResult
                    {
                        IsSuccessful = false,
                        Message = "Failed to initialize Steam game server API"
                    };
                }

                RegisterCallbacks();
                
                SteamGameServer.SetProduct(GetSteamProductString());
                SteamGameServer.SetDedicatedServer(true);
                SteamGameServer.SetServerName(config.ServerName);
                SteamGameServer.SetGameDescription("Schedule I");
                SteamGameServer.SetModDir("Schedule I");
                SteamGameServer.SetMapName("Main");
                SteamGameServer.SetMaxPlayerCount(config.MaxPlayers);
                SteamGameServer.SetPasswordProtected(!string.IsNullOrEmpty(config.ServerPassword));
                SteamGameServer.SetGameTags($"ver:{config.SteamGameServerVersion}");

                if (config.SteamGameServerLogOnAnonymous)
                {
                    SteamGameServer.LogOnAnonymous();
                    _logger.Msg("Steam game server auth backend logging on anonymously");
                }
                else
                {
                    SteamGameServer.LogOn(config.SteamGameServerToken ?? string.Empty);
                    _logger.Msg("Steam game server auth backend logging on using token");
                }

                TrySetAdvertiseServerActive(true);

                _isInitialized = true;

                return new AuthenticationResult
                {
                    IsSuccessful = true,
                    Message = "Steam game server auth backend initialized"
                };
            }
            catch (Exception ex)
            {
                _logger.Error($"Steam game server auth backend initialization failed: {ex}");
                return new AuthenticationResult
                {
                    IsSuccessful = false,
                    Message = $"Steam game server auth backend initialization failed: {ex.Message}"
                };
            }
        }

        /// <inheritdoc />
        public AuthBeginResult BeginAuthentication(NetworkConnection connection, Shared.Networking.AuthTicketMessage ticketMessage)
        {
            if (!_isInitialized)
            {
                return new AuthBeginResult
                {
                    IsPending = false,
                    ImmediateResult = new AuthenticationResult
                    {
                        IsSuccessful = false,
                        Message = "Steam game server auth backend is not initialized",
                        ShouldDisconnect = true
                    }
                };
            }

            if (!SteamGameServer.BLoggedOn())
            {
                return new AuthBeginResult
                {
                    IsPending = false,
                    ImmediateResult = new AuthenticationResult
                    {
                        IsSuccessful = false,
                        Message = "Steam game server is not logged on yet; retrying is allowed",
                        ShouldDisconnect = false
                    }
                };
            }

            if (connection == null)
            {
                return new AuthBeginResult
                {
                    IsPending = false,
                    ImmediateResult = new AuthenticationResult
                    {
                        IsSuccessful = false,
                        Message = "Connection is null",
                        ShouldDisconnect = true
                    }
                };
            }

            if (ticketMessage == null)
            {
                return new AuthBeginResult
                {
                    IsPending = false,
                    ImmediateResult = new AuthenticationResult
                    {
                        IsSuccessful = false,
                        Message = "Authentication ticket payload missing",
                        ShouldDisconnect = true
                    }
                };
            }

            if (!TryParseSteamId(ticketMessage.SteamId, out ulong steamIdValue))
            {
                return new AuthBeginResult
                {
                    IsPending = false,
                    ImmediateResult = new AuthenticationResult
                    {
                        IsSuccessful = false,
                        Message = "Invalid SteamID format in auth payload",
                        ShouldDisconnect = true
                    }
                };
            }

            if (!TryDecodeHex(ticketMessage.TicketHex, out byte[] ticketBytes))
            {
                return new AuthBeginResult
                {
                    IsPending = false,
                    ImmediateResult = new AuthenticationResult
                    {
                        IsSuccessful = false,
                        Message = "Invalid ticket format in auth payload",
                        ShouldDisconnect = true
                    }
                };
            }

            if (_pendingBySteamId.ContainsKey(steamIdValue))
            {
                return new AuthBeginResult
                {
                    IsPending = false,
                    ImmediateResult = new AuthenticationResult
                    {
                        IsSuccessful = false,
                        Message = "Authentication is already pending for this SteamID",
                        ShouldDisconnect = true
                    }
                };
            }

            CSteamID steamId = new CSteamID(steamIdValue);
            EBeginAuthSessionResult beginResult = SteamGameServer.BeginAuthSession(ticketBytes, ticketBytes.Length, steamId);

            if (beginResult != EBeginAuthSessionResult.k_EBeginAuthSessionResultOK)
            {
                return new AuthBeginResult
                {
                    IsPending = false,
                    ImmediateResult = BuildBeginFailureResult(beginResult)
                };
            }

            _pendingBySteamId[steamIdValue] = connection;

            return new AuthBeginResult
            {
                IsPending = true,
                ImmediateResult = null
            };
        }

        /// <inheritdoc />
        public void Tick()
        {
            if (!_isInitialized)
            {
                return;
            }

            try
            {
                GameServer.RunCallbacks();
                if (SteamGameServer.BLoggedOn() && !_isAdvertisingActive)
                {
                    TrySetAdvertiseServerActive(true);
                }
            }
            catch (Exception ex)
            {
                _logger.Warning($"Steam game server callback pump failed: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<AuthCompletion> DrainCompletions()
        {
            if (_completionBuffer.Count == 0)
            {
                return Array.Empty<AuthCompletion>();
            }

            List<AuthCompletion> snapshot = new List<AuthCompletion>(_completionBuffer);
            _completionBuffer.Clear();
            return snapshot;
        }

        /// <inheritdoc />
        public void EndSession(string steamId)
        {
            if (!_isInitialized || string.IsNullOrWhiteSpace(steamId))
            {
                return;
            }

            if (!TryParseSteamId(steamId, out ulong steamIdValue))
            {
                return;
            }

            try
            {
                SteamGameServer.EndAuthSession(new CSteamID(steamIdValue));
            }
            catch (Exception ex)
            {
                _logger.Warning($"Failed to end auth session for {steamId}: {ex.Message}");
            }

            _pendingBySteamId.Remove(steamIdValue);
            _activeBySteamId.Remove(steamIdValue);
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            if (!_isInitialized)
            {
                return;
            }

            try
            {
                foreach (ulong steamId in _activeBySteamId.Keys)
                {
                    try
                    {
                        SteamGameServer.EndAuthSession(new CSteamID(steamId));
                    }
                    catch
                    {
                    }
                }

                foreach (ulong steamId in _pendingBySteamId.Keys)
                {
                    try
                    {
                        SteamGameServer.EndAuthSession(new CSteamID(steamId));
                    }
                    catch
                    {
                    }
                }

                _activeBySteamId.Clear();
                _pendingBySteamId.Clear();
                _completionBuffer.Clear();

                TrySetAdvertiseServerActive(false);
                SteamGameServer.LogOff();
                GameServer.Shutdown();
            }
            catch (Exception ex)
            {
                _logger.Warning($"Steam game server auth backend shutdown encountered an error: {ex.Message}");
            }
            finally
            {
                _validateAuthCallback = null;
                _serversConnectedCallback = null;
                _serverConnectFailureCallback = null;
                _serversDisconnectedCallback = null;
                _isInitialized = false;
            }
        }

        private void RegisterCallbacks()
        {
            _validateAuthCallback = Callback<ValidateAuthTicketResponse_t>.CreateGameServer(OnValidateAuthTicketResponse);
            _serversConnectedCallback = Callback<SteamServersConnected_t>.CreateGameServer(_ =>
            {
                _logger.Msg("Steam game server connected to Steam backend");
                TrySetAdvertiseServerActive(true);
            });
            _serverConnectFailureCallback = Callback<SteamServerConnectFailure_t>.CreateGameServer(data =>
            {
                _logger.Warning($"Steam game server connection failure: {data.m_eResult}");
            });
            _serversDisconnectedCallback = Callback<SteamServersDisconnected_t>.CreateGameServer(data =>
            {
                _isAdvertisingActive = false;
                _logger.Warning($"Steam game server disconnected from Steam backend: {data.m_eResult}");
            });
        }

        private void TrySetAdvertiseServerActive(bool active)
        {
            try
            {
                SteamGameServer.SetAdvertiseServerActive(active);
                _isAdvertisingActive = active;
                _logger.Msg($"Steam server advertising {(active ? "enabled" : "disabled")}");
            }
            catch (Exception ex)
            {
                _logger.Warning($"Failed to set Steam server advertising to {active}: {ex.Message}");
            }
        }

        private static string GetSteamProductString()
        {
            return "3164500";
        }

        private void OnValidateAuthTicketResponse(ValidateAuthTicketResponse_t callbackData)
        {
            ulong steamIdValue = callbackData.m_SteamID.m_SteamID;
            NetworkConnection connection = null;

            if (_pendingBySteamId.TryGetValue(steamIdValue, out NetworkConnection pendingConnection))
            {
                connection = pendingConnection;
                _pendingBySteamId.Remove(steamIdValue);
            }
            else if (_activeBySteamId.TryGetValue(steamIdValue, out NetworkConnection activeConnection))
            {
                connection = activeConnection;
            }

            if (connection == null)
            {
                _logger.Warning($"Received auth callback for unknown SteamID {steamIdValue}");
                return;
            }

            AuthenticationResult result = BuildValidateResult(callbackData.m_eAuthSessionResponse, steamIdValue);

            if (result.IsSuccessful)
            {
                _activeBySteamId[steamIdValue] = connection;
            }
            else
            {
                _activeBySteamId.Remove(steamIdValue);

                try
                {
                    SteamGameServer.EndAuthSession(new CSteamID(steamIdValue));
                }
                catch
                {
                }
            }

            _completionBuffer.Add(new AuthCompletion
            {
                Connection = connection,
                Result = result
            });
        }

        private static bool TryParseSteamId(string steamId, out ulong value)
        {
            return ulong.TryParse(steamId, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value > 0;
        }

        private static bool TryDecodeHex(string hex, out byte[] bytes)
        {
            bytes = Array.Empty<byte>();

            if (string.IsNullOrWhiteSpace(hex))
            {
                return false;
            }

            string trimmed = hex.Trim();
            if ((trimmed.Length % 2) != 0)
            {
                return false;
            }

            byte[] parsed = new byte[trimmed.Length / 2];
            for (int i = 0; i < parsed.Length; i++)
            {
                string segment = trimmed.Substring(i * 2, 2);
                if (!byte.TryParse(segment, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out parsed[i]))
                {
                    return false;
                }
            }

            bytes = parsed;
            return true;
        }

        private static EServerMode ToSteamServerMode(SteamGameServerAuthenticationMode mode)
        {
            switch (mode)
            {
                case SteamGameServerAuthenticationMode.NoAuthentication:
                    return EServerMode.eServerModeNoAuthentication;
                case SteamGameServerAuthenticationMode.AuthenticationAndSecure:
                    return EServerMode.eServerModeAuthenticationAndSecure;
                case SteamGameServerAuthenticationMode.Authentication:
                default:
                    return EServerMode.eServerModeAuthentication;
            }
        }

        private static AuthenticationResult BuildBeginFailureResult(EBeginAuthSessionResult beginResult)
        {
            switch (beginResult)
            {
                case EBeginAuthSessionResult.k_EBeginAuthSessionResultInvalidTicket:
                    return new AuthenticationResult
                    {
                        IsSuccessful = false,
                        Message = "Steam ticket rejected: invalid ticket",
                        ShouldDisconnect = true
                    };
                case EBeginAuthSessionResult.k_EBeginAuthSessionResultDuplicateRequest:
                    return new AuthenticationResult
                    {
                        IsSuccessful = false,
                        Message = "Steam ticket rejected: duplicate request",
                        ShouldDisconnect = true
                    };
                case EBeginAuthSessionResult.k_EBeginAuthSessionResultInvalidVersion:
                    return new AuthenticationResult
                    {
                        IsSuccessful = false,
                        Message = "Steam ticket rejected: invalid ticket version",
                        ShouldDisconnect = true
                    };
                case EBeginAuthSessionResult.k_EBeginAuthSessionResultGameMismatch:
                    return new AuthenticationResult
                    {
                        IsSuccessful = false,
                        Message = "Steam ticket rejected: game mismatch",
                        ShouldDisconnect = true
                    };
                case EBeginAuthSessionResult.k_EBeginAuthSessionResultExpiredTicket:
                    return new AuthenticationResult
                    {
                        IsSuccessful = false,
                        Message = "Steam ticket rejected: expired ticket",
                        ShouldDisconnect = true
                    };
                default:
                    return new AuthenticationResult
                    {
                        IsSuccessful = false,
                        Message = $"Steam ticket rejected: {beginResult}",
                        ShouldDisconnect = true
                    };
            }
        }

        private static AuthenticationResult BuildValidateResult(EAuthSessionResponse response, ulong steamIdValue)
        {
            bool isSuccess = response == EAuthSessionResponse.k_EAuthSessionResponseOK;
            string steamId = steamIdValue.ToString(CultureInfo.InvariantCulture);

            if (isSuccess)
            {
                return new AuthenticationResult
                {
                    IsSuccessful = true,
                    Message = "Steam ticket validated",
                    ShouldDisconnect = false,
                    ExtractedSteamId = steamId
                };
            }

            string message;
            switch (response)
            {
                case EAuthSessionResponse.k_EAuthSessionResponseNoLicenseOrExpired:
                    message = "Steam authentication failed: no valid game license";
                    break;
                case EAuthSessionResponse.k_EAuthSessionResponseUserNotConnectedToSteam:
                    message = "Steam authentication failed: user is not connected to Steam";
                    break;
                case EAuthSessionResponse.k_EAuthSessionResponseAuthTicketInvalid:
                    message = "Steam authentication failed: invalid ticket";
                    break;
                case EAuthSessionResponse.k_EAuthSessionResponseAuthTicketInvalidAlreadyUsed:
                    message = "Steam authentication failed: ticket already used";
                    break;
                case EAuthSessionResponse.k_EAuthSessionResponseAuthTicketCanceled:
                    message = "Steam authentication failed: ticket was canceled";
                    break;
                case EAuthSessionResponse.k_EAuthSessionResponseVACBanned:
                    message = "Steam authentication failed: VAC banned";
                    break;
                case EAuthSessionResponse.k_EAuthSessionResponsePublisherIssuedBan:
                    message = "Steam authentication failed: publisher-issued ban";
                    break;
                case EAuthSessionResponse.k_EAuthSessionResponseLoggedInElseWhere:
                    message = "Steam authentication failed: logged in elsewhere";
                    break;
                case EAuthSessionResponse.k_EAuthSessionResponseVACCheckTimedOut:
                    message = "Steam authentication failed: VAC check timed out";
                    break;
                case EAuthSessionResponse.k_EAuthSessionResponseAuthTicketNetworkIdentityFailure:
                    message = "Steam authentication failed: network identity mismatch";
                    break;
                default:
                    message = $"Steam authentication failed: {response}";
                    break;
            }

            return new AuthenticationResult
            {
                IsSuccessful = false,
                Message = message,
                ShouldDisconnect = true,
                ExtractedSteamId = steamId
            };
        }
    }
}
