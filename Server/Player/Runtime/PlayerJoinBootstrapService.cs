#if IL2CPP
using Il2CppFishNet;
using Il2CppFishNet.Connection;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Quests;
using LoadManagerType = Il2CppScheduleOne.Persistence.LoadManager;
using QuestManagerType = Il2CppScheduleOne.Quests.QuestManager;
using TimeManagerType = Il2CppScheduleOne.GameTime.TimeManager;
#else
using FishNet.Connection;
using ScheduleOne.DevUtilities;
using ScheduleOne.Quests;
using LoadManagerType = ScheduleOne.Persistence.LoadManager;
using QuestManagerType = ScheduleOne.Quests.QuestManager;
using TimeManagerType = ScheduleOne.GameTime.TimeManager;
#endif
using System.Collections;
using System.Reflection;
using DedicatedServerMod.Shared.Configuration;
using DedicatedServerMod.Shared.Networking;
using DedicatedServerMod.Utils;
using HarmonyLib;
using MelonLoader;
using Newtonsoft.Json;
using UnityEngine;

namespace DedicatedServerMod.Server.Player.Runtime
{
    /// <summary>
    /// Handles post-join client bootstrap messages and native time replay.
    /// </summary>
    internal sealed class PlayerJoinBootstrapService
    {
        private const int InitialTimeReplayAttempts = 4;
        private const float InitialTimeReplayFirstDelaySeconds = 0.35f;
        private const float InitialTimeReplayIntervalSeconds = 0.75f;
        private const float QuestReplayPollIntervalSeconds = 0.25f;
        private const float QuestReplayTimeoutSeconds = 15f;

        private static readonly MethodInfo SetTimeDataClientMethod = AccessTools.Method(
            typeof(TimeManagerType),
            "SetTimeData_Client",
            new[] { typeof(NetworkConnection), typeof(int), typeof(int), typeof(uint) });

        private readonly PlayerSessionRegistry _registry;

        internal PlayerJoinBootstrapService(PlayerSessionRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        internal void StartInitialJoinBootstrap(ConnectedPlayerInfo playerInfo)
        {
            if (playerInfo?.Connection == null)
            {
                return;
            }

            SendInitialServerDataToClient(playerInfo.Connection);
            MelonCoroutines.Start(ReplayInitialTimeDataToClient(playerInfo));
            MelonCoroutines.Start(ReplayQuestStateToClient(playerInfo));
        }

        internal void SendInitialServerDataToClient(NetworkConnection connection)
        {
            if (connection == null)
            {
                return;
            }

            try
            {
                ServerConfig config = ServerConfig.Instance;
                Shared.ServerData serverData = new Shared.ServerData
                {
                    ServerName = config.ServerName,
                    ServerDescription = config.ServerDescription,
                    CurrentPlayers = _registry.GetVisiblePlayerCount(),
                    MaxPlayers = config.MaxPlayers,
                    AllowSleeping = config.AllowSleeping
                };

                CustomMessaging.SendToClient(connection, Constants.Messages.ServerData, JsonConvert.SerializeObject(serverData));
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"Failed to send server data to ClientId {connection.ClientId}: {ex.Message}");
            }
        }

        private IEnumerator ReplayInitialTimeDataToClient(ConnectedPlayerInfo playerInfo)
        {
            if (playerInfo == null || playerInfo.IsLoopbackConnection)
            {
                yield break;
            }

            if (SetTimeDataClientMethod == null)
            {
                DebugLog.Warning("Unable to replay authoritative time because TimeManager.SetTimeData_Client could not be resolved.");
                yield break;
            }

            yield return new WaitForSecondsRealtime(InitialTimeReplayFirstDelaySeconds);

            for (int attempt = 1; attempt <= InitialTimeReplayAttempts; attempt++)
            {
                if (!_registry.IsTrackedPlayerActive(playerInfo))
                {
                    yield break;
                }

                LoadManagerType loadManager = Singleton<LoadManagerType>.Instance;
                TimeManagerType timeManager = NetworkSingleton<TimeManagerType>.Instance;
                if (loadManager != null && timeManager != null && !loadManager.IsLoading && loadManager.IsGameLoaded)
                {
                    try
                    {
                        SetTimeDataClientMethod.Invoke(timeManager, new object[]
                        {
                            playerInfo.Connection,
                            timeManager.ElapsedDays,
                            timeManager.CurrentTime,
                            0u
                        });

                        DebugLog.PlayerLifecycleDebug(
                            $"Replayed authoritative time to ClientId {playerInfo.ClientId} " +
                            $"attempt {attempt}/{InitialTimeReplayAttempts}: day={timeManager.ElapsedDays}, time={timeManager.CurrentTime:D4}");
                    }
                    catch (Exception ex)
                    {
                        DebugLog.Warning($"Failed to replay authoritative time to ClientId {playerInfo.ClientId}: {ex.Message}");
                        yield break;
                    }
                }

                if (attempt < InitialTimeReplayAttempts)
                {
                    yield return new WaitForSecondsRealtime(InitialTimeReplayIntervalSeconds);
                }
            }
        }

        private IEnumerator ReplayQuestStateToClient(ConnectedPlayerInfo playerInfo)
        {
            if (playerInfo == null || playerInfo.IsLoopbackConnection)
            {
                yield break;
            }

            float startedAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startedAt < QuestReplayTimeoutSeconds)
            {
                if (!_registry.IsTrackedPlayerActive(playerInfo))
                {
                    yield break;
                }

                LoadManagerType loadManager = Singleton<LoadManagerType>.Instance;
                QuestManagerType questManager = NetworkSingleton<QuestManagerType>.Instance;
                bool playerDataReady = playerInfo.PlayerInstance != null && playerInfo.PlayerInstance.playerDataRetrieveReturned;
                bool worldReady = loadManager != null && !loadManager.IsLoading && loadManager.IsGameLoaded;

                if (playerDataReady && worldReady && questManager?.DefaultQuests != null)
                {
                    try
                    {
                        int questStateCount = 0;
                        int entryStateCount = 0;
                        int trackedQuestCount = 0;

                        foreach (Quest quest in questManager.DefaultQuests)
                        {
                            if (quest == null)
                            {
                                continue;
                            }

                            for (int entryIndex = 0; entryIndex < quest.Entries.Count; entryIndex++)
                            {
                                QuestEntry entry = quest.Entries[entryIndex];
                                if (entry != null && entry.State != EQuestState.Inactive)
                                {
                                    questManager.ReceiveQuestEntryState(
                                        playerInfo.Connection,
                                        quest.GUID.ToString(),
                                        entryIndex,
                                        entry.State);
                                    entryStateCount++;
                                }
                            }

                            if (quest.State != EQuestState.Inactive)
                            {
                                questManager.ReceiveQuestState(
                                    playerInfo.Connection,
                                    quest.GUID.ToString(),
                                    quest.State);
                                questStateCount++;
                            }

                            if (quest.IsTracked)
                            {
                                questManager.SetQuestTracked(
                                    playerInfo.Connection,
                                    quest.GUID.ToString(),
                                    tracked: true);
                                trackedQuestCount++;
                            }
                        }

                        DebugLog.Info(
                            $"Replayed authoritative quest state to ClientId {playerInfo.ClientId}: " +
                            $"quests={questStateCount}, entries={entryStateCount}, tracked={trackedQuestCount}");
                    }
                    catch (Exception ex)
                    {
                        DebugLog.Warning($"Failed to replay quest state to ClientId {playerInfo.ClientId}: {ex.Message}");
                    }

                    yield break;
                }

                yield return new WaitForSecondsRealtime(QuestReplayPollIntervalSeconds);
            }

            LoadManagerType timedOutLoadManager = Singleton<LoadManagerType>.Instance;
            bool timedOutPlayerDataReady = playerInfo.PlayerInstance != null && playerInfo.PlayerInstance.playerDataRetrieveReturned;
            bool timedOutWorldReady = timedOutLoadManager != null && !timedOutLoadManager.IsLoading && timedOutLoadManager.IsGameLoaded;
            DebugLog.Warning(
                $"Timed out replaying quest state to ClientId {playerInfo.ClientId}: " +
                $"playerDataReady={timedOutPlayerDataReady}, worldReady={timedOutWorldReady}, " +
                $"questManagerReady={NetworkSingleton<QuestManagerType>.Instance?.DefaultQuests != null}");
        }
    }
}
