using System;
using System.Collections.Generic;
using System.Reflection;
using DedicatedServerMod;
using DedicatedServerMod.Shared.Configuration;
using HarmonyLib;
using MelonLoader;
using ScheduleOne.PlayerScripts;
using FishNet.Connection;

namespace DedicatedServerMod.Server.Game
{
    /// <summary>
    /// Manages Harmony patches for game systems specific to dedicated server operation.
    /// </summary>
    public class GamePatchManager
    {
        private readonly MelonLogger.Instance logger;

        private readonly List<string> appliedPatches;
        private readonly HarmonyLib.Harmony harmony;

        public GamePatchManager(MelonLogger.Instance loggerInstance)
        {
            logger = loggerInstance;
            appliedPatches = new List<string>();
            harmony = new HarmonyLib.Harmony("DedicatedServerMod.GamePatches");
        }

        /// <summary>
        /// Initialize and apply game patches
        /// </summary>
        public void Initialize()
        {
            try
            {
                ApplyServerPatches();
                logger.Msg($"Game patch manager initialized with {appliedPatches.Count} patches");
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to initialize game patch manager: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Apply Harmony patches needed for dedicated server operation
        /// </summary>
        private void ApplyServerPatches()
        {
            try
            {
                // 1) Core always-on patches
                harmony.PatchAll(typeof(DedicatedServerPatches));
                appliedPatches.Add("CoreHarmonyPatches");

                // 2) Dynamic RPC / mangled method patches
                PatchPlayerReceivePlayerNameData();
                appliedPatches.Add("PlayerNameRPCPatch");

                // 3) Console command permissions
                PatchConsoleSubmitCommand();

                // 3) Feature flags which may be toggled
                if (ServerConfig.Instance.TimeNeverStops)
                    appliedPatches.Add("TimeNeverStopsEnabled");
                if (ServerConfig.Instance.IgnoreGhostHostForSleep)
                    appliedPatches.Add("IgnoreGhostHostForSleepEnabled");

            }
            catch (Exception ex)
            {
                logger.Error($"Error applying server patches: {ex}");
            }
        }

        /// <summary>
        /// Find and patch Player RPC method that delivers player name and id (SteamId) to the server.
        /// The method name can be mangled, so we search by signature.
        /// </summary>
        private void PatchPlayerReceivePlayerNameData()
        {
            try
            {
                MethodInfo target = null;
                foreach (var mi in typeof(ScheduleOne.PlayerScripts.Player).GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!mi.Name.StartsWith("RpcLogic_")) continue;
                    var prms = mi.GetParameters();
                    if (prms.Length == 3 && prms[0].ParameterType == typeof(NetworkConnection)
                        && prms[1].ParameterType == typeof(string) && prms[2].ParameterType == typeof(string))
                    {
                        target = mi;
                        break;
                    }
                }

                if (target == null)
                {
                    logger.Warning("Could not find Player RPC for ReceivePlayerNameData; identity binding may be delayed");
                    return;
                }

                var postfix = typeof(DedicatedServerPatches).GetMethod(nameof(DedicatedServerPatches.BindPlayerIdentityPostfix), BindingFlags.Public | BindingFlags.Static);
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                logger.Msg($"Patched Player RPC for identity binding: {target.Name}");
            }
            catch (Exception ex)
            {
                logger.Error($"Error patching Player ReceivePlayerNameData: {ex}");
            }
        }

        /// <summary>
        /// Patch ScheduleOne.Console.SubmitCommand by resolving overload explicitly to avoid ambiguous match.
        /// </summary>
        private void PatchConsoleSubmitCommand()
        {
            try
            {
                MethodInfo target = null;
                foreach (var mi in typeof(ScheduleOne.Console).GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (mi.Name != "SubmitCommand") continue;
                    var prms = mi.GetParameters();
                    if (prms.Length == 1 && prms[0].ParameterType == typeof(List<string>))
                    {
                        target = mi;
                        break;
                    }
                }

                if (target == null)
                {
                    logger.Warning("Could not find ScheduleOne.Console.SubmitCommand(List<string>) for patching");
                    return;
                }

                var prefix = typeof(DedicatedServerPatches).GetMethod(nameof(DedicatedServerPatches.ConsoleSubmitCommand_Prefix), BindingFlags.Public | BindingFlags.Static);
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                logger.Msg("Patched Console.SubmitCommand(List<string>) with permission checks");
                appliedPatches.Add("ConsoleSubmitCommandPatch");
            }
            catch (Exception ex)
            {
                logger.Error($"Error patching Console.SubmitCommand: {ex}");
            }
        }

        /// <summary>
        /// Get the number of applied patches
        /// </summary>
        public int GetAppliedPatchCount()
        {
            return appliedPatches.Count;
        }

        /// <summary>
        /// Get list of applied patch names
        /// </summary>
        public List<string> GetAppliedPatches()
        {
            return new List<string>(appliedPatches);
        }

        /// <summary>
        /// Get patch information
        /// </summary>
        public PatchInfo GetPatchInfo()
        {
            return new PatchInfo
            {
                TotalPatches = appliedPatches.Count,
                AppliedPatches = GetAppliedPatches(),
                HarmonyId = harmony.Id
            };
        }

        /// <summary>
        /// Shutdown and unpatch if necessary
        /// </summary>
        public void Shutdown()
        {
            try
            {
                // Note: Generally we don't unpatch during shutdown as it can cause issues
                // But we could do cleanup here if needed
                
                logger.Msg($"Game patch manager shutdown ({appliedPatches.Count} patches remain active)");
            }
            catch (Exception ex)
            {
                logger.Error($"Error during game patch manager shutdown: {ex}");
            }
        }
    }

    /// <summary>
    /// Patch information
    /// </summary>
    public class PatchInfo
    {
        public int TotalPatches { get; set; }
        public List<string> AppliedPatches { get; set; }
        public string HarmonyId { get; set; }

        public override string ToString()
        {
            return $"Patches: {TotalPatches} applied | Harmony ID: {HarmonyId}";
        }
    }

    // Example patch classes (these would be implemented with actual game methods)
    
    /// <summary>
    /// Example time system patch
    /// </summary>
    public static class TimePatch
    {
        public static bool Prefix()
        {
            // Patch logic here
            return true; // Allow original method to run
        }
    }

    /// <summary>
    /// Example sleep system patch
    /// </summary>
    public static class SleepPatch
    {
        public static bool Prefix()
        {
            // Patch logic here
            return true; // Allow original method to run
        }
    }
}
