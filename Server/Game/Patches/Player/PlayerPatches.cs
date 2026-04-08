using System.Globalization;
using System.Reflection;
using DedicatedServerMod.Server.Core;
using DedicatedServerMod.Server.Game.Patches.Common;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Utils;
using HarmonyLib;
#if IL2CPP
using Il2CppFishNet;
using Il2CppFishNet.Connection;
using PlayerType = Il2CppScheduleOne.PlayerScripts.Player;
#else
using FishNet;
using FishNet.Connection;
using PlayerType = ScheduleOne.PlayerScripts.Player;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Player
{
    internal static class PlayerPatches
    {
        private static readonly MethodInfo ReceivePlayerNameDataMethod = AccessTools.Method(
            typeof(PlayerType),
            "ReceivePlayerNameData",
            new[] { typeof(NetworkConnection), typeof(string), typeof(string) });

        public static void BindPlayerIdentityPostfix(PlayerType __instance, NetworkConnection conn, string playerName, string id)
        {
            try
            {
                if (!InstanceFinder.IsServer)
                {
                    return;
                }

                var sourceConn = __instance?.Owner;
                if (sourceConn == null)
                {
                    return;
                }

                var playerManager = ServerBootstrap.Players;
                playerManager?.SetPlayerIdentity(sourceConn, id, playerName);
                DebugLog.PlayerLifecycleDebug($"BindPlayerIdentityPostfix: SourceClientId {sourceConn.ClientId}, TargetClientId {(conn != null ? conn.ClientId.ToString() : "null")} -> SteamID {id} ({playerName})");
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error binding player identity: {ex}");
            }
        }

        public static bool AllowDedicatedServerPlayerNameDataPrefix(PlayerType __instance, string playerName, ulong id)
        {
            try
            {
                if (!InstanceFinder.IsServer)
                {
                    return true;
                }

                if (ReceivePlayerNameDataMethod == null)
                {
                    DebugLog.Warning("ReceivePlayerNameData method not found; falling back to vanilla friend gate.");
                    return true;
                }

                string steamId = id.ToString(CultureInfo.InvariantCulture);
                ReceivePlayerNameDataMethod.Invoke(__instance, new object[] { null, playerName, steamId });
                __instance.PlayerName = playerName;
                __instance.PlayerCode = steamId;
                return false;
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"AllowDedicatedServerPlayerNameDataPrefix error: {ex.Message}");
                return true;
            }
        }

        [HarmonyPatch(typeof(PlayerType), "get_SaveFolderName")]
        [HarmonyPrefix]
        private static bool GetSaveFolderNamePrefix(PlayerType __instance, ref string __result)
        {
            try
            {
                if (!InstanceFinder.IsServer || __instance == null || !__instance.IsGhostHost())
                {
                    return true;
                }

                __result = "Player_0";
                return false;
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"Player.SaveFolderName patch error: {ex.Message}");
                return true;
            }
        }

        [HarmonyPatch(typeof(PlayerType), "PlayerLoaded")]
        [HarmonyPrefix]
        private static void PlayerLoadedPrefix(PlayerType __instance)
        {
            try
            {
                if (!InstanceFinder.IsServer || __instance == null)
                {
                    return;
                }

                if (!__instance.IsGhostHost() || __instance.HasCompletedIntro)
                {
                    return;
                }

                __instance.HasCompletedIntro = true;
                DebugLog.Info("Marked dedicated server loopback host intro as completed before PlayerLoaded.");
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"PlayerLoadedPrefix error: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(PlayerType), "OnDestroy")]
    internal static class PlayerOnDestroyPatches
    {
        private static void Prefix(PlayerType __instance)
        {
            try
            {
                if (!InstanceFinder.IsServer || !Shared.Configuration.ServerConfig.Instance.AutoSaveOnPlayerLeave)
                {
                    return;
                }

                if (__instance.IsGhostHost())
                {
                    return;
                }

                ConnectedPlayerInfo playerInfo = __instance?.Owner != null
                    ? ServerBootstrap.Players?.GetPlayer(__instance.Owner)
                    : null;

                if (playerInfo == null || !playerInfo.HasCompletedJoinFlow)
                {
                    return;
                }

                ServerBootstrap.Persistence?.TriggerAutoSave($"player_disconnect_{__instance?.PlayerName}");
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"Player.OnDestroy save trigger error: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(PlayerType), nameof(PlayerType.AreAllPlayersReadyToSleep))]
    internal static class PlayerSleepPatches
    {
        private static bool Prefix(ref bool __result)
        {
            if (!InstanceFinder.IsServer || !Shared.Configuration.ServerConfig.Instance.IgnoreGhostHostForSleep)
            {
                return true;
            }

            try
            {
                var list = PlayerType.PlayerList;
                if (list == null || list.Count == 0)
                {
                    __result = false;
                    return false;
                }

                int eligiblePlayers = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    var player = list[i];
                    if (player == null || player.IsGhostHost())
                    {
                        continue;
                    }

                    eligiblePlayers++;
                    if (!player.IsReadyToSleep)
                    {
                        __result = false;
                        return false;
                    }
                }

                if (eligiblePlayers == 0)
                {
                    __result = false;
                    return false;
                }

                __result = true;
                return false;
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"AreAllPlayersReadyToSleep prefix error: {ex.Message}");
                return true;
            }
        }
    }
}
