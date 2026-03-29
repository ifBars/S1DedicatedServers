using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DedicatedServerMod;
using DedicatedServerMod.Shared.Configuration;
using HarmonyLib;
using MelonLoader;
#if IL2CPP
using Il2CppFishNet.Connection;
using ConsoleType = Il2CppScheduleOne.Console;
using PlayerType = Il2CppScheduleOne.PlayerScripts.Player;
#else
using FishNet.Connection;
using ConsoleType = ScheduleOne.Console;
using PlayerType = ScheduleOne.PlayerScripts.Player;
#endif

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

        internal GamePatchManager(MelonLogger.Instance loggerInstance)
        {
            logger = loggerInstance;
            appliedPatches = new List<string>();
            harmony = new HarmonyLib.Harmony("DedicatedServerMod.GamePatches");
        }

        /// <summary>
        /// Initialize and apply game patches
        /// </summary>
        internal void Initialize()
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
                // 1) Attribute-based patches are applied automatically by MelonLoader/Harmony.
                appliedPatches.Add("AttributeBasedServerPatches");

                // 2) Dynamic RPC / mangled method patches
                PatchPlayerReceivePlayerNameData();
                appliedPatches.Add("PlayerNameRPCPatch");
                PatchPlayerServerNameValidation();
                appliedPatches.Add("PlayerNameFriendGatePatch");

                // 3) Console command permissions
                PatchConsoleSubmitCommand();

                // 3) Feature flags which may be toggled
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
                MethodInfo target = typeof(PlayerType).GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(mi =>
                    {
                        if (!mi.Name.StartsWith("RpcLogic___ReceivePlayerNameData_", StringComparison.Ordinal))
                        {
                            return false;
                        }

                        ParameterInfo[] prms = mi.GetParameters();
                        return prms.Length == 3
                            && prms[0].ParameterType == typeof(NetworkConnection)
                            && prms[1].ParameterType == typeof(string)
                            && prms[2].ParameterType == typeof(string);
                    });

                if (target == null)
                {
                    target = typeof(PlayerType).GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(mi =>
                        {
                            if (!mi.Name.StartsWith("RpcLogic_", StringComparison.Ordinal))
                            {
                                return false;
                            }

                            ParameterInfo[] prms = mi.GetParameters();
                            return prms.Length == 3
                                && prms[0].ParameterType == typeof(NetworkConnection)
                                && prms[1].ParameterType == typeof(string)
                                && prms[2].ParameterType == typeof(string)
                                && string.Equals(prms[1].Name, "playerName", StringComparison.OrdinalIgnoreCase)
                                && string.Equals(prms[2].Name, "id", StringComparison.OrdinalIgnoreCase);
                        });
                }

                if (target == null)
                {
                    logger.Warning("Could not find Player RPC for ReceivePlayerNameData; identity binding may be delayed");
                    return;
                }

                var postfix = typeof(PlayerPatches).GetMethod(nameof(PlayerPatches.BindPlayerIdentityPostfix), BindingFlags.Public | BindingFlags.Static);
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                logger.Msg($"Patched Player RPC for identity binding: {target.Name}");
            }
            catch (Exception ex)
            {
                logger.Error($"Error patching Player ReceivePlayerNameData: {ex}");
            }
        }

        /// <summary>
        /// Patches the server-side player-name RPC logic to bypass the vanilla host-friend gate.
        /// Dedicated servers already use their own auth/ban pipeline, so the peer-host friend check
        /// incorrectly kicks local test clients and dedicated server players.
        /// </summary>
        private void PatchPlayerServerNameValidation()
        {
            try
            {
                MethodInfo target = typeof(PlayerType).GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(mi =>
                    {
                        if (!mi.Name.StartsWith("RpcLogic___SendPlayerNameData_", StringComparison.Ordinal))
                        {
                            return false;
                        }

                        ParameterInfo[] prms = mi.GetParameters();
                        return prms.Length == 2
                            && prms[0].ParameterType == typeof(string)
                            && prms[1].ParameterType == typeof(ulong);
                    });

                if (target == null)
                {
                    target = typeof(PlayerType).GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(mi =>
                        {
                            if (!mi.Name.StartsWith("RpcLogic_", StringComparison.Ordinal))
                            {
                                return false;
                            }

                            ParameterInfo[] prms = mi.GetParameters();
                            return prms.Length == 2
                                && prms[0].ParameterType == typeof(string)
                                && prms[1].ParameterType == typeof(ulong)
                                && string.Equals(prms[0].Name, "playerName", StringComparison.OrdinalIgnoreCase)
                                && string.Equals(prms[1].Name, "id", StringComparison.OrdinalIgnoreCase);
                        });
                }

                if (target == null)
                {
                    logger.Warning("Could not find Player RPC logic for SendPlayerNameData; dedicated server friend gate patch was skipped.");
                    return;
                }

                MethodInfo prefix = typeof(PlayerPatches).GetMethod(nameof(PlayerPatches.AllowDedicatedServerPlayerNameDataPrefix), BindingFlags.Public | BindingFlags.Static);
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                logger.Msg($"Patched Player server name validation RPC: {target.Name}");
            }
            catch (Exception ex)
            {
                logger.Error($"Error patching Player SendPlayerNameData RPC: {ex}");
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
                foreach (var mi in typeof(ConsoleType).GetMethods(BindingFlags.Public | BindingFlags.Static))
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

                var prefix = typeof(ConsolePatches).GetMethod(nameof(ConsolePatches.SubmitCommandPrefix), BindingFlags.Public | BindingFlags.Static);
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
        internal void Shutdown()
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
