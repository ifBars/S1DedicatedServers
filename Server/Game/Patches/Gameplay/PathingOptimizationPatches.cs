using System;
using System.Collections.Generic;
using System.Reflection;
using DedicatedServerMod.Server.Game.Patches.Common;
using HarmonyLib;
using Pathfinding;
using ScheduleOne.NPCs;
using UnityEngine;
using UnityEngine.AI;

namespace DedicatedServerMod.Server.Game.Patches.Gameplay
{
    internal readonly struct Vector3CellKey : IEquatable<Vector3CellKey>
    {
        private readonly int x;
        private readonly int y;
        private readonly int z;

        private Vector3CellKey(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        internal static Vector3CellKey From(Vector3 value, float cellSize)
        {
            float safeCellSize = Mathf.Max(cellSize, 0.01f);
            return new Vector3CellKey(
                Mathf.RoundToInt(value.x / safeCellSize),
                Mathf.RoundToInt(value.y / safeCellSize),
                Mathf.RoundToInt(value.z / safeCellSize));
        }

        public bool Equals(Vector3CellKey other)
        {
            return x == other.x && y == other.y && z == other.z;
        }

        public override bool Equals(object obj)
        {
            return obj is Vector3CellKey other && Equals(other);
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

    internal readonly struct NpcPathCacheKey : IEquatable<NpcPathCacheKey>
    {
        private readonly Vector3CellKey start;
        private readonly Vector3CellKey end;

        private NpcPathCacheKey(Vector3CellKey start, Vector3CellKey end)
        {
            this.start = start;
            this.end = end;
        }

        internal static NpcPathCacheKey From(Vector3 start, Vector3 end)
        {
            ServerAdaptivePerformanceSnapshot tuning = ServerAdaptivePerformanceTuning.GetSnapshot();
            return new NpcPathCacheKey(
                Vector3CellKey.From(start, tuning.NpcSharedCacheCellSize),
                Vector3CellKey.From(end, tuning.NpcSharedCacheCellSize));
        }

        public bool Equals(NpcPathCacheKey other)
        {
            return start.Equals(other.start) && end.Equals(other.end);
        }

        public override bool Equals(object obj)
        {
            return obj is NpcPathCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (start.GetHashCode() * 397) ^ end.GetHashCode();
            }
        }
    }

    internal sealed class SharedNpcPathEntry
    {
        internal SharedNpcPathEntry(Vector3 start, Vector3 end, NavMeshPath path, float createdAt)
        {
            Start = start;
            End = end;
            Path = path;
            CreatedAt = createdAt;
        }

        internal Vector3 Start { get; }

        internal Vector3 End { get; }

        internal NavMeshPath Path { get; }

        internal float CreatedAt { get; }
    }

    internal static class SharedNpcPathCache
    {
        private static readonly Dictionary<NpcPathCacheKey, SharedNpcPathEntry> Entries = new();
        private static readonly Queue<NpcPathCacheKey> InsertionOrder = new();
        private const int MaximumEntries = 512;

        internal static bool TryGet(Vector3 start, Vector3 end, float sqrMaxDistance, out NavMeshPath path)
        {
            path = null;
            Prune();

            if (!Entries.TryGetValue(NpcPathCacheKey.From(start, end), out SharedNpcPathEntry entry))
            {
                return false;
            }

            float toleranceSqr = Mathf.Max(sqrMaxDistance, ServerAdaptivePerformanceTuning.GetSnapshot().NpcPathCacheToleranceSqr);
            if ((entry.Start - start).sqrMagnitude > toleranceSqr || (entry.End - end).sqrMagnitude > toleranceSqr)
            {
                return false;
            }

            path = entry.Path;
            return path != null;
        }

        internal static void Store(Vector3 start, Vector3 end, NavMeshPath path)
        {
            if (path == null || path.corners == null || path.corners.Length < 2 || path.status == NavMeshPathStatus.PathInvalid)
            {
                return;
            }

            Prune();

            NpcPathCacheKey key = NpcPathCacheKey.From(start, end);
            if (!Entries.ContainsKey(key))
            {
                InsertionOrder.Enqueue(key);
            }

            Entries[key] = new SharedNpcPathEntry(start, end, path, Time.realtimeSinceStartup);
            TrimToCapacity();
        }

        private static void Prune()
        {
            if (Entries.Count == 0)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            float lifetimeSeconds = ServerAdaptivePerformanceTuning.GetSnapshot().NpcSharedCacheLifetimeSeconds;
            int checks = InsertionOrder.Count;
            for (int i = 0; i < checks; i++)
            {
                NpcPathCacheKey key = InsertionOrder.Peek();
                if (!Entries.TryGetValue(key, out SharedNpcPathEntry entry))
                {
                    InsertionOrder.Dequeue();
                    continue;
                }

                if (now - entry.CreatedAt <= lifetimeSeconds)
                {
                    break;
                }

                Entries.Remove(key);
                InsertionOrder.Dequeue();
            }
        }

        private static void TrimToCapacity()
        {
            while (Entries.Count > MaximumEntries && InsertionOrder.Count > 0)
            {
                NpcPathCacheKey key = InsertionOrder.Dequeue();
                Entries.Remove(key);
            }
        }
    }

    internal sealed class CachedNodeLinkResult
    {
        internal CachedNodeLinkResult(List<NodeLink> links, float createdAt)
        {
            Links = links;
            CreatedAt = createdAt;
        }

        internal List<NodeLink> Links { get; }

        internal float CreatedAt { get; }
    }

    internal readonly struct NodeLinkDistance
    {
        internal NodeLinkDistance(NodeLink link, float distanceSqr)
        {
            Link = link;
            DistanceSqr = distanceSqr;
        }

        internal NodeLink Link { get; }

        internal float DistanceSqr { get; }
    }

    internal static class VehicleNodeLinkCache
    {
        private static readonly Dictionary<Vector3CellKey, CachedNodeLinkResult> Entries = new();
        private static readonly Queue<Vector3CellKey> InsertionOrder = new();
        private const float CacheCellSize = 2f;
        private const int MaximumEntries = 256;

        internal static bool TryGet(Vector3 point, out List<NodeLink> links)
        {
            links = null;
            Prune();

            if (!Entries.TryGetValue(Vector3CellKey.From(point, CacheCellSize), out CachedNodeLinkResult entry))
            {
                return false;
            }

            if (entry.Links == null || entry.Links.Count == 0)
            {
                return false;
            }

            for (int i = entry.Links.Count - 1; i >= 0; i--)
            {
                NodeLink link = entry.Links[i];
                if (link == null || link.Start == null || link.End == null)
                {
                    Entries.Remove(Vector3CellKey.From(point, CacheCellSize));
                    return false;
                }
            }

            links = new List<NodeLink>(entry.Links);
            return true;
        }

        internal static void Store(Vector3 point, List<NodeLink> links)
        {
            if (links == null || links.Count == 0)
            {
                return;
            }

            Prune();

            Vector3CellKey key = Vector3CellKey.From(point, CacheCellSize);
            if (!Entries.ContainsKey(key))
            {
                InsertionOrder.Enqueue(key);
            }

            Entries[key] = new CachedNodeLinkResult(new List<NodeLink>(links), Time.realtimeSinceStartup);
            TrimToCapacity();
        }

        private static void Prune()
        {
            if (Entries.Count == 0)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            float lifetimeSeconds = ServerAdaptivePerformanceTuning.GetSnapshot().VehicleNodeLinkCacheLifetimeSeconds;
            int checks = InsertionOrder.Count;
            for (int i = 0; i < checks; i++)
            {
                Vector3CellKey key = InsertionOrder.Peek();
                if (!Entries.TryGetValue(key, out CachedNodeLinkResult entry))
                {
                    InsertionOrder.Dequeue();
                    continue;
                }

                if (now - entry.CreatedAt <= lifetimeSeconds)
                {
                    break;
                }

                Entries.Remove(key);
                InsertionOrder.Dequeue();
            }
        }

        private static void TrimToCapacity()
        {
            while (Entries.Count > MaximumEntries && InsertionOrder.Count > 0)
            {
                Vector3CellKey key = InsertionOrder.Dequeue();
                Entries.Remove(key);
            }
        }
    }

    /// <summary>
    /// Expands the built-in NPC path cache tolerance slightly on dedicated servers so repeated job destinations reuse existing NavMesh paths.
    /// </summary>
    /// <remarks>
    /// This is an adaptive performance optimization. If NPCs start showing delayed reroutes, stalls, or other
    /// movement/pathfinding regressions, revisit this repath gate before changing broader navigation systems.
    /// </remarks>
    [HarmonyPatch]
    internal static class NpcMovementSetDestinationPatches
    {
        private sealed class RepathGateState
        {
            internal Vector3 LastDestination { get; set; }

            internal float LastIssuedAt { get; set; }
        }

        private static readonly Dictionary<int, RepathGateState> RepathGateStates = new();

        private const int RepathGateCapacityBeforeReset = 4096;

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(NPCMovement),
                "SetDestination",
                new[]
                {
                    typeof(Vector3),
                    typeof(Action<NPCMovement.WalkResult>),
                    typeof(bool),
                    typeof(float),
                    typeof(float)
                });
        }

        private static bool Prefix(
            NPCMovement __instance,
            Vector3 pos,
            Action<NPCMovement.WalkResult> callback,
            bool interruptExistingCallback,
            ref float cacheMaxDistSqr)
        {
            if (!DedicatedServerPatchCommon.IsDedicatedHeadlessServer())
            {
                return true;
            }

            // Combat and pursuit paths intentionally pass a tiny cache tolerance
            // (for example 0.1f) so moving-target repaths stay accurate.
            // Only widen the broader default/static travel paths.
            bool usesBroadCacheTolerance = cacheMaxDistSqr >= 1f;
            ServerAdaptivePerformanceSnapshot tuning = ServerAdaptivePerformanceTuning.GetSnapshot();
            if (usesBroadCacheTolerance)
            {
                cacheMaxDistSqr = Mathf.Max(cacheMaxDistSqr, tuning.NpcPathCacheToleranceSqr);
            }

            if (ShouldSkipDuplicateRepath(__instance, pos, callback, interruptExistingCallback, usesBroadCacheTolerance, tuning))
            {
                return false;
            }

            return true;
        }

        private static bool ShouldSkipDuplicateRepath(
            NPCMovement movement,
            Vector3 destination,
            Action<NPCMovement.WalkResult> callback,
            bool interruptExistingCallback,
            bool usesBroadCacheTolerance,
            ServerAdaptivePerformanceSnapshot tuning)
        {
            if (movement == null ||
                callback != null ||
                interruptExistingCallback ||
                !usesBroadCacheTolerance ||
                !movement.HasDestination ||
                movement.Agent == null ||
                (!movement.Agent.pathPending && !movement.IsMoving))
            {
                return false;
            }

            float similaritySqr = GetDestinationSimilaritySqr(tuning);
            if ((movement.CurrentDestination - destination).sqrMagnitude > similaritySqr)
            {
                return false;
            }

            int instanceId = movement.GetInstanceID();
            float now = Time.realtimeSinceStartup;
            float minIntervalSeconds = GetMinimumRepathIntervalSeconds(tuning);
            if (RepathGateStates.TryGetValue(instanceId, out RepathGateState existingState))
            {
                if (now - existingState.LastIssuedAt < minIntervalSeconds &&
                    (existingState.LastDestination - destination).sqrMagnitude <= similaritySqr)
                {
                    return true;
                }

                existingState.LastDestination = destination;
                existingState.LastIssuedAt = now;
                return false;
            }

            if (RepathGateStates.Count > RepathGateCapacityBeforeReset)
            {
                RepathGateStates.Clear();
            }

            RepathGateStates[instanceId] = new RepathGateState
            {
                LastDestination = destination,
                LastIssuedAt = now
            };
            return false;
        }

        private static float GetMinimumRepathIntervalSeconds(ServerAdaptivePerformanceSnapshot tuning)
        {
            float cadenceFloor = tuning.TickDeltaSeconds > 0f ? tuning.TickDeltaSeconds : 0.05f;

            switch (tuning.PressureLevel)
            {
                case ServerPressureLevel.High:
                    return Mathf.Max(0.5f, cadenceFloor * 10f);

                case ServerPressureLevel.Medium:
                    return Mathf.Max(0.3f, cadenceFloor * 6f);

                default:
                    return Mathf.Max(0.2f, cadenceFloor * 4f);
            }
        }

        private static float GetDestinationSimilaritySqr(ServerAdaptivePerformanceSnapshot tuning)
        {
            // Keep the repath gate only for materially identical destinations.
            return Mathf.Max(0.25f, tuning.NpcPathCacheToleranceSqr * 0.25f);
        }
    }

    /// <summary>
    /// Reuses recently-computed foot NPC NavMesh paths across different NPCs on dedicated servers.
    /// </summary>
    /// <remarks>
    /// Shared path reuse lowers NavMesh churn, but it can possibly surface NPC pathfinding issues if nearby agents
    /// with different local constraints are forced through the same cached route shape.
    /// </remarks>
    [HarmonyPatch(typeof(NPCPathCache), nameof(NPCPathCache.GetPath))]
    internal static class NpcPathCacheGetPathPatches
    {
        private static void Postfix(Vector3 start, Vector3 end, float sqrMaxDistance, ref NavMeshPath __result)
        {
            if (!DedicatedServerPatchCommon.IsDedicatedHeadlessServer() || __result != null)
            {
                return;
            }

            if (SharedNpcPathCache.TryGet(start, end, sqrMaxDistance, out NavMeshPath cachedPath))
            {
                __result = cachedPath;
            }
        }
    }

    /// <summary>
    /// Stores successful foot NPC NavMesh paths into a shared server cache for reuse by nearby NPCs.
    /// </summary>
    /// <remarks>
    /// This cache should be treated as a performance hint, not a correctness guarantee. If NPC navigation starts to
    /// diverge or oscillate, audit this shared cache first.
    /// </remarks>
    [HarmonyPatch(typeof(NPCPathCache), nameof(NPCPathCache.AddPath))]
    internal static class NpcPathCacheAddPathPatches
    {
        private static void Postfix(Vector3 start, Vector3 end, NavMeshPath path)
        {
            if (!DedicatedServerPatchCommon.IsDedicatedHeadlessServer())
            {
                return;
            }

            SharedNpcPathCache.Store(start, end, path);
        }
    }

    /// <summary>
    /// Reduces dedicated-server vehicle road search breadth by caching and capping the nearest node-link candidates used for route generation.
    /// </summary>
    /// <remarks>
    /// This optimization can possibly affect vehicle movement/pathfinding if stale or over-pruned node-link sets stop
    /// a vehicle from considering the route it would normally choose. Revisit this patch first if vehicle routing
    /// becomes hesitant, incorrect, or fails outright.
    /// </remarks>
    [HarmonyPatch(typeof(NodeLink), nameof(NodeLink.GetClosestLinks))]
    internal static class NodeLinkGetClosestLinksPatches
    {
        private static bool Prefix(Vector3 point, ref List<NodeLink> __result)
        {
            if (!DedicatedServerPatchCommon.IsDedicatedHeadlessServer())
            {
                return true;
            }

            if (VehicleNodeLinkCache.TryGet(point, out List<NodeLink> cachedLinks))
            {
                __result = cachedLinks;
                return false;
            }

            List<NodeLink> orderedLinks = CreateOrderedLinks(point);
            VehicleNodeLinkCache.Store(point, orderedLinks);
            __result = orderedLinks;
            return false;
        }

        private static void Postfix(Vector3 point, ref List<NodeLink> __result)
        {
            if (!DedicatedServerPatchCommon.IsDedicatedHeadlessServer() || __result == null || __result.Count == 0)
            {
                return;
            }

            VehicleNodeLinkCache.Store(point, __result);
        }

        private static List<NodeLink> CreateOrderedLinks(Vector3 point)
        {
            List<NodeLink> validLinks = NodeLink.validNodeLinks;
            if (validLinks == null || validLinks.Count == 0)
            {
                return new List<NodeLink>();
            }

            List<NodeLinkDistance> orderedDistances = new(validLinks.Count);
            for (int i = 0; i < validLinks.Count; i++)
            {
                NodeLink link = validLinks[i];
                if (link == null || link.Start == null || link.End == null)
                {
                    continue;
                }

                Vector3 closestPoint = link.GetClosestPoint(point);
                float distanceSqr = (closestPoint - point).sqrMagnitude;
                orderedDistances.Add(new NodeLinkDistance(link, distanceSqr));
            }

            orderedDistances.Sort((left, right) => left.DistanceSqr.CompareTo(right.DistanceSqr));

            List<NodeLink> orderedLinks = new(orderedDistances.Count);
            for (int i = 0; i < orderedDistances.Count; i++)
            {
                orderedLinks.Add(orderedDistances[i].Link);
            }

            return orderedLinks;
        }
    }
}
