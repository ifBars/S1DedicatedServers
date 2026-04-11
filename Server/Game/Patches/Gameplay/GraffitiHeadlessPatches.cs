using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using DedicatedServerMod.Server.Game.Patches.Common;
using HarmonyLib;
#if IL2CPP
using Il2CppInterop.Runtime;
using DrawingType = Il2CppScheduleOne.Graffiti.Drawing;
using PixelDataType = Il2CppScheduleOne.Graffiti.PixelData;
using SprayStrokeType = Il2CppScheduleOne.Graffiti.SprayStroke;
using SpraySurfaceType = Il2CppScheduleOne.Graffiti.SpraySurface;
using TextureChangedCallbackType = Il2CppSystem.Action;
#else
using DrawingType = ScheduleOne.Graffiti.Drawing;
using PixelDataType = ScheduleOne.Graffiti.PixelData;
using SprayStrokeType = ScheduleOne.Graffiti.SprayStroke;
using SpraySurfaceType = ScheduleOne.Graffiti.SpraySurface;
using TextureChangedCallbackType = System.Action;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Gameplay
{
    internal static class HeadlessGraffitiPatchState
    {
        private const int MaxUndoStates = 10;
        private const int CacheHistorySlot = 10;

        private static readonly FieldInfo DrawingField = AccessTools.Field(typeof(SpraySurfaceType), "drawing");
        private static readonly MethodInfo DrawingChangedMethod = AccessTools.Method(typeof(SpraySurfaceType), "DrawingChanged");

        private static readonly FieldInfo StrokesField = AccessTools.Field(typeof(DrawingType), "strokes");
        private static readonly FieldInfo HistoryTextureArrayField = AccessTools.Field(typeof(DrawingType), "_historyTextureArray");
        private static readonly FieldInfo PaintedPixelHistoryField = AccessTools.Field(typeof(DrawingType), "PaintedPixelHistory");
        private static readonly FieldInfo StrokeHistoryField = AccessTools.Field(typeof(DrawingType), "_strokeHistory");

        private static readonly MethodInfo WidthSetter = AccessTools.PropertySetter(typeof(DrawingType), "_width");
        private static readonly MethodInfo HeightSetter = AccessTools.PropertySetter(typeof(DrawingType), "_height");
        private static readonly MethodInfo WidthGetter = AccessTools.PropertyGetter(typeof(DrawingType), "_width");
        private static readonly MethodInfo HeightGetter = AccessTools.PropertyGetter(typeof(DrawingType), "_height");
        private static readonly MethodInfo OutputTextureSetter = AccessTools.PropertySetter(typeof(DrawingType), "OutputTexture");
        private static readonly MethodInfo HistoryIndexSetter = AccessTools.PropertySetter(typeof(DrawingType), "HistoryIndex");
        private static readonly MethodInfo HistoryCountSetter = AccessTools.PropertySetter(typeof(DrawingType), "HistoryCount");

        internal static bool ShouldUseHeadlessDrawing()
        {
            return DedicatedServerPatchCommon.IsDedicatedHeadlessServer();
        }

        internal static DrawingType GetDrawing(SpraySurfaceType surface)
        {
            return surface == null ? null : (DrawingType)DrawingField.GetValue(surface);
        }

        internal static void SetDrawing(SpraySurfaceType surface, DrawingType drawing)
        {
            DrawingField.SetValue(surface, drawing);
        }

        internal static bool IsHeadless(DrawingType drawing)
        {
            return ShouldUseHeadlessDrawing() && drawing != null && drawing.OutputTexture == null;
        }

        internal static DrawingType CreateHeadlessDrawing(int width, int height)
        {
            var drawing = (DrawingType)FormatterServices.GetUninitializedObject(typeof(DrawingType));
            StrokesField.SetValue(drawing, new List<SprayStrokeType>());
            HistoryTextureArrayField.SetValue(drawing, null);
            PaintedPixelHistoryField.SetValue(drawing, new int[CacheHistorySlot + 1]);
            StrokeHistoryField.SetValue(drawing, new int[MaxUndoStates]);
            WidthSetter.Invoke(drawing, new object[] { width });
            HeightSetter.Invoke(drawing, new object[] { height });
            OutputTextureSetter.Invoke(drawing, new object[] { null });
            drawing.PaintedPixelCount = 0;
            SetHistoryIndex(drawing, -1);
            SetHistoryCount(drawing, 0);
            drawing.onTextureChanged = null;
            return drawing;
        }

        internal static DrawingType CloneHeadlessDrawing(DrawingType source)
        {
            var copy = CreateHeadlessDrawing(GetWidth(source), GetHeight(source));
            GetStrokes(copy).AddRange(GetStrokes(source));
            Array.Copy(GetPaintedPixelHistory(source), GetPaintedPixelHistory(copy), CacheHistorySlot + 1);
            Array.Copy(GetStrokeHistory(source), GetStrokeHistory(copy), MaxUndoStates);
            copy.PaintedPixelCount = source.PaintedPixelCount;
            SetHistoryIndex(copy, source.HistoryIndex);
            SetHistoryCount(copy, source.HistoryCount);
            return copy;
        }

        internal static List<SprayStrokeType> GetStrokes(DrawingType drawing)
        {
            return (List<SprayStrokeType>)StrokesField.GetValue(drawing);
        }

        internal static int[] GetPaintedPixelHistory(DrawingType drawing)
        {
            return (int[])PaintedPixelHistoryField.GetValue(drawing);
        }

        internal static int[] GetStrokeHistory(DrawingType drawing)
        {
            return (int[])StrokeHistoryField.GetValue(drawing);
        }

        internal static int GetWidth(DrawingType drawing)
        {
            return (int)WidthGetter.Invoke(drawing, null);
        }

        internal static int GetHeight(DrawingType drawing)
        {
            return (int)HeightGetter.Invoke(drawing, null);
        }

        internal static void SetHistoryIndex(DrawingType drawing, int value)
        {
            HistoryIndexSetter.Invoke(drawing, new object[] { value });
        }

        internal static void SetHistoryCount(DrawingType drawing, int value)
        {
            HistoryCountSetter.Invoke(drawing, new object[] { value });
        }

        internal static int CountPaintOperations(IEnumerable<SprayStrokeType> strokes)
        {
            int total = 0;
            if (strokes == null)
            {
                return total;
            }

            foreach (var stroke in strokes)
            {
                if (stroke == null)
                {
                    continue;
                }

                var pixels = stroke.GetPixelsFromStroke();
                if (pixels != null)
                {
                    total += pixels.Count;
                }
            }

            return total;
        }

        internal static TextureChangedCallbackType CreateDrawingChangedCallback(SpraySurfaceType surface)
        {
            if (DrawingChangedMethod == null || surface == null)
            {
                return null;
            }

#if IL2CPP
            System.Action callback = (System.Action)Delegate.CreateDelegate(typeof(System.Action), surface, DrawingChangedMethod);
            return DelegateSupport.ConvertDelegate<TextureChangedCallbackType>(callback);
#else
            return (TextureChangedCallbackType)Delegate.CreateDelegate(typeof(TextureChangedCallbackType), surface, DrawingChangedMethod);
#endif
        }

        internal static void NotifyTextureChanged(DrawingType drawing)
        {
            drawing?.onTextureChanged?.Invoke();
        }

        internal static void NotifySurfaceChanged(SpraySurfaceType surface)
        {
            DrawingChangedMethod?.Invoke(surface, null);
        }
    }

    /// <summary>
    /// Replaces texture-backed graffiti drawing allocation with a metadata-only representation on dedicated servers.
    /// </summary>
    [HarmonyPatch(typeof(SpraySurfaceType), "CreateNewDrawing")]
    internal static class SpraySurfaceCreateNewDrawingHeadlessPatches
    {
        private static bool Prefix(SpraySurfaceType __instance)
        {
            if (!HeadlessGraffitiPatchState.ShouldUseHeadlessDrawing())
            {
                return true;
            }

            TextureChangedCallbackType callback = HeadlessGraffitiPatchState.CreateDrawingChangedCallback(__instance);
            DrawingType current = HeadlessGraffitiPatchState.GetDrawing(__instance);
            if (current != null && callback != null)
            {
#if IL2CPP
                Il2CppSystem.Delegate remaining = Il2CppSystem.Delegate.Remove(current.onTextureChanged, callback);
                current.onTextureChanged = remaining != null ? remaining.Cast<TextureChangedCallbackType>() : null;
#else
                current.onTextureChanged = (TextureChangedCallbackType)Delegate.Remove(current.onTextureChanged, callback);
#endif
            }

            DrawingType drawing = HeadlessGraffitiPatchState.CreateHeadlessDrawing(__instance.Width, __instance.Height);
            if (callback != null)
            {
#if IL2CPP
                drawing.onTextureChanged = drawing.onTextureChanged != null
                    ? Il2CppSystem.Delegate.Combine(drawing.onTextureChanged, callback).Cast<TextureChangedCallbackType>()
                    : callback;
#else
                drawing.onTextureChanged = (TextureChangedCallbackType)Delegate.Combine(drawing.onTextureChanged, callback);
#endif
            }

            HeadlessGraffitiPatchState.SetDrawing(__instance, drawing);
            HeadlessGraffitiPatchState.NotifySurfaceChanged(__instance);
            return false;
        }
    }

    /// <summary>
    /// Keeps headless drawings cloneable for any server-side code path that snapshots graffiti state.
    /// </summary>
    [HarmonyPatch(typeof(DrawingType), nameof(DrawingType.GetCopy))]
    internal static class DrawingGetCopyHeadlessPatches
    {
        private static bool Prefix(DrawingType __instance, ref DrawingType __result)
        {
            if (!HeadlessGraffitiPatchState.IsHeadless(__instance))
            {
                return true;
            }

            __result = HeadlessGraffitiPatchState.CloneHeadlessDrawing(__instance);
            return false;
        }
    }

    /// <summary>
    /// Tracks server-authoritative graffiti state without touching Unity textures in headless mode.
    /// </summary>
    [HarmonyPatch(typeof(DrawingType), nameof(DrawingType.AddStroke))]
    internal static class DrawingAddStrokeHeadlessPatches
    {
        private static bool Prefix(DrawingType __instance, SprayStrokeType stroke)
        {
            if (!HeadlessGraffitiPatchState.IsHeadless(__instance))
            {
                return true;
            }

            if (stroke == null)
            {
                return false;
            }

            HeadlessGraffitiPatchState.GetStrokes(__instance).Add(stroke);
            __instance.PaintedPixelCount += HeadlessGraffitiPatchState.CountPaintOperations(new[] { stroke });
            HeadlessGraffitiPatchState.NotifyTextureChanged(__instance);
            return false;
        }
    }

    /// <summary>
    /// Tracks server-authoritative multi-stroke updates without allocating render textures in headless mode.
    /// </summary>
    [HarmonyPatch(typeof(DrawingType), nameof(DrawingType.AddStrokes))]
    internal static class DrawingAddStrokesHeadlessPatches
    {
        private static bool Prefix(DrawingType __instance, List<SprayStrokeType> newStrokes)
        {
            if (!HeadlessGraffitiPatchState.IsHeadless(__instance))
            {
                return true;
            }

            if (newStrokes == null || newStrokes.Count == 0)
            {
                return false;
            }

            HeadlessGraffitiPatchState.GetStrokes(__instance).AddRange(newStrokes);
            __instance.PaintedPixelCount += HeadlessGraffitiPatchState.CountPaintOperations(newStrokes);
            HeadlessGraffitiPatchState.NotifyTextureChanged(__instance);
            return false;
        }
    }

    /// <summary>
    /// Keeps temporary paint counters moving on the server without sampling or writing textures.
    /// </summary>
    [HarmonyPatch(typeof(DrawingType), nameof(DrawingType.DrawPaintedPixel))]
    internal static class DrawingDrawPaintedPixelHeadlessPatches
    {
        private static bool Prefix(DrawingType __instance, PixelDataType data, bool applyTexture)
        {
            if (!HeadlessGraffitiPatchState.IsHeadless(__instance))
            {
                return true;
            }

            if (data == null)
            {
                return false;
            }

            __instance.PaintedPixelCount++;
            if (applyTexture)
            {
                HeadlessGraffitiPatchState.NotifyTextureChanged(__instance);
            }

            return false;
        }
    }

    /// <summary>
    /// Records undo checkpoints for headless graffiti state using stroke and paint counters only.
    /// </summary>
    [HarmonyPatch(typeof(DrawingType), nameof(DrawingType.AddTextureToHistory))]
    internal static class DrawingAddTextureToHistoryHeadlessPatches
    {
        private static bool Prefix(DrawingType __instance)
        {
            if (!HeadlessGraffitiPatchState.IsHeadless(__instance))
            {
                return true;
            }

            int nextIndex = (__instance.HistoryIndex + 1 + 10) % 10;
            HeadlessGraffitiPatchState.SetHistoryIndex(__instance, nextIndex);
            HeadlessGraffitiPatchState.GetPaintedPixelHistory(__instance)[nextIndex] = __instance.PaintedPixelCount;
            HeadlessGraffitiPatchState.GetStrokeHistory(__instance)[nextIndex] = HeadlessGraffitiPatchState.GetStrokes(__instance).Count;
            HeadlessGraffitiPatchState.SetHistoryCount(__instance, Math.Min(__instance.HistoryCount + 1, 10));
            return false;
        }
    }

    /// <summary>
    /// Preserves the headless paint counter cache used by the graffiti interaction flow.
    /// </summary>
    [HarmonyPatch(typeof(DrawingType), nameof(DrawingType.CacheDrawing))]
    internal static class DrawingCacheHeadlessPatches
    {
        private static bool Prefix(DrawingType __instance)
        {
            if (!HeadlessGraffitiPatchState.IsHeadless(__instance))
            {
                return true;
            }

            HeadlessGraffitiPatchState.GetPaintedPixelHistory(__instance)[10] = __instance.PaintedPixelCount;
            return false;
        }
    }

    /// <summary>
    /// Restores the cached headless paint counter without any texture copies.
    /// </summary>
    [HarmonyPatch(typeof(DrawingType), nameof(DrawingType.RestoreFromCache))]
    internal static class DrawingRestoreFromCacheHeadlessPatches
    {
        private static bool Prefix(DrawingType __instance)
        {
            if (!HeadlessGraffitiPatchState.IsHeadless(__instance))
            {
                return true;
            }

            __instance.PaintedPixelCount = HeadlessGraffitiPatchState.GetPaintedPixelHistory(__instance)[10];
            return false;
        }
    }

    /// <summary>
    /// Applies undo to the headless metadata state so dedicated servers keep correct save and replication data.
    /// </summary>
    [HarmonyPatch(typeof(DrawingType), nameof(DrawingType.Undo))]
    internal static class DrawingUndoHeadlessPatches
    {
        private static bool Prefix(DrawingType __instance)
        {
            if (!HeadlessGraffitiPatchState.IsHeadless(__instance))
            {
                return true;
            }

            if (__instance.HistoryCount <= 0 || __instance.HistoryIndex < 0)
            {
                return false;
            }

            int historyIndex = __instance.HistoryIndex;
            __instance.PaintedPixelCount = HeadlessGraffitiPatchState.GetPaintedPixelHistory(__instance)[historyIndex];

            List<SprayStrokeType> strokes = HeadlessGraffitiPatchState.GetStrokes(__instance);
            int targetStrokeCount = HeadlessGraffitiPatchState.GetStrokeHistory(__instance)[historyIndex];
            if (targetStrokeCount < strokes.Count)
            {
                strokes.RemoveRange(targetStrokeCount, strokes.Count - targetStrokeCount);
            }

            HeadlessGraffitiPatchState.SetHistoryIndex(__instance, (historyIndex - 1 + 10) % 10);
            HeadlessGraffitiPatchState.SetHistoryCount(__instance, __instance.HistoryCount - 1);
            HeadlessGraffitiPatchState.NotifyTextureChanged(__instance);
            return false;
        }
    }
}
