using System;
using FishNet;
using UnityEngine;

namespace DedicatedServerMod.Server.Game.Patches.Common
{
    internal enum ServerPressureLevel
    {
        Low,
        Medium,
        High
    }

    internal readonly struct ServerAdaptivePerformanceSnapshot
    {
        internal ServerAdaptivePerformanceSnapshot(
            int processorCount,
            float frameSeconds,
            float targetFrameSeconds,
            ushort tickRate,
            float tickDeltaSeconds,
            ServerPressureLevel pressureLevel,
            float npcSharedCacheCellSize,
            float npcPathCacheToleranceSqr,
            float npcSharedCacheLifetimeSeconds,
            int vehicleNodeLinkCandidateLimit,
            float vehicleNodeLinkCacheLifetimeSeconds,
            float weatherSweepIntervalSeconds,
            float weatherSpatialCacheCellSize,
            float weatherSpatialCacheLifetimeSeconds,
            float weatherEntityMovementThresholdSqr,
            float weatherVehicleRecheckSeconds,
            float weatherNpcRecheckSeconds,
            int weatherNpcBatchSize)
        {
            ProcessorCount = processorCount;
            FrameSeconds = frameSeconds;
            TargetFrameSeconds = targetFrameSeconds;
            TickRate = tickRate;
            TickDeltaSeconds = tickDeltaSeconds;
            PressureLevel = pressureLevel;
            NpcSharedCacheCellSize = npcSharedCacheCellSize;
            NpcPathCacheToleranceSqr = npcPathCacheToleranceSqr;
            NpcSharedCacheLifetimeSeconds = npcSharedCacheLifetimeSeconds;
            VehicleNodeLinkCandidateLimit = vehicleNodeLinkCandidateLimit;
            VehicleNodeLinkCacheLifetimeSeconds = vehicleNodeLinkCacheLifetimeSeconds;
            WeatherSweepIntervalSeconds = weatherSweepIntervalSeconds;
            WeatherSpatialCacheCellSize = weatherSpatialCacheCellSize;
            WeatherSpatialCacheLifetimeSeconds = weatherSpatialCacheLifetimeSeconds;
            WeatherEntityMovementThresholdSqr = weatherEntityMovementThresholdSqr;
            WeatherVehicleRecheckSeconds = weatherVehicleRecheckSeconds;
            WeatherNpcRecheckSeconds = weatherNpcRecheckSeconds;
            WeatherNpcBatchSize = weatherNpcBatchSize;
        }

        internal int ProcessorCount { get; }

        internal float FrameSeconds { get; }

        internal float TargetFrameSeconds { get; }

        internal ushort TickRate { get; }

        internal float TickDeltaSeconds { get; }

        internal ServerPressureLevel PressureLevel { get; }

        internal float NpcSharedCacheCellSize { get; }

        internal float NpcPathCacheToleranceSqr { get; }

        internal float NpcSharedCacheLifetimeSeconds { get; }

        internal int VehicleNodeLinkCandidateLimit { get; }

        internal float VehicleNodeLinkCacheLifetimeSeconds { get; }

        internal float WeatherSweepIntervalSeconds { get; }

        internal float WeatherSpatialCacheCellSize { get; }

        internal float WeatherSpatialCacheLifetimeSeconds { get; }

        internal float WeatherEntityMovementThresholdSqr { get; }

        internal float WeatherVehicleRecheckSeconds { get; }

        internal float WeatherNpcRecheckSeconds { get; }

        internal int WeatherNpcBatchSize { get; }
    }

    internal static class ServerAdaptivePerformanceTuning
    {
        private const float DefaultTargetFramesPerSecond = 60f;
        private const float MaximumSampledFrameSeconds = 1f;
        private const float RefreshIntervalSeconds = 0.5f;
        private const float SmoothedFrameBlendFactor = 0.12f;

        private static float _smoothedFrameSeconds = 1f / DefaultTargetFramesPerSecond;
        private static float _nextRefreshTime;
        private static ServerAdaptivePerformanceSnapshot _current = CreateSnapshot(
            1,
            1f / DefaultTargetFramesPerSecond,
            1f / DefaultTargetFramesPerSecond,
            0,
            0f,
            ServerPressureLevel.High);

        internal static ServerAdaptivePerformanceSnapshot GetSnapshot()
        {
            SampleFrameTime();
            RefreshIfNeeded();
            return _current;
        }

        private static void SampleFrameTime()
        {
            float deltaSeconds = Time.unscaledDeltaTime;
            if (deltaSeconds <= 0f || deltaSeconds > MaximumSampledFrameSeconds)
            {
                return;
            }

            _smoothedFrameSeconds = Mathf.Lerp(_smoothedFrameSeconds, deltaSeconds, SmoothedFrameBlendFactor);
        }

        private static void RefreshIfNeeded()
        {
            float now = Time.realtimeSinceStartup;
            if (now < _nextRefreshTime)
            {
                return;
            }

            int processorCount = Math.Max(1, Environment.ProcessorCount);
            float targetFramesPerSecond = Application.targetFrameRate > 0
                ? Application.targetFrameRate
                : DefaultTargetFramesPerSecond;
            float targetFrameSeconds = 1f / Mathf.Max(targetFramesPerSecond, 1f);

            var timeManager = InstanceFinder.TimeManager;
            ushort tickRate = timeManager != null ? timeManager.TickRate : (ushort)0;
            float tickDeltaSeconds = timeManager != null ? (float)timeManager.TickDelta : 0f;

            ServerPressureLevel pressureLevel = DeterminePressureLevel(processorCount, _smoothedFrameSeconds, targetFrameSeconds);
            _current = CreateSnapshot(
                processorCount,
                _smoothedFrameSeconds,
                targetFrameSeconds,
                tickRate,
                tickDeltaSeconds,
                pressureLevel);

            _nextRefreshTime = now + RefreshIntervalSeconds;
        }

        private static ServerPressureLevel DeterminePressureLevel(int processorCount, float frameSeconds, float targetFrameSeconds)
        {
            float framePressure = frameSeconds / Mathf.Max(targetFrameSeconds, 0.0001f);
            if (processorCount <= 1 || framePressure >= 1.15f)
            {
                return ServerPressureLevel.High;
            }

            if (processorCount <= 2 || framePressure >= 0.9f)
            {
                return ServerPressureLevel.Medium;
            }

            return ServerPressureLevel.Low;
        }

        private static ServerAdaptivePerformanceSnapshot CreateSnapshot(
            int processorCount,
            float frameSeconds,
            float targetFrameSeconds,
            ushort tickRate,
            float tickDeltaSeconds,
            ServerPressureLevel pressureLevel)
        {
            float networkCadenceFloor = tickDeltaSeconds > 0f ? tickDeltaSeconds : targetFrameSeconds;

            switch (pressureLevel)
            {
                case ServerPressureLevel.High:
                    return new ServerAdaptivePerformanceSnapshot(
                        processorCount,
                        frameSeconds,
                        targetFrameSeconds,
                        tickRate,
                        tickDeltaSeconds,
                        pressureLevel,
                        npcSharedCacheCellSize: 2f,
                        npcPathCacheToleranceSqr: 4f,
                        npcSharedCacheLifetimeSeconds: 240f,
                        vehicleNodeLinkCandidateLimit: 1,
                        vehicleNodeLinkCacheLifetimeSeconds: 60f,
                        weatherSweepIntervalSeconds: Mathf.Max(0.75f, networkCadenceFloor * 6f),
                        weatherSpatialCacheCellSize: 2f,
                        weatherSpatialCacheLifetimeSeconds: Mathf.Max(1f, networkCadenceFloor * 8f),
                        weatherEntityMovementThresholdSqr: 2.25f,
                        weatherVehicleRecheckSeconds: Mathf.Max(0.5f, networkCadenceFloor * 4f),
                        weatherNpcRecheckSeconds: Mathf.Max(1.25f, networkCadenceFloor * 10f),
                        weatherNpcBatchSize: 12);

                case ServerPressureLevel.Medium:
                    return new ServerAdaptivePerformanceSnapshot(
                        processorCount,
                        frameSeconds,
                        targetFrameSeconds,
                        tickRate,
                        tickDeltaSeconds,
                        pressureLevel,
                        npcSharedCacheCellSize: 1.5f,
                        npcPathCacheToleranceSqr: 2.25f,
                        npcSharedCacheLifetimeSeconds: 150f,
                        vehicleNodeLinkCandidateLimit: 2,
                        vehicleNodeLinkCacheLifetimeSeconds: 45f,
                        weatherSweepIntervalSeconds: Mathf.Max(0.5f, networkCadenceFloor * 4f),
                        weatherSpatialCacheCellSize: 1.5f,
                        weatherSpatialCacheLifetimeSeconds: Mathf.Max(0.5f, networkCadenceFloor * 6f),
                        weatherEntityMovementThresholdSqr: 1f,
                        weatherVehicleRecheckSeconds: Mathf.Max(0.35f, networkCadenceFloor * 3f),
                        weatherNpcRecheckSeconds: Mathf.Max(0.75f, networkCadenceFloor * 6f),
                        weatherNpcBatchSize: 24);

                default:
                    return new ServerAdaptivePerformanceSnapshot(
                        processorCount,
                        frameSeconds,
                        targetFrameSeconds,
                        tickRate,
                        tickDeltaSeconds,
                        pressureLevel,
                        npcSharedCacheCellSize: 1f,
                        npcPathCacheToleranceSqr: 1f,
                        npcSharedCacheLifetimeSeconds: 90f,
                        vehicleNodeLinkCandidateLimit: 2,
                        vehicleNodeLinkCacheLifetimeSeconds: 30f,
                        weatherSweepIntervalSeconds: Mathf.Max(0.25f, networkCadenceFloor * 2f),
                        weatherSpatialCacheCellSize: 1f,
                        weatherSpatialCacheLifetimeSeconds: Mathf.Max(0.25f, networkCadenceFloor * 4f),
                        weatherEntityMovementThresholdSqr: 0.25f,
                        weatherVehicleRecheckSeconds: Mathf.Max(0.25f, networkCadenceFloor * 2f),
                        weatherNpcRecheckSeconds: Mathf.Max(0.5f, networkCadenceFloor * 4f),
                        weatherNpcBatchSize: 64);
            }
        }
    }
}
