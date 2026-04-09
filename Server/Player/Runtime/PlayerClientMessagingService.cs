using System.Collections;
using DedicatedServerMod.Shared.ModVerification;
using DedicatedServerMod.Shared.Networking;
using DedicatedServerMod.Utils;
using MelonLoader;
using Newtonsoft.Json;
using UnityEngine;
#if IL2CPP
using Il2CppFishNet.Connection;
#else
using FishNet.Connection;
#endif

namespace DedicatedServerMod.Server.Player.Runtime
{
    /// <summary>
    /// Sends protocol messages from the server to tracked clients.
    /// </summary>
    internal sealed class PlayerClientMessagingService
    {
        internal void SendAuthChallenge(NetworkConnection connection, AuthChallengeMessage challenge)
        {
            if (connection == null || challenge == null)
            {
                return;
            }

            CustomMessaging.SendToClientOrDeferUntilReady(
                connection,
                Constants.Messages.AuthChallenge,
                JsonConvert.SerializeObject(challenge));
        }

        internal void SendAuthResult(ConnectedPlayerInfo playerInfo, AuthenticationResult result)
        {
            if (playerInfo?.Connection == null || result == null)
            {
                return;
            }

            try
            {
                AuthResultMessage payload = new AuthResultMessage
                {
                    Success = result.IsSuccessful,
                    Message = result.Message ?? string.Empty,
                    SteamId = result.ExtractedSteamId ?? string.Empty
                };

                CustomMessaging.SendToClientOrDeferUntilReady(
                    playerInfo.Connection,
                    Constants.Messages.AuthResult,
                    JsonConvert.SerializeObject(payload));
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"Failed to send auth result to ClientId {playerInfo.ClientId}: {ex.Message}");
            }
        }

        internal void SendModVerificationChallenge(ConnectedPlayerInfo playerInfo, ModVerificationChallengeMessage challenge)
        {
            if (playerInfo?.Connection == null || challenge == null)
            {
                return;
            }

            CustomMessaging.SendToClientOrDeferUntilReady(
                playerInfo.Connection,
                Constants.Messages.ModVerifyChallenge,
                JsonConvert.SerializeObject(challenge));
        }

        internal void SendModVerificationResult(ConnectedPlayerInfo playerInfo, ModVerificationEvaluationResult result)
        {
            if (playerInfo?.Connection == null || result == null)
            {
                return;
            }

            try
            {
                ModVerificationResultMessage payload = new ModVerificationResultMessage
                {
                    Success = result.Success,
                    Message = result.Message ?? string.Empty
                };

                CustomMessaging.SendToClientOrDeferUntilReady(
                    playerInfo.Connection,
                    Constants.Messages.ModVerifyResult,
                    JsonConvert.SerializeObject(payload));
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"Failed to send mod verification result to ClientId {playerInfo.ClientId}: {ex.Message}");
            }
        }

        internal void SendDisconnectNotice(ConnectedPlayerInfo player, string title, string reason)
        {
            if (player?.Connection == null)
            {
                return;
            }

            DisconnectNoticeMessage payload = new DisconnectNoticeMessage
            {
                Title = title ?? string.Empty,
                Message = reason ?? string.Empty
            };

            CustomMessaging.SendToClientOrDeferUntilReady(
                player.Connection,
                Constants.Messages.DisconnectNotice,
                JsonConvert.SerializeObject(payload));
        }

        internal void BeginDisconnectAfterDelay(ConnectedPlayerInfo player, float delaySeconds)
        {
            if (player == null)
            {
                return;
            }

            MelonCoroutines.Start(DisconnectPlayerAfterDelay(player, delaySeconds));
        }

        private static IEnumerator DisconnectPlayerAfterDelay(ConnectedPlayerInfo player, float delaySeconds)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, delaySeconds));

            if (player?.Connection == null || !player.Connection.IsActive)
            {
                yield break;
            }

            player.Connection.Disconnect(true);
        }
    }
}
