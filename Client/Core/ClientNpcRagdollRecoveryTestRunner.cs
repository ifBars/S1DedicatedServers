using System;
using System.Collections;
using System.Globalization;
using System.IO;
using DedicatedServerMod.Client.Managers;
using DedicatedServerMod.Shared.Testing;
using DedicatedServerMod.Utils;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
#if IL2CPP
using NpcManagerType = Il2CppScheduleOne.NPCs.NPCManager;
using NpcType = Il2CppScheduleOne.NPCs.NPC;
#else
using NpcManagerType = ScheduleOne.NPCs.NPCManager;
using NpcType = ScheduleOne.NPCs.NPC;
#endif

namespace DedicatedServerMod.Client.Core
{
    /// <summary>
    /// Observes the client side of the opt-in real-game NPC ragdoll recovery probe.
    /// </summary>
    internal sealed class ClientNpcRagdollRecoveryTestRunner
    {
        private readonly ClientConnectionManager _connectionManager;
        private NpcRagdollRecoveryTestOptions _options;
        private bool _failed;

        internal ClientNpcRagdollRecoveryTestRunner(ClientConnectionManager connectionManager)
        {
            _connectionManager = connectionManager;
        }

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

            yield return WaitForClientWorld();
            if (_failed)
            {
                yield break;
            }

            NpcRagdollRecoveryTestProtocol.WriteFile(
                _options,
                NpcRagdollRecoveryTestProtocol.CLIENT_READY_FILE,
                "ready");
            Log($"WORLD_READY npcCount={NpcManagerType.NPCRegistry.Count}");

            string targetPath = NpcRagdollRecoveryTestProtocol.GetPath(
                _options,
                NpcRagdollRecoveryTestProtocol.TARGET_FILE);
            yield return WaitForFile(targetPath, "target");
            if (_failed)
            {
                yield break;
            }

            string targetId = File.ReadAllText(targetPath).Trim();
            NpcType npc = null;
            float targetWaitStartedAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - targetWaitStartedAt <= _options.TimeoutSeconds)
            {
                npc = FindNpc(targetId);
                if (npc != null && npc.Avatar != null)
                {
                    break;
                }

                yield return null;
            }

            if (npc == null || npc.Avatar == null)
            {
                Fail($"target NPC '{targetId}' was not available on the client");
                yield break;
            }

            NpcRagdollRecoveryTestProtocol.WriteFile(
                _options,
                NpcRagdollRecoveryTestProtocol.CLIENT_TARGET_READY_FILE,
                targetId);
            Log($"TARGET_READY id={targetId} name={npc.FullName}");

            float observationStartedAt = Time.realtimeSinceStartup;
            bool observedRagdoll = false;
            while (Time.realtimeSinceStartup - observationStartedAt <= _options.TimeoutSeconds)
            {
                if (npc == null || npc.Avatar == null)
                {
                    Fail("target NPC became unavailable during client observation");
                    yield break;
                }

                bool isRagdolled = npc.Avatar.Ragdolled;
                if (isRagdolled && !observedRagdoll)
                {
                    observedRagdoll = true;
                    Log("OBSERVED_RAGDOLL");
                }
                else if (observedRagdoll && !isRagdolled)
                {
                    float elapsedSeconds = Time.realtimeSinceStartup - observationStartedAt;
                    Pass(targetId, elapsedSeconds);
                    yield break;
                }

                yield return null;
            }

            Fail(observedRagdoll
                ? $"client still observed target ragdolled after {_options.TimeoutSeconds:F1}s"
                : "client never observed the target enter ragdoll state");
        }

        private IEnumerator WaitForClientWorld()
        {
            float startedAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startedAt <= _options.TimeoutSeconds)
            {
                if (_connectionManager.IsConnectedToDedicatedServer &&
                    string.Equals(SceneManager.GetActiveScene().name, "Main", StringComparison.Ordinal) &&
                    NpcManagerType.NPCRegistry != null &&
                    NpcManagerType.NPCRegistry.Count > 0)
                {
                    yield break;
                }

                yield return null;
            }

            Fail(
                $"client world did not become ready; scene={SceneManager.GetActiveScene().name} " +
                $"connected={_connectionManager.IsConnectedToDedicatedServer} " +
                $"npcCount={NpcManagerType.NPCRegistry?.Count ?? 0}");
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

            Fail($"timed out waiting for {phase} file");
        }

        private static NpcType FindNpc(string targetId)
        {
            if (NpcManagerType.NPCRegistry == null)
            {
                return null;
            }

            for (int i = 0; i < NpcManagerType.NPCRegistry.Count; i++)
            {
                NpcType npc = NpcManagerType.NPCRegistry[i];
                if (npc != null && string.Equals(npc.ID, targetId, StringComparison.Ordinal))
                {
                    return npc;
                }
            }

            return null;
        }

        private void Pass(string targetId, float elapsedSeconds)
        {
            string result =
                $"PASS|runtime={NpcRagdollRecoveryTestProtocol.GetRuntimeName()}|role=client|" +
                $"target={targetId}|elapsedSeconds={elapsedSeconds.ToString("F3", CultureInfo.InvariantCulture)}";
            NpcRagdollRecoveryTestProtocol.WriteFile(
                _options,
                NpcRagdollRecoveryTestProtocol.CLIENT_RESULT_FILE,
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
                $"FAIL|runtime={NpcRagdollRecoveryTestProtocol.GetRuntimeName()}|role=client|message={message}";
            try
            {
                if (_options != null && !string.IsNullOrWhiteSpace(_options.RunDirectory))
                {
                    NpcRagdollRecoveryTestProtocol.WriteFile(
                        _options,
                        NpcRagdollRecoveryTestProtocol.CLIENT_RESULT_FILE,
                        result);
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error($"{NpcRagdollRecoveryTestProtocol.LOG_TAG} failed to write client result: {ex.Message}");
            }

            DebugLog.Error($"{NpcRagdollRecoveryTestProtocol.LOG_TAG} {result}");
        }

        private static void Log(string message)
        {
            DebugLog.Info($"{NpcRagdollRecoveryTestProtocol.LOG_TAG} {message}");
        }
    }
}
