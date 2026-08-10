using System;
using System.Runtime.CompilerServices;
using DedicatedServerMod.Utils;
using HarmonyLib;
using UnityEngine;
#if IL2CPP
using NpcMovementType = Il2CppScheduleOne.NPCs.NPCMovement;
using NpcType = Il2CppScheduleOne.NPCs.NPC;
#else
using NpcMovementType = ScheduleOne.NPCs.NPCMovement;
using NpcType = ScheduleOne.NPCs.NPC;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Gameplay
{
    /// <summary>
    /// Restores conscious NPCs whose headless ragdoll physics never reaches the vanilla velocity threshold.
    /// </summary>
    /// <remarks>
    /// Vanilla recovery remains authoritative and gets the first opportunity to run. This postfix only invokes the
    /// existing networked deactivation path after the NPC has remained continuously eligible for recovery for a
    /// conservative fallback interval. Knocked-out, dead, unconscious, paused, and seizure ragdolls are excluded.
    /// </remarks>
    [HarmonyPatch(typeof(NpcMovementType), "FixedUpdate")]
    internal static class NpcRagdollRecoveryPatches
    {
        private const float FALLBACK_RECOVERY_SECONDS = 8f;

        private static readonly ConditionalWeakTable<NpcMovementType, RecoveryState> RecoveryStates = new();

        private static void Postfix(NpcMovementType __instance)
        {
            if (__instance == null)
            {
                return;
            }

            if (!IsEligibleForFallback(__instance, out NpcType npc))
            {
                RecoveryStates.Remove(__instance);
                return;
            }

            if (!RecoveryStates.TryGetValue(__instance, out RecoveryState state))
            {
                state = new RecoveryState(Time.realtimeSinceStartup);
                RecoveryStates.Add(__instance, state);
                return;
            }

            float elapsedSeconds = Time.realtimeSinceStartup - state.EligibleSince;
            if (elapsedSeconds < FALLBACK_RECOVERY_SECONDS)
            {
                return;
            }

            RecoveryStates.Remove(__instance);
            try
            {
                __instance.DeactivateRagdoll();
                DebugLog.Info(
                    $"Recovered conscious NPC '{npc.FullName}' after headless ragdoll physics remained unsettled " +
                    $"for {elapsedSeconds:F1}s.");
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"Failed to recover unsettled NPC ragdoll '{npc.FullName}': {ex.Message}");
            }
        }

        private static bool IsEligibleForFallback(NpcMovementType movement, out NpcType npc)
        {
            npc = null;
            if (movement == null || movement.IsPaused || !movement.IsServerInitialized)
            {
                return false;
            }

            npc = movement.npc;
            if (npc == null ||
                npc.Avatar == null ||
                npc.Health == null ||
                !npc.Avatar.Ragdolled ||
                !npc.IsConscious ||
                npc.Health.IsDead ||
                npc.Health.IsKnockedOut)
            {
                return false;
            }

            try
            {
                return movement.CanRecoverFromRagdoll();
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"Could not evaluate ragdoll recovery eligibility for '{npc.FullName}': {ex.Message}");
                return false;
            }
        }

        private sealed class RecoveryState
        {
            internal RecoveryState(float eligibleSince)
            {
                EligibleSince = eligibleSince;
            }

            internal float EligibleSince { get; }
        }
    }
}
