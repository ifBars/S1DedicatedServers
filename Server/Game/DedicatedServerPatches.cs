using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using FishNet;
using FishNet.Component.Scenes;
using FishNet.Connection;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using ScheduleOne.DevUtilities;
using ScheduleOne.GameTime;
using ScheduleOne.Persistence;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Product;
using ScheduleOne.UI;
using UnityEngine;
using CorgiGodRays;
using ScheduleOne.Heatmap;

namespace DedicatedServerMod.Server.Game
{
    /// <summary>
    /// Centralized Harmony patches migrated from the legacy DedicatedServerHost implementation.
    /// These patches delegate to the new modular systems in ServerBootstrap and managers.
    /// </summary>
    internal static class DedicatedServerPatches
    {
        private static readonly MelonLogger.Instance logger = new MelonLogger.Instance("DedicatedServerPatches");

        // ------- Multipass.Initialize: ensure Tugboat exists and is added to transports -------
        [HarmonyPatch(typeof(Multipass), "Initialize")]
        private static class Multipass_Initialize_Prefix
        {
            private static void Prefix(Multipass __instance)
            {
                try
                {
                    var tugboat = __instance.gameObject.GetComponent<Tugboat>();
                    if (tugboat == null)
                    {
                        tugboat = __instance.gameObject.AddComponent<Tugboat>();
                        logger.Msg("Added Tugboat component to Multipass for server");

                        // Add to internal transports list if present
                        var transportsField = typeof(Multipass).GetField("_transports", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (transportsField?.GetValue(__instance) is List<Transport> transports)
                        {
                            if (!transports.Contains(tugboat))
                                transports.Add(tugboat);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error($"Error in Multipass.Initialize patch: {ex}");
                }
            }
        }

        // ------- LoadManager: keep original flow but ensure transport ready before load -------
        [HarmonyPatch(typeof(LoadManager), "StartGame")]
        private static class LoadManager_StartGame_Prefix
        {
            private static void Prefix(LoadManager __instance)
            {
                try
                {
                    var networkManager = InstanceFinder.NetworkManager;
                    var mp = networkManager?.TransportManager?.Transport as Multipass;
                    if (mp == null) return;

                    var tug = mp.gameObject.GetComponent<Tugboat>();
                    if (tug == null) 
                    {
                        var clientField = typeof(Multipass).GetField("_clientTransport", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (clientField != null)
                            clientField.SetValue(mp, tug);
                        tug = mp.gameObject.AddComponent<Tugboat>();
                    }
                        
                    tug.SetPort((ushort)DedicatedServerMod.Shared.Configuration.ServerConfig.Instance.ServerPort);
                }
                catch (Exception ex)
                {
                    logger.Warning($"LoadManager.StartGame transport prep failed: {ex.Message}");
                }
            }
        }

        // ------- Player identity binding when name data arrives -------
        // The ReceivePlayerNameData method has a mangled name; dynamic patching is handled in GamePatchManager.
        public static void BindPlayerIdentityPostfix(ScheduleOne.PlayerScripts.Player __instance, NetworkConnection conn, string playerName, string id)
        {
            try
            {
                if (!InstanceFinder.IsServer) return;
                var targetConn = conn ?? __instance.Owner;
                if (targetConn == null) return;
                DedicatedServerMod.Server.Player.PlayerManager pm = Server.Core.ServerBootstrap.Players;
                pm?.SetPlayerIdentity(targetConn, id, playerName);
            }
            catch (Exception ex)
            {
                logger.Error($"Error binding player identity: {ex}");
            }
        }

        // ------- Player initialization postfix: ensure per-client setup if needed -------
        // NOTE: MarkPlayerInitialized method does not exist in the game code
        // This patch is disabled - was likely removed in a game update

        // ------- Player disconnect -> trigger save if configured -------
        [HarmonyPatch(typeof(ScheduleOne.PlayerScripts.Player), "OnDestroy")]
        private static class Player_OnDestroy_Prefix
        {
            private static void Prefix(ScheduleOne.PlayerScripts.Player __instance)
            {
                try
                {
                    if (!InstanceFinder.IsServer || !DedicatedServerMod.Shared.Configuration.ServerConfig.Instance.AutoSaveOnPlayerLeave) return;
                    if (__instance?.gameObject?.name == "[DedicatedServerHostLoopback]") return;
                    Server.Core.ServerBootstrap.Persistence?.TriggerAutoSave($"player_disconnect_{__instance?.PlayerName}");
                }
                catch (Exception ex)
                {
                    logger.Warning($"Player.OnDestroy save trigger error: {ex.Message}");
                }
            }
        }

        // ------- TimeManager patches: prevent 4AM freeze and sync time -------
        // ------- TimeManager patches: prevent 4AM freeze and sync time -------
        // REMOVED: TimeManager.Tick patch caused HarmonyException (method not found/signature mismatch).
        // Logic should be handled in Update or via other means if 4AM freeze prevention is strictly required.

        // ------- Ensure server never treats itself as tutorial when saving -------
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.IsTutorial), MethodType.Getter)]
        private static class GameManager_IsTutorial_Getter_Prefix
        {
            private static bool Prefix(ref bool __result)
            {
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(TimeManager), "Update")]
        private static class TimeManager_Update_Prefix
        {
            private static bool Prefix(TimeManager __instance)
            {
                if (!InstanceFinder.IsServer) return true;
                // Avoid try+yield within this method; keep simple and safe
                if (__instance.IsSleepInProgress)
                {
                    var sleepEndTimeField = typeof(TimeManager).GetField("sleepEndTime", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (sleepEndTimeField != null)
                    {
                        var sleepEndTime = (int)sleepEndTimeField.GetValue(__instance);
                        if (__instance.IsCurrentTimeWithinRange(sleepEndTime, TimeManager.AddMinutesTo24HourTime(sleepEndTime, 60)))
                        {
                            var endSleep = typeof(TimeManager).GetMethod("EndSleep", BindingFlags.NonPublic | BindingFlags.Instance);
                            endSleep?.Invoke(__instance, null);
                        }
                    }
                }
                else if (ScheduleOne.PlayerScripts.Player.AreAllPlayersReadyToSleep())
                {
                    var startSleep = typeof(TimeManager).GetMethod("StartSleep", BindingFlags.NonPublic | BindingFlags.Instance);
                    startSleep?.Invoke(__instance, null);
                }
                return true;
            }
        }

        // ------- Console permissions (patched dynamically to avoid overload ambiguity) -------
        public static bool ConsoleSubmitCommand_Prefix(List<string> args)
        {
            if (args == null || args.Count == 0) return true;
            if (!(InstanceFinder.IsServer && !InstanceFinder.IsHost)) return true;

            var local = ScheduleOne.PlayerScripts.Player.Local;
            if (local == null) return true; // server console
            string cmd = args[0]?.ToLower() ?? string.Empty;
            if (!DedicatedServerMod.Shared.Permissions.PermissionManager.CanUseConsole(local)) return false;
            if (!DedicatedServerMod.Shared.Permissions.PermissionManager.CanUseCommand(local, cmd)) return false;
            return true;
        }

        // ------- DailySummary RPC registration hook -------
        [HarmonyPatch(typeof(DailySummary), "Awake")]
        private static class DailySummary_Awake_Postfix
        {
            private static void Postfix(DailySummary __instance)
            {
                try
                {
                    DedicatedServerMod.Shared.Networking.CustomMessaging.DailySummaryAwakePostfix(__instance);
                }
                catch (Exception ex)
                {
                    logger.Warning($"DailySummary.Awake postfix error: {ex.Message}");
                }
            }
        }

        // ------- Sleep system: ignore loopback ghost host when checking readiness -------
        [HarmonyPatch(typeof(ScheduleOne.PlayerScripts.Player), nameof(ScheduleOne.PlayerScripts.Player.AreAllPlayersReadyToSleep))]
        private static class Player_AreAllPlayersReadyToSleep_Prefix
        {
            private static bool Prefix(ref bool __result)
            {
                if (!InstanceFinder.IsServer || !DedicatedServerMod.Shared.Configuration.ServerConfig.Instance.IgnoreGhostHostForSleep)
                    return true; // run original

                try
                {
                    var list = ScheduleOne.PlayerScripts.Player.PlayerList;
                    if (list == null || list.Count == 0)
                    {
                        __result = false;
                        return false; // skip original
                    }

                    for (int i = 0; i < list.Count; i++)
                    {
                        var p = list[i];
                        if (p == null) continue;
                        if (p.gameObject?.name == "[DedicatedServerHostLoopback]")
                            continue; // ignore ghost host
                        if (!p.IsReadyToSleep)
                        {
                            __result = false;
                            return false;
                        }
                    }

                    __result = true;
                    return false; // handled
                }
                catch (Exception ex)
                {
                    logger.Warning($"AreAllPlayersReadyToSleep prefix error: {ex.Message}");
                    return true;
                }
            }
        }

        // ------- ProductIconManager: Prevent icon generation crashing headless server -------
        [HarmonyPatch(typeof(ProductIconManager), "GenerateIcons")]
        private static class ProductIconManager_GenerateIcons_Prefix
        {
            private static bool Prefix()
            {
                if (InstanceFinder.IsServer || Application.isBatchMode)
                {
                    return false; // Skip original method
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(IconGenerator), "GeneratePackagingIcon")]
        private static class IconGenerator_GeneratePackagingIcon_Prefix
        {
            private static bool Prefix(ref Texture2D __result)
            {
                if (InstanceFinder.IsServer || Application.isBatchMode)
                {
                    __result = Texture2D.whiteTexture; 
                    return false; // Skip original method
                }
                return true;
            }
        }

        // ------- CorgiGodRays: Prevent compute buffer creation on server -------
        [HarmonyPatch(typeof(GodRaysRenderPass), "Initialize")]
        private static class GodRaysRenderPass_Initialize_Prefix
        {
            private static bool Prefix()
            {
                if (InstanceFinder.IsServer || Application.isBatchMode)
                {
                    return false;
                }
                return true;
            }
        }

        // ------- HeatmapManager: Prevent compute shader usage on server -------
        [HarmonyPatch(typeof(HeatmapManager), "Start")]
        private static class HeatmapManager_Start_Prefix
        {
            private static bool Prefix()
            {
                 if (InstanceFinder.IsServer || Application.isBatchMode)
                {
                    return false;
                }
                return true;
            }
        }
    }
}
