using System;
using System.Collections.Generic;
using DedicatedServerMod.Server.Game.Patches.Common;
using HarmonyLib;
using UnityEngine;
#if IL2CPP
using EnvironmentManagerType = Il2CppScheduleOne.Weather.EnvironmentManager;
using LandVehicleType = Il2CppScheduleOne.Vehicles.LandVehicle;
using NpcType = Il2CppScheduleOne.NPCs.NPC;
using SkateboardType = Il2CppScheduleOne.Skating.Skateboard;
using WeatherEntityType = Il2CppScheduleOne.Weather.IWeatherEntity;
using WeatherProfileType = Il2CppScheduleOne.Weather.WeatherProfile;
#else
using EnvironmentManagerType = ScheduleOne.Weather.EnvironmentManager;
using LandVehicleType = ScheduleOne.Vehicles.LandVehicle;
using NpcType = ScheduleOne.NPCs.NPC;
using SkateboardType = ScheduleOne.Skating.Skateboard;
using WeatherEntityType = ScheduleOne.Weather.IWeatherEntity;
using WeatherProfileType = ScheduleOne.Weather.WeatherProfile;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Weather
{
    internal readonly struct WeatherSpatialKey : IEquatable<WeatherSpatialKey>
    {
        private readonly int x;
        private readonly int y;
        private readonly int z;

        private WeatherSpatialKey(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        internal static WeatherSpatialKey From(Vector3 position, float cellSize)
        {
            float safeCellSize = Mathf.Max(cellSize, 0.1f);
            return new WeatherSpatialKey(
                Mathf.RoundToInt(position.x / safeCellSize),
                Mathf.RoundToInt(position.y / safeCellSize),
                Mathf.RoundToInt(position.z / safeCellSize));
        }

        public bool Equals(WeatherSpatialKey other)
        {
            return x == other.x && y == other.y && z == other.z;
        }

        public override bool Equals(object obj)
        {
            return obj is WeatherSpatialKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = x;
                hashCode = (hashCode * 397) ^ y;
                hashCode = (hashCode * 397) ^ z;
                return hashCode;
            }
        }
    }

    internal sealed class WeatherSampleState
    {
        internal WeatherSampleState(WeatherProfileType profile, bool isUnderCover, float createdAt)
        {
            Profile = profile;
            IsUnderCover = isUnderCover;
            CreatedAt = createdAt;
        }

        internal WeatherProfileType Profile { get; }

        internal bool IsUnderCover { get; }

        internal float CreatedAt { get; }
    }

    internal sealed class WeatherEntityState
    {
        internal Vector3 LastPosition { get; set; }

        internal float NextEligibleUpdateTime { get; set; }
    }

    internal sealed class WeatherManagerState
    {
        internal readonly Dictionary<WeatherEntityType, WeatherEntityState> EntityStates = new();
        internal readonly Dictionary<WeatherSpatialKey, WeatherSampleState> SpatialSamples = new();
        internal readonly Queue<WeatherSpatialKey> SpatialSampleOrder = new();

        internal float NextSweepTime { get; set; }

        internal int NpcCursor { get; set; }
    }

    internal static class AdaptiveWeatherEntityUpdater
    {
        private static readonly Dictionary<EnvironmentManagerType, WeatherManagerState> ManagerStates = new();
        private const int MaximumSpatialSamples = 512;

        internal static void Update(EnvironmentManagerType manager)
        {
            if (manager == null)
            {
                return;
            }

            ServerAdaptivePerformanceSnapshot tuning = ServerAdaptivePerformanceTuning.GetSnapshot();
            WeatherManagerState state = GetState(manager);
            float now = Time.realtimeSinceStartup;
            if (now < state.NextSweepTime)
            {
                return;
            }

            state.NextSweepTime = now + tuning.WeatherSweepIntervalSeconds;

            var entities = manager._registeredWeatherEntities;
            if (entities == null || entities.Count == 0)
            {
                return;
            }

            CleanupEntityStates(state, entities);
            PruneSpatialSamples(state, tuning, now);

            ProcessDynamicEntities(manager, state, entities, tuning, now);
            ProcessNpcBatch(manager, state, entities, tuning, now);
        }

        private static WeatherManagerState GetState(EnvironmentManagerType manager)
        {
            if (!ManagerStates.TryGetValue(manager, out WeatherManagerState state))
            {
                state = new WeatherManagerState();
                ManagerStates[manager] = state;
            }

            return state;
        }

        private static void CleanupEntityStates(WeatherManagerState state, IList<WeatherEntityType> entities)
        {
            if (state.EntityStates.Count == 0)
            {
                return;
            }

            HashSet<WeatherEntityType> currentEntities = new HashSet<WeatherEntityType>();
            for (int i = 0; i < entities.Count; i++)
            {
                WeatherEntityType entity = entities[i];
                if (entity != null)
                {
                    currentEntities.Add(entity);
                }
            }

            List<WeatherEntityType> staleKeys = new List<WeatherEntityType>();
            foreach (KeyValuePair<WeatherEntityType, WeatherEntityState> pair in state.EntityStates)
            {
                if (!currentEntities.Contains(pair.Key))
                {
                    staleKeys.Add(pair.Key);
                }
            }

            for (int i = 0; i < staleKeys.Count; i++)
            {
                state.EntityStates.Remove(staleKeys[i]);
            }
        }

        private static void ProcessDynamicEntities(
            EnvironmentManagerType manager,
            WeatherManagerState state,
            IList<WeatherEntityType> entities,
            ServerAdaptivePerformanceSnapshot tuning,
            float now)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                WeatherEntityType entity = entities[i];
                if (!IsDynamicWeatherEntity(entity))
                {
                    continue;
                }

                UpdateEntity(manager, state, entity, tuning, now, tuning.WeatherVehicleRecheckSeconds);
            }
        }

        private static void ProcessNpcBatch(
            EnvironmentManagerType manager,
            WeatherManagerState state,
            IList<WeatherEntityType> entities,
            ServerAdaptivePerformanceSnapshot tuning,
            float now)
        {
            if (entities.Count == 0)
            {
                return;
            }

            int processed = 0;
            int visited = 0;
            int startIndex = entities.Count > 0 ? state.NpcCursor % entities.Count : 0;

            while (visited < entities.Count && processed < tuning.WeatherNpcBatchSize)
            {
                int index = (startIndex + visited) % entities.Count;
                WeatherEntityType entity = entities[index];
                visited++;

                if (!IsNpcWeatherEntity(entity))
                {
                    continue;
                }

                UpdateEntity(manager, state, entity, tuning, now, tuning.WeatherNpcRecheckSeconds);
                processed++;
            }

            state.NpcCursor = (startIndex + Math.Max(visited, 1)) % entities.Count;
        }

        private static void UpdateEntity(
            EnvironmentManagerType manager,
            WeatherManagerState state,
            WeatherEntityType entity,
            ServerAdaptivePerformanceSnapshot tuning,
            float now,
            float minimumRecheckSeconds)
        {
            if (entity == null || entity.Transform == null)
            {
                return;
            }

            WeatherEntityState entityState = GetEntityState(state, entity);
            Vector3 position = entity.Transform.position;
            float movementSqr = (position - entityState.LastPosition).sqrMagnitude;
            if (movementSqr < tuning.WeatherEntityMovementThresholdSqr && now < entityState.NextEligibleUpdateTime)
            {
                return;
            }

            WeatherSampleState sample = GetWeatherSample(manager, state, position, tuning, now);
            entity.IsUnderCover = sample.IsUnderCover;

            string nextWeatherVolume = sample.Profile != null ? sample.Profile.name : string.Empty;
            string currentWeatherVolume = entity.WeatherVolume ?? string.Empty;
            if (!string.IsNullOrEmpty(nextWeatherVolume) && !string.Equals(currentWeatherVolume, nextWeatherVolume, StringComparison.Ordinal))
            {
                entity.WeatherVolume = nextWeatherVolume;
                entity.OnWeatherChange(sample.Profile.Conditions);
            }

            entityState.LastPosition = position;
            entityState.NextEligibleUpdateTime = now + minimumRecheckSeconds;
        }

        private static WeatherEntityState GetEntityState(WeatherManagerState state, WeatherEntityType entity)
        {
            if (!state.EntityStates.TryGetValue(entity, out WeatherEntityState entityState))
            {
                entityState = new WeatherEntityState
                {
                    NextEligibleUpdateTime = 0f,
                    LastPosition = Vector3.positiveInfinity
                };

                state.EntityStates[entity] = entityState;
            }

            return entityState;
        }

        private static WeatherSampleState GetWeatherSample(
            EnvironmentManagerType manager,
            WeatherManagerState state,
            Vector3 position,
            ServerAdaptivePerformanceSnapshot tuning,
            float now)
        {
            WeatherSpatialKey key = WeatherSpatialKey.From(position, tuning.WeatherSpatialCacheCellSize);
            if (state.SpatialSamples.TryGetValue(key, out WeatherSampleState cachedSample) &&
                now - cachedSample.CreatedAt <= tuning.WeatherSpatialCacheLifetimeSeconds)
            {
                return cachedSample;
            }

            WeatherProfileType profile = manager.GetWeatherProfileFromPosition(position);
            bool isUnderCover = manager.IsPositionUnderCover(position);
            WeatherSampleState sample = new WeatherSampleState(profile, isUnderCover, now);

            if (!state.SpatialSamples.ContainsKey(key))
            {
                state.SpatialSampleOrder.Enqueue(key);
            }

            state.SpatialSamples[key] = sample;
            TrimSpatialSamples(state);
            return sample;
        }

        private static void PruneSpatialSamples(WeatherManagerState state, ServerAdaptivePerformanceSnapshot tuning, float now)
        {
            int checks = state.SpatialSampleOrder.Count;
            for (int i = 0; i < checks; i++)
            {
                WeatherSpatialKey key = state.SpatialSampleOrder.Peek();
                if (!state.SpatialSamples.TryGetValue(key, out WeatherSampleState sample))
                {
                    state.SpatialSampleOrder.Dequeue();
                    continue;
                }

                if (now - sample.CreatedAt <= tuning.WeatherSpatialCacheLifetimeSeconds)
                {
                    break;
                }

                state.SpatialSamples.Remove(key);
                state.SpatialSampleOrder.Dequeue();
            }
        }

        private static void TrimSpatialSamples(WeatherManagerState state)
        {
            while (state.SpatialSamples.Count > MaximumSpatialSamples && state.SpatialSampleOrder.Count > 0)
            {
                WeatherSpatialKey key = state.SpatialSampleOrder.Dequeue();
                state.SpatialSamples.Remove(key);
            }
        }

        private static bool IsNpcWeatherEntity(WeatherEntityType entity)
        {
            return entity is NpcType;
        }

        private static bool IsDynamicWeatherEntity(WeatherEntityType entity)
        {
            return entity is LandVehicleType || entity is SkateboardType;
        }
    }

    /// <summary>
    /// Replaces the weather presentation update loop with a dedicated-server fast path that keeps authoritative weather state for gameplay entities on an adaptive cadence.
    /// </summary>
    [HarmonyPatch(typeof(EnvironmentManagerType), "Update")]
    internal static class EnvironmentManagerFastPathPatches
    {
        private static bool Prefix(EnvironmentManagerType __instance)
        {
            if (!DedicatedServerPatchCommon.IsDedicatedHeadlessServer() || __instance == null)
            {
                return true;
            }

            if (__instance._activeWeatherVolumes == null || __instance._activeWeatherVolumes.Count == 0)
            {
                return false;
            }

            AdaptiveWeatherEntityUpdater.Update(__instance);
            return false;
        }
    }
}
