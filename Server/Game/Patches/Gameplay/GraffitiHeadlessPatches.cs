using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using DedicatedServerMod.Server.Game.Patches.Common;
using HarmonyLib;
#if IL2CPP
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using DrawingType = Il2CppScheduleOne.Graffiti.Drawing;
using IntArrayType = Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int>;
using PixelDataType = Il2CppScheduleOne.Graffiti.PixelData;
using SprayStrokeType = Il2CppScheduleOne.Graffiti.SprayStroke;
using SpraySurfaceType = Il2CppScheduleOne.Graffiti.SpraySurface;
using StrokeListType = Il2CppSystem.Collections.Generic.List<Il2CppScheduleOne.Graffiti.SprayStroke>;
using TextureChangedCallbackType = Il2CppSystem.Action;
#else
using DrawingType = ScheduleOne.Graffiti.Drawing;
using IntArrayType = System.Int32[];
using PixelDataType = ScheduleOne.Graffiti.PixelData;
using SprayStrokeType = ScheduleOne.Graffiti.SprayStroke;
using SpraySurfaceType = ScheduleOne.Graffiti.SpraySurface;
using StrokeListType = System.Collections.Generic.List<ScheduleOne.Graffiti.SprayStroke>;
using TextureChangedCallbackType = System.Action;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Gameplay
{
    internal static class HeadlessGraffitiPatchState
    {
        private const int MaxUndoStates = 10;
        private const int CacheHistorySlot = 10;

        private static readonly MemberInfo DrawingMember = FindFieldOrProperty(typeof(SpraySurfaceType), "drawing");
        private static readonly MethodInfo DrawingChangedMethod = AccessTools.Method(typeof(SpraySurfaceType), "DrawingChanged");

        private static readonly MemberInfo StrokesMember = FindFieldOrProperty(typeof(DrawingType), "strokes");
        private static readonly MemberInfo HistoryTextureArrayMember = FindFieldOrProperty(typeof(DrawingType), "_historyTextureArray");
        private static readonly MemberInfo PaintedPixelHistoryMember = FindFieldOrProperty(typeof(DrawingType), "PaintedPixelHistory");
        private static readonly MemberInfo StrokeHistoryMember = FindFieldOrProperty(typeof(DrawingType), "_strokeHistory");

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
            return surface == null ? null : (DrawingType)GetMemberValue(DrawingMember, surface);
        }

        internal static void SetDrawing(SpraySurfaceType surface, DrawingType drawing)
        {
            SetMemberValue(DrawingMember, surface, drawing);
        }

        internal static bool IsHeadless(DrawingType drawing)
        {
            return ShouldUseHeadlessDrawing() && drawing != null && drawing.OutputTexture == null;
        }

        internal static DrawingType CreateHeadlessDrawing(int width, int height)
        {
#if IL2CPP
            var drawing = new DrawingType(IL2CPP.il2cpp_object_new(Il2CppClassPointerStore<DrawingType>.NativeClassPtr));
#else
            var drawing = (DrawingType)FormatterServices.GetUninitializedObject(typeof(DrawingType));
#endif
            SetMemberValue(StrokesMember, drawing, new StrokeListType());
            SetMemberValue(HistoryTextureArrayMember, drawing, null);
            SetMemberValue(PaintedPixelHistoryMember, drawing, CreateIntArray(CacheHistorySlot + 1));
            SetMemberValue(StrokeHistoryMember, drawing, CreateIntArray(MaxUndoStates));
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
            CopyStrokes(GetStrokes(source), GetStrokes(copy));
            CopyIntArray(GetPaintedPixelHistory(source), GetPaintedPixelHistory(copy), CacheHistorySlot + 1);
            CopyIntArray(GetStrokeHistory(source), GetStrokeHistory(copy), MaxUndoStates);
            copy.PaintedPixelCount = source.PaintedPixelCount;
            SetHistoryIndex(copy, source.HistoryIndex);
            SetHistoryCount(copy, source.HistoryCount);
            return copy;
        }

        internal static StrokeListType GetStrokes(DrawingType drawing)
        {
            return (StrokeListType)GetMemberValue(StrokesMember, drawing);
        }

        internal static IntArrayType GetPaintedPixelHistory(DrawingType drawing)
        {
            return (IntArrayType)GetMemberValue(PaintedPixelHistoryMember, drawing);
        }

        internal static IntArrayType GetStrokeHistory(DrawingType drawing)
        {
            return (IntArrayType)GetMemberValue(StrokeHistoryMember, drawing);
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

        internal static int CountPaintOperations(StrokeListType strokes)
        {
            int total = 0;
            if (strokes == null)
            {
                return total;
            }

            for (int i = 0; i < strokes.Count; i++)
            {
                SprayStrokeType stroke = strokes[i];
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

        internal static int CountPaintOperations(SprayStrokeType stroke)
        {
            if (stroke == null)
            {
                return 0;
            }

            var pixels = stroke.GetPixelsFromStroke();
            return pixels?.Count ?? 0;
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

        private static MemberInfo FindFieldOrProperty(Type type, string name)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(name, flags);
                if (field != null)
                {
                    return field;
                }

                PropertyInfo property = current.GetProperty(name, flags);
                if (property != null)
                {
                    return property;
                }
            }

            return null;
        }

        private static object GetMemberValue(MemberInfo member, object instance)
        {
            if (member is FieldInfo field)
            {
                return field.GetValue(instance);
            }

            if (member is PropertyInfo property)
            {
                return property.GetValue(instance, null);
            }

            return null;
        }

        private static void SetMemberValue(MemberInfo member, object instance, object value)
        {
            if (member is FieldInfo field)
            {
                field.SetValue(instance, value);
                return;
            }

            if (member is PropertyInfo property)
            {
                property.SetValue(instance, value, null);
            }
        }

        private static void CopyStrokes(StrokeListType source, StrokeListType destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                destination.Add(source[i]);
            }
        }

        private static IntArrayType CreateIntArray(int length)
        {
#if IL2CPP
            return new IntArrayType(length);
#else
            return new int[length];
#endif
        }

        private static void CopyIntArray(IntArrayType source, IntArrayType destination, int count)
        {
            if (source == null || destination == null)
            {
                return;
            }

            int limit = Math.Min(count, Math.Min(source.Length, destination.Length));
            for (int i = 0; i < limit; i++)
            {
                destination[i] = source[i];
            }
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
            __instance.PaintedPixelCount += HeadlessGraffitiPatchState.CountPaintOperations(stroke);
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
        private static bool Prefix(DrawingType __instance, StrokeListType newStrokes)
        {
            if (!HeadlessGraffitiPatchState.IsHeadless(__instance))
            {
                return true;
            }

            if (newStrokes == null || newStrokes.Count == 0)
            {
                return false;
            }

            StrokeListType strokes = HeadlessGraffitiPatchState.GetStrokes(__instance);
            for (int i = 0; i < newStrokes.Count; i++)
            {
                strokes.Add(newStrokes[i]);
            }

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

            StrokeListType strokes = HeadlessGraffitiPatchState.GetStrokes(__instance);
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
