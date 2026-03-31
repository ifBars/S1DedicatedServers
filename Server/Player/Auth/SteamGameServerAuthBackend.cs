using System.Globalization;
using DedicatedServerMod.Shared.Configuration;
using DedicatedServerMod.Utils;
#if IL2CPP
using Il2CppFishNet.Connection;
#else
using FishNet.Connection;
#endif
#if IL2CPP
using Il2CppSteamworks;
#else
using Steamworks;
#endif

namespace DedicatedServerMod.Server.Player.Auth
{
    /// <summary>
    /// Steam game server authentication backend using BeginAuthSession and callback validation.
    /// </summary>
    public sealed class SteamGameServerAuthBackend : IPlayerAuthBackend
    {
        private readonly Dictionary<ulong, NetworkConnection> _pendingBySteamId;
        private readonly Dictionary<ulong, NetworkConnection> _activeBySteamId;
        private readonly List<AuthCompletion> _completionBuffer;

        private Callback<ValidateAuthTicketResponse_t> _validateAuthCallback;
        private Callback<SteamServersConnected_t> _serversConnectedCallback;
        private Callback<SteamServerConnectFailure_t> _serverConnectFailureCallback;
        private Callback<SteamServersDisconnected_t> _serversDisconnectedCallback;

        private bool _isInitialized;
        private bool _isAdvertisingActive;
        private bool _ownsGameServerInitialization;
        private bool _ownsGameServerLogin;
        private SteamAuthApiMode _apiMode;

        /// <summary>
        /// Initializes a new steam game server authentication backend.
        /// </summary>
        public SteamGameServerAuthBackend()
        {
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

                if (!TryEnsureSteamApiContext(config, serverMode, out string initFailureMessage))
                {
                    return new AuthenticationResult
                    {
                        IsSuccessful = false,
                        Message = initFailureMessage
                    };
                }

                if (!RegisterCallbacks())
                {
                    return new AuthenticationResult
                    {
                        IsSuccessful = false,
                        Message = "Failed to register Steam game server callbacks"
                    };
                }
                
                string initMessage;
                if (_apiMode == SteamAuthApiMode.GameServer)
                {
                    SteamGameServer.SetProduct(GetSteamProductString());
                    SteamGameServer.SetDedicatedServer(true);
                    SteamGameServer.SetServerName(config.ServerName);
                    SteamGameServer.SetGameDescription("Schedule I");
                    SteamGameServer.SetModDir("Schedule I");
                    SteamGameServer.SetMapName("Main");
                    SteamGameServer.SetMaxPlayerCount(config.MaxPlayers);
                    SteamGameServer.SetPasswordProtected(!string.IsNullOrEmpty(config.ServerPassword));
                    SteamGameServer.SetGameTags($"ver:{config.SteamGameServerVersion}");

                    if (SteamGameServer.BLoggedOn())
                    {
                        DebugLog.AuthenticationDebug("Steam game server auth backend detected an existing logged-on Steam server session");
                    }
                    else if (config.SteamGameServerLogOnAnonymous)
                    {
                        SteamGameServer.LogOnAnonymous();
                        _ownsGameServerLogin = true;
                        DebugLog.AuthenticationDebug("Steam game server auth backend logging on anonymously");
                    }
                    else
                    {
                        SteamGameServer.LogOn(config.SteamGameServerToken ?? string.Empty);
                        _ownsGameServerLogin = true;
                        DebugLog.AuthenticationDebug("Steam game server auth backend logging on using token");
                    }

                    TrySetAdvertiseServerActive(true);
                    initMessage = "Steam game server auth backend initialized";
                }
                else
                {
                    initMessage = "Steam game server auth backend initialized using Steam user API fallback";
                }

                _isInitialized = true;

                return new AuthenticationResult
                {
                    IsSuccessful = true,
                    Message = initMessage
                };
            }
            catch (Exception ex)
            {
                DebugLog.Error("Steam game server auth backend initialization failed", ex);
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

            if (!IsAuthApiLoggedOn())
            {
                return new AuthBeginResult
                {
                    IsPending = false,
                    ImmediateResult = new AuthenticationResult
                    {
                        IsSuccessful = false,
                        Message = BuildNotLoggedOnMessage(),
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
            EBeginAuthSessionResult beginResult = BeginAuthSession(ticketBytes, steamId);

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
                PumpCallbacks();
                GC.KeepAlive(_validateAuthCallback);
                if (_apiMode == SteamAuthApiMode.GameServer)
                {
                    GC.KeepAlive(_serversConnectedCallback);
                    GC.KeepAlive(_serverConnectFailureCallback);
                    GC.KeepAlive(_serversDisconnectedCallback);

                    if (SteamGameServer.BLoggedOn() && !_isAdvertisingActive)
                    {
                        TrySetAdvertiseServerActive(true);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Steam game server callback pump failed", ex);
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
                EndAuthSession(new CSteamID(steamIdValue));
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Failed to end auth session for {steamId}", ex);
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
                        EndAuthSession(new CSteamID(steamId));
                    }
                    catch
                    {
                    }
                }

                foreach (ulong steamId in _pendingBySteamId.Keys)
                {
                    try
                    {
                        EndAuthSession(new CSteamID(steamId));
                    }
                    catch
                    {
                    }
                }

                _activeBySteamId.Clear();
                _pendingBySteamId.Clear();
                _completionBuffer.Clear();

                TrySetAdvertiseServerActive(false);
                if (_ownsGameServerLogin)
                {
                    SteamGameServer.LogOff();
                }

                if (_ownsGameServerInitialization)
                {
                    GameServer.Shutdown();
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Steam game server auth backend shutdown encountered an error", ex);
            }
            finally
            {
                _validateAuthCallback = null;
                _serversConnectedCallback = null;
                _serverConnectFailureCallback = null;
                _serversDisconnectedCallback = null;
                _isInitialized = false;
                _ownsGameServerInitialization = false;
                _ownsGameServerLogin = false;
                _apiMode = SteamAuthApiMode.None;
            }
        }

        private bool RegisterCallbacks()
        {
            try
            {
                if (_apiMode == SteamAuthApiMode.GameServer)
                {
                    _validateAuthCallback = Callback<ValidateAuthTicketResponse_t>.CreateGameServer(CreateValidateAuthTicketResponseDelegate());
                    _serversConnectedCallback = Callback<SteamServersConnected_t>.CreateGameServer(CreateSteamServersConnectedDelegate());
                    _serverConnectFailureCallback = Callback<SteamServerConnectFailure_t>.CreateGameServer(CreateSteamServerConnectFailureDelegate());
                    _serversDisconnectedCallback = Callback<SteamServersDisconnected_t>.CreateGameServer(CreateSteamServersDisconnectedDelegate());
                }
                else if (_apiMode == SteamAuthApiMode.SteamUser)
                {
                    _validateAuthCallback = Callback<ValidateAuthTicketResponse_t>.Create(CreateValidateAuthTicketResponseDelegate());
                    _serversConnectedCallback = null;
                    _serverConnectFailureCallback = null;
                    _serversDisconnectedCallback = null;
                }
                else
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                DebugLog.Error("Steam game server callback registration failed", ex);
                _validateAuthCallback = null;
                _serversConnectedCallback = null;
                _serverConnectFailureCallback = null;
                _serversDisconnectedCallback = null;
                return false;
            }
        }

        private bool TryEnsureSteamApiContext(ServerConfig config, EServerMode serverMode, out string failureMessage)
        {
            failureMessage = string.Empty;
            _apiMode = SteamAuthApiMode.None;

            bool initialized = GameServer.Init(
                0u,
                (ushort)config.ServerPort,
                (ushort)config.SteamGameServerQueryPort,
                serverMode,
                config.SteamGameServerVersion);

            if (initialized)
            {
                _ownsGameServerInitialization = true;
                _apiMode = SteamAuthApiMode.GameServer;
                return true;
            }

            if (TryReuseExistingGameServerContext())
            {
                _apiMode = SteamAuthApiMode.GameServer;
                DebugLog.AuthenticationDebug("Steam game server auth backend reusing existing Steam game server API context");
                return true;
            }

            if (TryEnsureSteamUserContext(out ulong localSteamId))
            {
                _apiMode = SteamAuthApiMode.SteamUser;
                DebugLog.Warning(
                    $"Steam game server API is unavailable; using the active Steam user API context for auth fallback (localSteamId={localSteamId.ToString(CultureInfo.InvariantCulture)}).");
                return true;
            }

            failureMessage =
                $"Failed to initialize Steam game server API (gamePort={config.ServerPort}, queryPort={config.SteamGameServerQueryPort}, mode={serverMode}) and no logged-in Steam user API fallback was available";
            return false;
        }

        private static bool TryReuseExistingGameServerContext()
        {
            try
            {
                return SteamGameServer.GetSteamID().m_SteamID != 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryEnsureSteamUserContext(out ulong steamIdValue)
        {
            steamIdValue = 0;

            try
            {
                CSteamID steamId = SteamUser.GetSteamID();
                steamIdValue = steamId.m_SteamID;
                return steamIdValue != 0 && SteamUser.BLoggedOn();
            }
            catch
            {
                return false;
            }
        }

        private bool IsAuthApiLoggedOn()
        {
            try
            {
                switch (_apiMode)
                {
                    case SteamAuthApiMode.GameServer:
                        return SteamGameServer.BLoggedOn();
                    case SteamAuthApiMode.SteamUser:
                        return SteamUser.BLoggedOn();
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private string BuildNotLoggedOnMessage()
        {
            switch (_apiMode)
            {
                case SteamAuthApiMode.SteamUser:
                    return "Steam user API is not logged on yet; retrying is allowed";
                case SteamAuthApiMode.GameServer:
                default:
                    return "Steam game server is not logged on yet; retrying is allowed";
            }
        }

        private EBeginAuthSessionResult BeginAuthSession(byte[] ticketBytes, CSteamID steamId)
        {
            switch (_apiMode)
            {
                case SteamAuthApiMode.GameServer:
                    return SteamGameServer.BeginAuthSession(ticketBytes, ticketBytes.Length, steamId);
                case SteamAuthApiMode.SteamUser:
                    return SteamUser.BeginAuthSession(ticketBytes, ticketBytes.Length, steamId);
                default:
                    throw new InvalidOperationException("Steam auth backend does not have an active Steam API context.");
            }
        }

        private void EndAuthSession(CSteamID steamId)
        {
            switch (_apiMode)
            {
                case SteamAuthApiMode.GameServer:
                    SteamGameServer.EndAuthSession(steamId);
                    return;
                case SteamAuthApiMode.SteamUser:
                    SteamUser.EndAuthSession(steamId);
                    return;
            }
        }

        private void PumpCallbacks()
        {
            switch (_apiMode)
            {
                case SteamAuthApiMode.GameServer:
                    GameServer.RunCallbacks();
                    return;
                case SteamAuthApiMode.SteamUser:
                    SteamAPI.RunCallbacks();
                    return;
            }
        }

        private Callback<ValidateAuthTicketResponse_t>.DispatchDelegate CreateValidateAuthTicketResponseDelegate()
        {
#if IL2CPP
            return (Callback<ValidateAuthTicketResponse_t>.DispatchDelegate)new Action<ValidateAuthTicketResponse_t>(OnValidateAuthTicketResponse);
#else
            return new Callback<ValidateAuthTicketResponse_t>.DispatchDelegate(OnValidateAuthTicketResponse);
#endif
        }

        private Callback<SteamServersConnected_t>.DispatchDelegate CreateSteamServersConnectedDelegate()
        {
#if IL2CPP
            return (Callback<SteamServersConnected_t>.DispatchDelegate)new Action<SteamServersConnected_t>(OnSteamServersConnected);
#else
            return new Callback<SteamServersConnected_t>.DispatchDelegate(OnSteamServersConnected);
#endif
        }

        private Callback<SteamServerConnectFailure_t>.DispatchDelegate CreateSteamServerConnectFailureDelegate()
        {
#if IL2CPP
            return (Callback<SteamServerConnectFailure_t>.DispatchDelegate)new Action<SteamServerConnectFailure_t>(OnSteamServerConnectFailure);
#else
            return new Callback<SteamServerConnectFailure_t>.DispatchDelegate(OnSteamServerConnectFailure);
#endif
        }

        private Callback<SteamServersDisconnected_t>.DispatchDelegate CreateSteamServersDisconnectedDelegate()
        {
#if IL2CPP
            return (Callback<SteamServersDisconnected_t>.DispatchDelegate)new Action<SteamServersDisconnected_t>(OnSteamServersDisconnected);
#else
            return new Callback<SteamServersDisconnected_t>.DispatchDelegate(OnSteamServersDisconnected);
#endif
        }

        private void OnSteamServersConnected(SteamServersConnected_t _)
        {
            DebugLog.AuthenticationDebug("Steam game server connected to Steam backend");
            TrySetAdvertiseServerActive(true);
        }

        private void OnSteamServerConnectFailure(SteamServerConnectFailure_t data)
        {
            DebugLog.Error($"Steam game server connection failure: {data.m_eResult}");
        }

        private void OnSteamServersDisconnected(SteamServersDisconnected_t data)
        {
            _isAdvertisingActive = false;
            DebugLog.Error($"Steam game server disconnected from Steam backend: {data.m_eResult}");
        }

        private void TrySetAdvertiseServerActive(bool active)
        {
            try
            {
                SteamGameServer.SetAdvertiseServerActive(active);
                _isAdvertisingActive = active;
                DebugLog.AuthenticationDebug($"Steam server advertising {(active ? "enabled" : "disabled")}");
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Failed to set Steam server advertising to {active}", ex);
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
                DebugLog.Warning($"Received auth callback for unknown SteamID {steamIdValue}");
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
                    EndAuthSession(new CSteamID(steamIdValue));
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

        private enum SteamAuthApiMode
        {
            None,
            GameServer,
            SteamUser
        }
    }
}
