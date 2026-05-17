using System;
using System.Globalization;
using DedicatedServerMod.Utils;
#if IL2CPP
using Il2CppSteamworks;
#else
using Steamworks;
#endif

namespace DedicatedServerMod.Client.Core
{
    /// <summary>
    /// Coordinates client-side Steamworks.NET readiness for Steam-authenticated dedicated server features.
    /// </summary>
    internal static class ClientSteamRuntime
    {
        private static readonly TimeSpan InitializationRetryDelay = TimeSpan.FromSeconds(1);

        private static DateTime _nextInitializationAttemptUtc = DateTime.MinValue;
        private static bool _loggedInitializationSuccess;
        private static bool _loggedWaitingForInitialization;
        private static bool _loggedUnsupportedLaunch;

        /// <summary>
        /// Ensures the Steam user API is initialized and logged on.
        /// </summary>
        internal static bool EnsureUserReady(bool attemptInitialization, out ulong steamId, out string status)
        {
            steamId = 0;
            status = string.Empty;

            bool steamRunning = TryIsSteamRunning(out string runningError);
            if (!steamRunning)
            {
                status = string.IsNullOrWhiteSpace(runningError)
                    ? "Steam is not running."
                    : $"Steam running check failed: {runningError}";
                return false;
            }

            if (TryGetLoggedOnSteamUser(out steamId, out string userError))
            {
                LogReadyOnce();
                status = $"Steam user API ready for SteamID {steamId.ToString(CultureInfo.InvariantCulture)}.";
                return true;
            }

            if (attemptInitialization)
            {
                TryInitializeSteamApi();

                if (TryGetLoggedOnSteamUser(out steamId, out userError))
                {
                    LogReadyOnce();
                    status = $"Steam user API ready for SteamID {steamId.ToString(CultureInfo.InvariantCulture)}.";
                    return true;
                }
            }

            status = string.IsNullOrWhiteSpace(userError)
                ? "Steam is running, but the Steam user API is not logged on yet."
                : $"Steam is running, but the Steam user API is not ready yet: {userError}";
            return false;
        }

        /// <summary>
        /// Logs the current Steam runtime state in terms users can act on.
        /// </summary>
        internal static void LogSupportState()
        {
            if (EnsureUserReady(attemptInitialization: true, out _, out string status))
            {
                DebugLog.Info($"DedicatedServerClient Steam runtime check passed: {status}");
                return;
            }

            if (TryIsSteamRunning(out _))
            {
                DebugLog.Warning(
                    "DedicatedServerClient Steam runtime is not ready yet. " +
                    $"{status} Authentication will retry before submitting a Steam ticket. " +
                    "If this persists, launch the official Steam copy through Steam and keep Steam online.");
                return;
            }

            if (!_loggedUnsupportedLaunch)
            {
                _loggedUnsupportedLaunch = true;
                DebugLog.Error(
                    "Unsupported DedicatedServerClient Steam runtime state. " +
                    $"{status} Dedicated server authentication requires launching a legitimate Steam copy through Steam with Steamworks available. " +
                    "Offline, cracked, copied, or non-Steam launch flows are not supported and will fail Steam ticket authentication.");
            }
        }

        /// <summary>
        /// Pumps Steam callbacks when the Steam user API is ready.
        /// </summary>
        internal static void Tick()
        {
            if (!EnsureUserReady(attemptInitialization: true, out _, out _))
            {
                return;
            }

            try
            {
                SteamAPI.RunCallbacks();
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"Steam client callback pump failed: {ex.Message}");
            }
        }

        private static bool TryIsSteamRunning(out string error)
        {
            error = string.Empty;

            try
            {
                return SteamAPI.IsSteamRunning();
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryGetLoggedOnSteamUser(out ulong steamId, out string error)
        {
            steamId = 0;
            error = string.Empty;

            try
            {
                steamId = SteamUser.GetSteamID().m_SteamID;
                return steamId != 0 && SteamUser.BLoggedOn();
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static void TryInitializeSteamApi()
        {
            DateTime now = DateTime.UtcNow;
            if (now < _nextInitializationAttemptUtc)
            {
                return;
            }

            _nextInitializationAttemptUtc = now + InitializationRetryDelay;

            try
            {
                if (SteamAPI.Init())
                {
                    if (!_loggedInitializationSuccess)
                    {
                        _loggedInitializationSuccess = true;
                        DebugLog.Info("DedicatedServerClient initialized Steamworks.NET client API context.");
                    }

                    return;
                }

                LogWaitingForInitialization("SteamAPI.Init returned false.");
            }
            catch (Exception ex)
            {
                LogWaitingForInitialization(ex.Message);
            }
        }

        private static void LogWaitingForInitialization(string reason)
        {
            if (_loggedWaitingForInitialization)
            {
                return;
            }

            _loggedWaitingForInitialization = true;
            DebugLog.Warning($"DedicatedServerClient is waiting for Steamworks.NET client API initialization: {reason}");
        }

        private static void LogReadyOnce()
        {
            if (_loggedInitializationSuccess)
            {
                return;
            }

            _loggedInitializationSuccess = true;
            DebugLog.Info("DedicatedServerClient detected a ready Steamworks.NET client API context.");
        }
    }
}
