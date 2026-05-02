using System.Reflection;
using DedicatedServerMod.Server.Game.Patches.Console;
using DedicatedServerMod.Server.Game.Patches.Gameplay;
using DedicatedServerMod.Server.Game.Patches.Player;
using DedicatedServerMod.Shared.Configuration;
using DedicatedServerMod.Shared.ConsoleSupport;
using DedicatedServerMod.Utils;
using HarmonyLib;
using MelonLoader;
#if IL2CPP
using Il2CppFishNet.Connection;
using TimeManagerType = Il2CppScheduleOne.GameTime.TimeManager;
using PlayerType = Il2CppScheduleOne.PlayerScripts.Player;
#else
using FishNet.Connection;
using TimeManagerType = ScheduleOne.GameTime.TimeManager;
using PlayerType = ScheduleOne.PlayerScripts.Player;
#endif

namespace DedicatedServerMod.Server.Game
{
    /// <summary>
    /// Manages Harmony patches for game systems specific to dedicated server operation.
    /// </summary>
    public class GamePatchManager
    {
        private readonly List<string> appliedPatches;
        private readonly HarmonyLib.Harmony harmony;

        internal GamePatchManager()
        {
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
                DebugLog.StartupDebug($"Game patch manager initialized with {appliedPatches.Count} patches");
            }
            catch (Exception ex)
            {
                DebugLog.Error("Failed to initialize game patch manager", ex);
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
                PatchCasinoRemoteClientFlow();
                PatchHeadlessSleepCompletion();

                // 3) Console command permissions
                PatchConsoleSubmitCommand();

                // 3) Feature flags which may be toggled
                if (ServerConfig.Instance.IgnoreGhostHostForSleep)
                    appliedPatches.Add("IgnoreGhostHostForSleepEnabled");

            }
            catch (Exception ex)
            {
                DebugLog.Error("Error applying server patches", ex);
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
                    DebugLog.Warning("Could not find Player RPC for ReceivePlayerNameData; identity binding may be delayed");
                    return;
                }

                var postfix = typeof(PlayerPatches).GetMethod(nameof(PlayerPatches.BindPlayerIdentityPostfix), BindingFlags.Public | BindingFlags.Static);
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                DebugLog.StartupDebug($"Patched Player RPC for identity binding: {target.Name}");
            }
            catch (Exception ex)
            {
                DebugLog.Error("Error patching Player ReceivePlayerNameData", ex);
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
                    DebugLog.Warning("Could not find Player RPC logic for SendPlayerNameData; dedicated server friend gate patch was skipped.");
                    return;
                }

                MethodInfo prefix = typeof(PlayerPatches).GetMethod(nameof(PlayerPatches.AllowDedicatedServerPlayerNameDataPrefix), BindingFlags.Public | BindingFlags.Static);
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                DebugLog.StartupDebug($"Patched Player server name validation RPC: {target.Name}");
            }
            catch (Exception ex)
            {
                DebugLog.Error("Error patching Player SendPlayerNameData RPC", ex);
            }
        }

        /// <summary>
        /// Patches the generated sleep RPC logic so headless servers mark host sleep
        /// complete even when the runtime bypasses the wrapper-level StartSleep postfix.
        /// </summary>
        private void PatchHeadlessSleepCompletion()
        {
            try
            {
                MethodInfo target = typeof(TimeManagerType).GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(mi =>
                        mi.Name.StartsWith("RpcLogic___StartSleep_", StringComparison.Ordinal)
                        && mi.GetParameters().Length == 0);

                if (target == null)
                {
                    DebugLog.Warning("Could not find TimeManager sleep RPC logic; headless sleep completion fallback was skipped.");
                    return;
                }

                MethodInfo postfix = typeof(TimeManagerStartSleepHeadlessPatches).GetMethod(
                    nameof(TimeManagerStartSleepHeadlessPatches.ForceHeadlessHostSleepDone),
                    BindingFlags.Public | BindingFlags.Static);

                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                appliedPatches.Add("HeadlessSleepCompletionPatch");
                DebugLog.StartupDebug($"Patched TimeManager sleep RPC logic for headless completion: {target.Name}");
            }
            catch (Exception ex)
            {
                DebugLog.Error("Error patching TimeManager sleep RPC logic", ex);
            }
        }

        /// <summary>
        /// Patch ScheduleOne.Console.SubmitCommand by resolving overload explicitly to avoid ambiguous match.
        /// </summary>
        private void PatchConsoleSubmitCommand()
        {
            try
            {
                if (!GameConsoleAccess.TryGetSubmitCommandArgumentListOverload(out MethodInfo target))
                {
                    DebugLog.Warning("Could not find ScheduleOne.Console.SubmitCommand(List<string>) for patching");
                    return;
                }

                var prefix = typeof(ConsolePatches).GetMethod(nameof(ConsolePatches.SubmitCommandPrefix), BindingFlags.Public | BindingFlags.Static);
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                DebugLog.StartupDebug("Patched Console.SubmitCommand(List<string>) with permission checks");
                appliedPatches.Add("ConsoleSubmitCommandPatch");
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error patching Console.SubmitCommand", ex);
            }
        }

        /// <summary>
        /// Patches casino table state transitions so remote dedicated-server clients can
        /// start waiting casino games once all seated players are ready.
        /// </summary>
        private void PatchCasinoRemoteClientFlow()
        {
            try
            {
                Type casinoGamePlayersType = CasinoGamePatches.GetCasinoGamePlayersType();
                if (casinoGamePlayersType == null)
                {
                    DebugLog.Warning("Could not find CasinoGamePlayers type; casino dedicated-server patch was skipped.");
                    return;
                }

                MethodInfo receivePlayerBoolTarget = AccessTools.Method(casinoGamePlayersType, "ReceivePlayerBool");
                MethodInfo setPlayerListTarget = AccessTools.Method(casinoGamePlayersType, "SetPlayerList");

                MethodInfo receivePlayerBoolPostfix = typeof(CasinoGamePatches).GetMethod(
                    nameof(CasinoGamePatches.ReceivePlayerBoolPostfix),
                    BindingFlags.Public | BindingFlags.Static);
                MethodInfo setPlayerListPostfix = typeof(CasinoGamePatches).GetMethod(
                    nameof(CasinoGamePatches.SetPlayerListPostfix),
                    BindingFlags.Public | BindingFlags.Static);

                bool patchedAny = false;

                if (receivePlayerBoolTarget != null && receivePlayerBoolPostfix != null)
                {
                    harmony.Patch(receivePlayerBoolTarget, postfix: new HarmonyMethod(receivePlayerBoolPostfix));
                    patchedAny = true;
                    DebugLog.StartupDebug("Patched CasinoGamePlayers.ReceivePlayerBool for dedicated-server casino readiness.");
                }
                else
                {
                    DebugLog.Warning("Could not patch CasinoGamePlayers.ReceivePlayerBool; remote casino readiness may stay blocked.");
                }

                if (setPlayerListTarget != null && setPlayerListPostfix != null)
                {
                    harmony.Patch(setPlayerListTarget, postfix: new HarmonyMethod(setPlayerListPostfix));
                    patchedAny = true;
                    DebugLog.StartupDebug("Patched CasinoGamePlayers.SetPlayerList for dedicated-server casino roster reevaluation.");
                }
                else
                {
                    DebugLog.Warning("Could not patch CasinoGamePlayers.SetPlayerList; casino roster reevaluation may be incomplete.");
                }

                if (patchedAny)
                {
                    appliedPatches.Add("CasinoRemoteClientPatch");
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error("Error patching casino remote-client flow", ex);
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
    }

}
