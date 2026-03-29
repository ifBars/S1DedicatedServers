using System;
using DedicatedServerMod.API;
using DedicatedServerMod.Client.Core;
using DedicatedServerMod.Shared.ModVerification;
using DedicatedServerMod.Shared.Networking;
using Newtonsoft.Json;
using MelonLoader;

namespace DedicatedServerMod.Client.Managers
{
    /// <summary>
    /// Handles the client-side mod verification handshake with the dedicated server.
    /// </summary>
    internal sealed class ClientModVerificationManager
    {
        private readonly MelonLogger.Instance _logger;

        private bool _isVerified;

        /// <summary>
        /// Initializes a new client mod verification manager.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        internal ClientModVerificationManager(MelonLogger.Instance logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Gets whether the current server session passed mod verification.
        /// </summary>
        internal bool IsVerified => _isVerified;

        /// <summary>
        /// Initializes verification message subscriptions.
        /// </summary>
        internal void Initialize()
        {
            CustomMessaging.ClientMessageReceived += OnClientMessageReceived;
        }

        /// <summary>
        /// Resets per-session verification state after disconnect.
        /// </summary>
        internal void OnDisconnected()
        {
            _isVerified = false;
        }

        /// <summary>
        /// Shuts down verification resources.
        /// </summary>
        internal void Shutdown()
        {
            CustomMessaging.ClientMessageReceived -= OnClientMessageReceived;
            OnDisconnected();
        }

        private void OnClientMessageReceived(string command, string data)
        {
            if (string.Equals(command, Utils.Constants.Messages.ModVerifyChallenge, StringComparison.Ordinal))
            {
                HandleVerificationChallenge(data);
                return;
            }

            if (string.Equals(command, Utils.Constants.Messages.ModVerifyResult, StringComparison.Ordinal))
            {
                HandleVerificationResult(data);
            }
        }

        private void HandleVerificationChallenge(string data)
        {
            ModVerificationChallengeMessage challenge;
            try
            {
                challenge = JsonConvert.DeserializeObject<ModVerificationChallengeMessage>(data ?? string.Empty);
            }
            catch (JsonException ex)
            {
                _logger.Warning($"Failed to parse mod verification challenge: {ex.Message}");
                ClientBootstrap.Instance?.ConnectionManager?.OnModVerificationFailed("Failed to parse the server's mod verification challenge.");
                return;
            }

            if (challenge == null)
            {
                ClientBootstrap.Instance?.ConnectionManager?.OnModVerificationFailed("The server sent an empty mod verification challenge.");
                return;
            }

            try
            {
                ModVerificationReportMessage report = new ModVerificationReportMessage
                {
                    Nonce = challenge.Nonce ?? string.Empty,
                    Mods = ModManager.GetLoadedClientModsForVerification(typeof(ClientBootstrap).Assembly)
                };

                string payload = JsonConvert.SerializeObject(report);
                if (!CustomMessaging.TrySendToServer(Utils.Constants.Messages.ModVerifyReport, payload))
                {
                    ClientBootstrap.Instance?.ConnectionManager?.OnModVerificationFailed("Failed to send the client mod verification report to the server.");
                    return;
                }

                _logger.Msg($"Client mod verification report submitted ({report.Mods.Count} mods, policy={challenge.PolicyHash})");
            }
            catch (Exception ex)
            {
                _logger.Warning($"Failed to build mod verification report: {ex.Message}");
                ClientBootstrap.Instance?.ConnectionManager?.OnModVerificationFailed("Failed to build the client mod verification report.");
            }
        }

        private void HandleVerificationResult(string data)
        {
            ModVerificationResultMessage result;
            try
            {
                result = JsonConvert.DeserializeObject<ModVerificationResultMessage>(data ?? string.Empty);
            }
            catch (JsonException ex)
            {
                _logger.Warning($"Failed to parse mod verification result: {ex.Message}");
                ClientBootstrap.Instance?.ConnectionManager?.OnModVerificationFailed("Failed to parse the server's mod verification result.");
                return;
            }

            if (result == null)
            {
                ClientBootstrap.Instance?.ConnectionManager?.OnModVerificationFailed("The server sent an empty mod verification result.");
                return;
            }

            _isVerified = result.Success;

            if (result.Success)
            {
                _logger.Msg($"Client mod verification succeeded: {result.Message}");
                ClientBootstrap.Instance?.ConnectionManager?.OnModVerificationSucceeded(result.Message);
            }
            else
            {
                _logger.Warning($"Client mod verification failed: {result.Message}");
                ClientBootstrap.Instance?.ConnectionManager?.OnModVerificationFailed(result.Message);
            }
        }
    }
}
