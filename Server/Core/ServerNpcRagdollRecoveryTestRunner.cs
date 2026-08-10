using System;
using System.Collections;
using System.Globalization;
using System.IO;
using DedicatedServerMod.Shared.Testing;
using DedicatedServerMod.Utils;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
#if IL2CPP
using Il2CppFishNet;
using NpcManagerType = Il2CppScheduleOne.NPCs.NPCManager;
using NpcType = Il2CppScheduleOne.NPCs.NPC;
#else
using FishNet;
using NpcManagerType = ScheduleOne.NPCs.NPCManager;
using NpcType = ScheduleOne.NPCs.NPC;
#endif

namespace DedicatedServerMod.Server.Core
{
    /// <summary>
    /// Runs an opt-in server-authoritative NPC ragdoll recovery probe in the real game.
    /// </summary>
    internal sealed class ServerNpcRagdollRecoveryTestRunner
    {
        private const float KNOCKOUT_EXCLUSION_SECONDS = 10f;

        private NpcRagdollRecoveryTestOptions _options;
        private bool _failed;

        internal void Initialize()
        {
            _options = NpcRagdollRecoveryTestProtocol.ParseOptions(Environment.GetCommandLineArgs());
            if (!_options.Enabled)
            {
                return;
            }

            MelonCoroutines.Start(RunTest());
        }

        private IEnumerator RunTest()
        {
            Log($"START runtime={NpcRagdollRecoveryTestProtocol.GetRuntimeName()} timeoutSeconds={_options.TimeoutSeconds:F1}");
            if (!string.IsNullOrWhiteSpace(_options.ParseError))
            {
                Fail(_options.ParseError);
                yield break;
            }

            yield return WaitForServerWorld();
            if (_failed)
            {
                yield break;
            }

            string clientReadyPath = NpcRagdollRecoveryTestProtocol.GetPath(
                _options,
                NpcRagdollRecoveryTestProtocol.CLIENT_READY_FILE);
            yield return WaitForFile(clientReadyPath, "client-ready");
            if (_failed)
            {
                yield break;
            }

            NpcType npc = FindRecoverableNpc();
            if (npc == null)
            {
                Fail("no conscious visible NPC with movement and avatar components was available");
                yield break;
            }

            string targetId = npc.ID;
            NpcRagdollRecoveryTestProtocol.WriteFile(
                _options,
                NpcRagdollRecoveryTestProtocol.TARGET_FILE,
                targetId);
            Log($"TARGET id={targetId} name={npc.FullName}");

            string clientTargetReadyPath = NpcRagdollRecoveryTestProtocol.GetPath(
                _options,
                NpcRagdollRecoveryTestProtocol.CLIENT_TARGET_READY_FILE);
            yield return WaitForFile(clientTargetReadyPath, "client-target-ready");
            if (_failed)
            {
                yield break;
            }

            Vector3 forcePoint = npc.Avatar.MiddleSpineRB.position;
            Vector3 forceDirection = (Vector3.forward + Vector3.up * 0.15f).normalized;
            npc.Movement.ActivateRagdoll_Server(forcePoint, forceDirection, 150f);
            Log("ACTIVATED forceMagnitude=150");

            float activationTime = Time.realtimeSinceStartup;
            bool observedRagdoll = false;
            float nextStateLogTime = activationTime;
            while (Time.realtimeSinceStartup - activationTime <= _options.TimeoutSeconds)
            {
                if (npc == null || npc.Avatar == null || npc.Movement == null)
                {
                    Fail("target NPC became unavailable during recovery");
                    yield break;
                }

                bool isRagdolled = npc.Avatar.Ragdolled;
                observedRagdoll |= isRagdolled;
                if (observedRagdoll && !isRagdolled)
                {
                    float elapsedSeconds = Time.realtimeSinceStartup - activationTime;
                    string clientResultPath = NpcRagdollRecoveryTestProtocol.GetPath(
                        _options,
                        NpcRagdollRecoveryTestProtocol.CLIENT_RESULT_FILE);
                    yield return WaitForFile(clientResultPath, "client-recovery-result");
                    if (_failed)
                    {
                        yield break;
                    }

                    yield return VerifyKnockoutExclusion(npc);
                    if (_failed)
                    {
                        yield break;
                    }

                    Pass(targetId, elapsedSeconds);
                    yield break;
                }

                if (Time.realtimeSinceStartup >= nextStateLogTime)
                {
                    float spineVelocity = npc.Avatar.MiddleSpineRB != null
                        ? npc.Avatar.MiddleSpineRB.velocity.magnitude
                        : -1f;
                    Log(
                        $"STATE elapsed={Time.realtimeSinceStartup - activationTime:F1} " +
                        $"ragdolled={isRagdolled} conscious={npc.IsConscious} " +
                        $"knockedOut={npc.Health.IsKnockedOut} spineVelocity={spineVelocity:F3} " +
                        $"fixedDeltaTime={Time.fixedDeltaTime:F3}");
                    nextStateLogTime = Time.realtimeSinceStartup + 1f;
                }

                yield return null;
            }

            Fail(observedRagdoll
                ? $"target remained ragdolled for {_options.TimeoutSeconds:F1}s"
                : "server never observed the target enter ragdoll state");
        }

        private IEnumerator VerifyKnockoutExclusion(NpcType npc)
        {
            npc.Health.KnockOut();
            float knockoutStartedAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - knockoutStartedAt <= _options.TimeoutSeconds)
            {
                if (npc == null || npc.Avatar == null || npc.Health == null)
                {
                    Fail("target NPC became unavailable during knockout exclusion check");
                    yield break;
                }

                if (npc.Health.IsKnockedOut && npc.Avatar.Ragdolled)
                {
                    break;
                }

                yield return null;
            }

            if (!npc.Health.IsKnockedOut || !npc.Avatar.Ragdolled)
            {
                Fail("target did not enter the expected knocked-out ragdoll state");
                yield break;
            }

            Log($"KNOCKOUT_EXCLUSION_STARTED durationSeconds={KNOCKOUT_EXCLUSION_SECONDS:F1}");
            float exclusionStartedAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - exclusionStartedAt < KNOCKOUT_EXCLUSION_SECONDS)
            {
                if (npc == null || npc.Avatar == null || npc.Health == null)
                {
                    Fail("target NPC became unavailable during knockout exclusion check");
                    yield break;
                }

                if (!npc.Health.IsKnockedOut || !npc.Avatar.Ragdolled)
                {
                    Fail("fallback recovery incorrectly revived or stood up a knocked-out NPC");
                    yield break;
                }

                yield return null;
            }

            npc.Health.Revive();
            Log("KNOCKOUT_EXCLUSION_PASS");
        }

        private IEnumerator WaitForServerWorld()
        {
            float startedAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startedAt <= _options.TimeoutSeconds)
            {
                if (InstanceFinder.IsServer &&
                    string.Equals(SceneManager.GetActiveScene().name, "Main", StringComparison.Ordinal) &&
                    NpcManagerType.NPCRegistry != null &&
                    NpcManagerType.NPCRegistry.Count > 0)
                {
                    Log($"WORLD_READY npcCount={NpcManagerType.NPCRegistry.Count}");
                    yield break;
                }

                yield return null;
            }

            Fail(
                $"server world did not become ready; scene={SceneManager.GetActiveScene().name} " +
                $"isServer={InstanceFinder.IsServer} npcCount={NpcManagerType.NPCRegistry?.Count ?? 0}");
        }

        private IEnumerator WaitForFile(string path, string phase)
        {
            float startedAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startedAt <= _options.TimeoutSeconds)
            {
                if (File.Exists(path))
                {
                    Log($"{phase.ToUpperInvariant()}_READY");
                    yield break;
                }

                yield return null;
            }

            Fail($"timed out waiting for {phase} marker");
        }

        private static NpcType FindRecoverableNpc()
        {
            if (NpcManagerType.NPCRegistry == null)
            {
                return null;
            }

            for (int i = 0; i < NpcManagerType.NPCRegistry.Count; i++)
            {
                NpcType npc = NpcManagerType.NPCRegistry[i];
                if (npc != null &&
                    npc.isVisible &&
                    npc.IsConscious &&
                    npc.Health != null &&
                    npc.NPCData != null &&
                    npc.NPCData.Health != null &&
                    !npc.NPCData.Health.Invincible &&
                    !npc.Health.IsDead &&
                    !npc.Health.IsKnockedOut &&
                    npc.Movement != null &&
                    npc.Avatar != null &&
                    npc.Avatar.MiddleSpineRB != null &&
                    !npc.Avatar.Ragdolled)
                {
                    return npc;
                }
            }

            return null;
        }

        private void Pass(string targetId, float elapsedSeconds)
        {
            string result =
                $"PASS|runtime={NpcRagdollRecoveryTestProtocol.GetRuntimeName()}|role=server|" +
                $"target={targetId}|elapsedSeconds={elapsedSeconds.ToString("F3", CultureInfo.InvariantCulture)}";
            NpcRagdollRecoveryTestProtocol.WriteFile(
                _options,
                NpcRagdollRecoveryTestProtocol.SERVER_RESULT_FILE,
                result);
            Log(result);
        }

        private void Fail(string message)
        {
            if (_failed)
            {
                return;
            }

            _failed = true;
            string result =
                $"FAIL|runtime={NpcRagdollRecoveryTestProtocol.GetRuntimeName()}|role=server|message={message}";
            try
            {
                if (_options != null && !string.IsNullOrWhiteSpace(_options.RunDirectory))
                {
                    NpcRagdollRecoveryTestProtocol.WriteFile(
                        _options,
                        NpcRagdollRecoveryTestProtocol.SERVER_RESULT_FILE,
                        result);
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error($"{NpcRagdollRecoveryTestProtocol.LOG_TAG} failed to write server result: {ex.Message}");
            }

            DebugLog.Error($"{NpcRagdollRecoveryTestProtocol.LOG_TAG} {result}");
        }

        private static void Log(string message)
        {
            DebugLog.Info($"{NpcRagdollRecoveryTestProtocol.LOG_TAG} {message}");
        }
    }
}
