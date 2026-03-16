using System;
using System.Globalization;
using System.Reflection;
using DedicatedServerMod.Server.Core;
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

namespace DedicatedServerMod.Server.Game
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

                var targetConn = conn ?? __instance.Owner;
                if (targetConn == null)
                {
                    return;
                }

                var playerManager = ServerBootstrap.Players;
                playerManager?.SetPlayerIdentity(targetConn, id, playerName);
                DebugLog.PlayerLifecycleDebug($"BindPlayerIdentityPostfix: ClientId {targetConn.ClientId} -> SteamID {id} ({playerName})");
            }
            catch (Exception ex)
            {
                DedicatedServerPatchCommon.Logger.Error($"Error binding player identity: {ex}");
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
                    DedicatedServerPatchCommon.Logger.Warning("ReceivePlayerNameData method not found; falling back to vanilla friend gate.");
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
                DedicatedServerPatchCommon.Logger.Warning($"AllowDedicatedServerPlayerNameDataPrefix error: {ex.Message}");
                return true;
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
                if (!InstanceFinder.IsServer || !DedicatedServerMod.Shared.Configuration.ServerConfig.Instance.AutoSaveOnPlayerLeave)
                {
                    return;
                }

                if (GhostHostIdentifier.IsGhostHost(__instance))
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
                DedicatedServerPatchCommon.Logger.Warning($"Player.OnDestroy save trigger error: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(PlayerType), nameof(PlayerType.AreAllPlayersReadyToSleep))]
    internal static class PlayerSleepPatches
    {
        private static bool Prefix(ref bool __result)
        {
            if (!InstanceFinder.IsServer || !DedicatedServerMod.Shared.Configuration.ServerConfig.Instance.IgnoreGhostHostForSleep)
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
                    if (player == null || GhostHostIdentifier.IsGhostHost(player))
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
                DedicatedServerPatchCommon.Logger.Warning($"AreAllPlayersReadyToSleep prefix error: {ex.Message}");
                return true;
            }
        }
    }
}
