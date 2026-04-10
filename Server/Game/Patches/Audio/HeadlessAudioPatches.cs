using DedicatedServerMod.Server.Game.Patches.Common;
using HarmonyLib;
using System;
using System.Reflection;
#if IL2CPP
using AmbientTrackGroupType = Il2CppScheduleOne.Audio.AmbientTrackGroup;
using AudioManagerType = Il2CppScheduleOne.Audio.AudioManager;
using AudioZoneType = Il2CppScheduleOne.Audio.AudioZone;
using HeartbeatSoundControllerType = Il2CppScheduleOne.Audio.HeartbeatSoundController;
using MusicTrackType = Il2CppScheduleOne.Audio.MusicTrack;
using SewerAmbientSoundType = Il2CppScheduleOne.Audio.SewerAmbientSound;
using SfxManagerType = Il2CppScheduleOne.Audio.SFXManager;
using SpottedTremoloType = Il2CppScheduleOne.Audio.SpottedTremolo;
using TimeOfDayVolumeControllerType = Il2CppScheduleOne.Audio.TimeOfDayVolumeController;
#else
using AmbientTrackGroupType = ScheduleOne.Audio.AmbientTrackGroup;
using AudioManagerType = ScheduleOne.Audio.AudioManager;
using AudioZoneType = ScheduleOne.Audio.AudioZone;
using HeartbeatSoundControllerType = ScheduleOne.Audio.HeartbeatSoundController;
using MusicTrackType = ScheduleOne.Audio.MusicTrack;
using SewerAmbientSoundType = ScheduleOne.Audio.SewerAmbientSound;
using SfxManagerType = ScheduleOne.Audio.SFXManager;
using SpottedTremoloType = ScheduleOne.Audio.SpottedTremolo;
using TimeOfDayVolumeControllerType = ScheduleOne.Audio.TimeOfDayVolumeController;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Audio
{
    internal static class HeadlessAudioPatchCommon
    {
        internal static Type GetTypeByName(string typeName)
        {
#if IL2CPP
            return AccessTools.TypeByName($"Il2CppScheduleOne.Audio.{typeName}")
                ?? AccessTools.TypeByName($"ScheduleOne.Audio.{typeName}");
#else
            return AccessTools.TypeByName($"ScheduleOne.Audio.{typeName}");
#endif
        }

        internal static MethodBase GetUpdateMethod(string typeName)
        {
            Type type = GetTypeByName(typeName);
            return type != null ? AccessTools.Method(type, "Update") : null;
        }
    }

    /// <summary>
    /// Skips mixer fade updates on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(AudioManagerType), "Update")]
    internal static class AudioManagerUpdatePatches
    {
        private static bool Prefix()
        {
            return DedicatedServerPatchCommon.ShouldRunClientPresentationAudio();
        }
    }

    /// <summary>
    /// Prevents audio zones from polling camera distance on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(AudioZoneType), "Awake")]
    internal static class AudioZoneAwakePatches
    {
        private static void Postfix(AudioZoneType __instance)
        {
            __instance?.CancelInvoke("RecalculateCameraDistance");
        }
    }

    /// <summary>
    /// Prevents audio zones from subscribing to time and initializing audio tracks on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(AudioZoneType), "Start")]
    internal static class AudioZoneStartPatches
    {
        private static bool Prefix()
        {
            return DedicatedServerPatchCommon.ShouldRunClientPresentationAudio();
        }
    }

    /// <summary>
    /// Skips audio-zone volume and track updates on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(AudioZoneType), "Update")]
    internal static class AudioZoneUpdatePatches
    {
        private static bool Prefix()
        {
            return DedicatedServerPatchCommon.ShouldRunClientPresentationAudio();
        }
    }

    /// <summary>
    /// Skips ambient music scheduling on dedicated headless servers.
    /// </summary>
    [HarmonyPatch]
    internal static class AmbientTrackUpdatePatches
    {
        private static MethodBase _targetMethod;

        private static bool Prepare()
        {
            _targetMethod = HeadlessAudioPatchCommon.GetUpdateMethod("AmbientTrack");
            return _targetMethod != null;
        }

        private static MethodBase TargetMethod()
        {
            return _targetMethod;
        }

        private static bool Prefix()
        {
            return DedicatedServerPatchCommon.ShouldRunClientPresentationAudio();
        }
    }

    /// <summary>
    /// Skips ambient track group updates on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(AmbientTrackGroupType), "Update")]
    internal static class AmbientTrackGroupUpdatePatches
    {
        private static bool Prefix()
        {
            return DedicatedServerPatchCommon.ShouldRunClientPresentationAudio();
        }
    }

    /// <summary>
    /// Skips ambient loop updates on dedicated headless servers.
    /// </summary>
    [HarmonyPatch]
    internal static class AmbientLoopUpdatePatches
    {
        private static MethodBase _targetMethod;

        private static bool Prepare()
        {
            _targetMethod = HeadlessAudioPatchCommon.GetUpdateMethod("AmbientLoop");
            return _targetMethod != null;
        }

        private static MethodBase TargetMethod()
        {
            return _targetMethod;
        }

        private static bool Prefix()
        {
            return DedicatedServerPatchCommon.ShouldRunClientPresentationAudio();
        }
    }

    /// <summary>
    /// Skips jukebox ambient loop updates on dedicated headless servers.
    /// </summary>
    [HarmonyPatch]
    internal static class AmbientLoopJukeboxUpdatePatches
    {
        private static MethodBase _targetMethod;

        private static bool Prepare()
        {
            _targetMethod = HeadlessAudioPatchCommon.GetUpdateMethod("AmbientLoopJukebox");
            return _targetMethod != null;
        }

        private static MethodBase TargetMethod()
        {
            return _targetMethod;
        }

        private static bool Prefix()
        {
            return DedicatedServerPatchCommon.ShouldRunClientPresentationAudio();
        }
    }

    /// <summary>
    /// Skips heartbeat audio modulation on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(HeartbeatSoundControllerType), "Update")]
    internal static class HeartbeatSoundControllerUpdatePatches
    {
        private static bool Prefix()
        {
            return DedicatedServerPatchCommon.ShouldRunClientPresentationAudio();
        }
    }

    /// <summary>
    /// Skips music player state updates on dedicated headless servers.
    /// </summary>
    [HarmonyPatch]
    internal static class MusicPlayerUpdatePatches
    {
        private static MethodBase _targetMethod;

        private static bool Prepare()
        {
            _targetMethod = HeadlessAudioPatchCommon.GetUpdateMethod("MusicPlayer");
            return _targetMethod != null;
        }

        private static MethodBase TargetMethod()
        {
            return _targetMethod;
        }

        private static bool Prefix()
        {
            return DedicatedServerPatchCommon.ShouldRunClientPresentationAudio();
        }
    }

    /// <summary>
    /// Skips music track playback maintenance on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(MusicTrackType), "Update")]
    internal static class MusicTrackUpdatePatches
    {
        private static bool Prefix()
        {
            return DedicatedServerPatchCommon.ShouldRunClientPresentationAudio();
        }
    }

    /// <summary>
    /// Skips pooled sound-source cleanup work on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(SfxManagerType), "Update")]
    internal static class SfxManagerUpdatePatches
    {
        private static bool Prefix()
        {
            return DedicatedServerPatchCommon.ShouldRunClientPresentationAudio();
        }
    }

    /// <summary>
    /// Skips sewer ambience updates on dedicated headless servers.
    /// </summary>
    [HarmonyPatch]
    internal static class SewerAmbienceUpdatePatches
    {
        private static MethodBase _targetMethod;

        private static bool Prepare()
        {
            _targetMethod = HeadlessAudioPatchCommon.GetUpdateMethod("SewerAmbience");
            return _targetMethod != null;
        }

        private static MethodBase TargetMethod()
        {
            return _targetMethod;
        }

        private static bool Prefix()
        {
            return DedicatedServerPatchCommon.ShouldRunClientPresentationAudio();
        }
    }

    /// <summary>
    /// Skips sewer ambient sound updates on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(SewerAmbientSoundType), "Update")]
    internal static class SewerAmbientSoundUpdatePatches
    {
        private static bool Prefix()
        {
            return DedicatedServerPatchCommon.ShouldRunClientPresentationAudio();
        }
    }

    /// <summary>
    /// Skips spotted tremolo audio updates on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(SpottedTremoloType), "Update")]
    internal static class SpottedTremoloUpdatePatches
    {
        private static bool Prefix()
        {
            return DedicatedServerPatchCommon.ShouldRunClientPresentationAudio();
        }
    }

    /// <summary>
    /// Skips time-of-day audio volume updates on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(TimeOfDayVolumeControllerType), "Update")]
    internal static class TimeOfDayVolumeControllerUpdatePatches
    {
        private static bool Prefix()
        {
            return DedicatedServerPatchCommon.ShouldRunClientPresentationAudio();
        }
    }

}
