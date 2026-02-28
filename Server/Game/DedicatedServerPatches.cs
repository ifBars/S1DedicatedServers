using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
#if IL2CPP
using Il2CppFishNet;
using Il2CppFishNet.Connection;
using Il2CppFishNet.Transporting;
using Il2CppFishNet.Transporting.Multipass;
using Il2CppFishNet.Transporting.Tugboat;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.UI;
#else
using FishNet;
using FishNet.Connection;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using ScheduleOne.DevUtilities;
using ScheduleOne.Persistence;
using ScheduleOne.Product;
using ScheduleOne.UI;
#endif
using UnityEngine;
#if IL2CPP
using Il2CppCorgiGodRays;
#else
using CorgiGodRays;
#endif
#if IL2CPP
using Il2CppScheduleOne.Heatmap;
#else
using ScheduleOne.Heatmap;
#endif
using DedicatedServerMod.Utils;
using System.Reflection.Emit;

namespace DedicatedServerMod.Server.Game
{
    /// <summary>
    /// Centralized Harmony patches migrated from the legacy DedicatedServerHost implementation.
    /// These patches delegate to the new modular systems in ServerBootstrap and managers.
    /// </summary>
    internal static class DedicatedServerPatches
    {
        private static readonly MelonLogger.Instance Logger = new MelonLogger.Instance("DedicatedServerPatches");

        // ------- Multipass.Initialize: ensure Tugboat exists and is added to transports -------
        [HarmonyPatch(typeof(Multipass), "Initialize")]
        private static class MultipassInitializePrefix
        {
            private static void Prefix(Multipass __instance)
            {
                try
                {
                    var tugboat = __instance.gameObject.GetComponent<Tugboat>();
                    if (tugboat == null)
                    {
                        tugboat = __instance.gameObject.AddComponent<Tugboat>();
                        Logger.Msg("Added Tugboat component to Multipass for server");

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
                    Logger.Error($"Error in Multipass.Initialize patch: {ex}");
                }
            }
        }

        // ------- LoadManager: keep original flow but ensure transport ready before load -------
        [HarmonyPatch(typeof(LoadManager), "StartGame")]
        private static class LoadManagerStartGamePrefix
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
                    Logger.Warning($"LoadManager.StartGame transport prep failed: {ex.Message}");
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
                Logger.Error($"Error binding player identity: {ex}");
            }
        }

        // ------- Player disconnect -> trigger save if configured -------
        [HarmonyPatch(typeof(ScheduleOne.PlayerScripts.Player), "OnDestroy")]
        private static class PlayerOnDestroyPrefix
        {
            private static void Prefix(ScheduleOne.PlayerScripts.Player __instance)
            {
                try
                {
                    if (!InstanceFinder.IsServer || !DedicatedServerMod.Shared.Configuration.ServerConfig.Instance.AutoSaveOnPlayerLeave) return;
                    if (GhostHostIdentifier.IsGhostHost(__instance)) return;
                    Server.Core.ServerBootstrap.Persistence?.TriggerAutoSave($"player_disconnect_{__instance?.PlayerName}");
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Player.OnDestroy save trigger error: {ex.Message}");
                }
            }
        }

        // ------- Ensure server never treats itself as tutorial when saving -------
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.IsTutorial), MethodType.Getter)]
        private static class GameManagerIsTutorialGetterPrefix
        {
            private static bool Prefix(ref bool __result)
            {
                __result = false;
                return false;
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
        private static class DailySummaryAwakePostfix
        {
            private static void Postfix(DailySummary __instance)
            {
                try
                {
                    DedicatedServerMod.Shared.Networking.CustomMessaging.DailySummaryAwakePostfix(__instance);
                }
                catch (Exception ex)
                {
                    Logger.Warning($"DailySummary.Awake postfix error: {ex.Message}");
                }
            }
        }

        // ------- Sleep system: ignore loopback ghost host when checking readiness -------
        // Experimental
        [HarmonyPatch(typeof(ScheduleOne.PlayerScripts.Player), nameof(ScheduleOne.PlayerScripts.Player.AreAllPlayersReadyToSleep))]
        private static class PlayerAreAllPlayersReadyToSleepPrefix
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
                        if (GhostHostIdentifier.IsGhostHost(p))
                            continue;
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
                    Logger.Warning($"AreAllPlayersReadyToSleep prefix error: {ex.Message}");
                    return true;
                }
            }
        }

        // ------- ProductIconManager: Prevent icon generation crashing headless server -------
        [HarmonyPatch(typeof(ProductIconManager), "GenerateIcons")]
        private static class ProductIconManagerGenerateIconsPrefix
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

        // ------- IconGenerator: Prevent icon generation compute on server -------
        [HarmonyPatch(typeof(IconGenerator), "GeneratePackagingIcon")]
        private static class IconGeneratorGeneratePackagingIconPrefix
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
        private static class GodRaysRenderPassInitializePrefix
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
        private static class HeatmapManagerStartPrefix
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

        // ------- TimeManager: Fix time progression in batchmode by replacing WaitForEndOfFrame -------
        // Experimental
        [HarmonyPatch(typeof(ScheduleOne.GameTime.TimeManager), "TimeLoop")]
        [HarmonyPatch(typeof(ScheduleOne.GameTime.TimeManager), "TickLoop")]
        private static class TimeLoopWaitPatch
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = instructions.ToList();
                var waitForEndOfFrameCtor = typeof(WaitForEndOfFrame).GetConstructor(Type.EmptyTypes);
                var waitForFixedUpdateCtor = typeof(WaitForFixedUpdate).GetConstructor(Type.EmptyTypes);
                
                bool patched = false;
                for (int i = 0; i < codes.Count; i++)
                {
                    // Replace new WaitForEndOfFrame() with new WaitForFixedUpdate() in batchmode
                    if (codes[i].opcode == OpCodes.Newobj && 
                        codes[i].operand is System.Reflection.ConstructorInfo ctor &&
                        ctor == waitForEndOfFrameCtor)
                    {
                        codes[i].operand = waitForFixedUpdateCtor;
                        patched = true;
                    }
                }
                
                if (patched)
                {
                    Logger.Msg("Patched TimeManager coroutines to use WaitForFixedUpdate for batchmode compatibility");
                }
                
                return codes;
            }
        }

        // ------- DailySummary: Disable UI canvas on dedicated server (runs after RPC registration) -------
        [HarmonyPatch(typeof(DailySummary), "Awake")]
        private static class DailySummaryCanvasDisablePostfix
        {
            private static void Postfix(DailySummary __instance)
            {
                if (InstanceFinder.IsServer && __instance.Canvas != null)
                {
                    __instance.Canvas.enabled = false;
                    Logger.Msg("Disabled DailySummary canvas on dedicated server (RPCs still registered)");
                }
            }
        }

        // ------- DailySummary: Prevent opening on server -------
        // Experimental
        [HarmonyPatch(typeof(DailySummary), "Open")]
        private static class DailySummaryOpenPrefix
        {
            private static bool Prefix(DailySummary __instance)
            {
                if (InstanceFinder.IsServer || Application.isBatchMode)
                {
                    // Skip opening the UI on server/batchmode
                    return false;
                }
                return true;
            }
        }
    }
}
