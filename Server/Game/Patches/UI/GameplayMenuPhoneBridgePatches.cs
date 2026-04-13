using System;
using System.Reflection;
using DedicatedServerMod.Server.Game.Patches.Common;
using HarmonyLib;
#if IL2CPP
using GameplayMenuInterfaceType = Il2CppScheduleOne.UI.GameplayMenuInterface;
using GameplayMenuType = Il2CppScheduleOne.UI.GameplayMenu;
#else
using GameplayMenuInterfaceType = ScheduleOne.UI.GameplayMenuInterface;
using GameplayMenuType = ScheduleOne.UI.GameplayMenu;
#endif

namespace DedicatedServerMod.Server.Game.Patches.UI
{
    internal static class GameplayMenuPatchCommon
    {
        private static readonly FieldInfo IsOpenField = AccessTools.Field(typeof(GameplayMenuType), "<IsOpen>k__BackingField");
        private static readonly FieldInfo CurrentScreenField = AccessTools.Field(typeof(GameplayMenuType), "<CurrentScreen>k__BackingField");
        private static readonly object CharacterScreenValue = ResolveCharacterScreenValue();

        internal static void ForceClosed(GameplayMenuType gameplayMenu)
        {
            if (gameplayMenu == null)
            {
                return;
            }

            IsOpenField?.SetValue(gameplayMenu, false);

            if (gameplayMenu.OverlayCamera != null)
            {
                gameplayMenu.OverlayCamera.enabled = false;
            }

            if (gameplayMenu.OverlayLight != null)
            {
                gameplayMenu.OverlayLight.enabled = false;
            }
        }

        internal static void ForceCharacterScreen(GameplayMenuType gameplayMenu)
        {
            if (gameplayMenu == null || CurrentScreenField == null || CharacterScreenValue == null)
            {
                return;
            }

            CurrentScreenField.SetValue(gameplayMenu, CharacterScreenValue);
        }

        private static object ResolveCharacterScreenValue()
        {
            if (CurrentScreenField == null)
            {
                return null;
            }

            Type fieldType = CurrentScreenField.FieldType;
            return fieldType != null && fieldType.IsEnum
                ? Enum.Parse(fieldType, "Character")
                : null;
        }
    }

    /// <summary>
    /// Prevents gameplay-menu input polling from reopening the phone path on dedicated servers.
    /// </summary>
    [HarmonyPatch(typeof(GameplayMenuType), "Update")]
    internal static class GameplayMenuUpdatePatches
    {
        private static bool Prefix()
        {
            return !DedicatedServerPatchCommon.IsDedicatedHeadlessServer();
        }
    }

    /// <summary>
    /// Forces the gameplay menu to remain closed on dedicated servers.
    /// </summary>
    [HarmonyPatch(typeof(GameplayMenuType), "SetIsOpen")]
    internal static class GameplayMenuSetIsOpenPatches
    {
        private static bool Prefix(GameplayMenuType __instance)
        {
            if (!DedicatedServerPatchCommon.IsDedicatedHeadlessServer())
            {
                return true;
            }

            GameplayMenuPatchCommon.ForceClosed(__instance);
            GameplayMenuPatchCommon.ForceCharacterScreen(__instance);
            return false;
        }
    }

    /// <summary>
    /// Ignores gameplay-menu screen changes on dedicated servers and keeps the menu on the character screen.
    /// </summary>
    [HarmonyPatch(typeof(GameplayMenuType), "SetScreen")]
    internal static class GameplayMenuSetScreenPatches
    {
        private static bool Prefix(GameplayMenuType __instance)
        {
            if (!DedicatedServerPatchCommon.IsDedicatedHeadlessServer())
            {
                return true;
            }

            GameplayMenuPatchCommon.ForceCharacterScreen(__instance);
            return false;
        }
    }

    /// <summary>
    /// Normalizes gameplay-menu state after startup so the phone screen is never the active server-side default.
    /// </summary>
    [HarmonyPatch(typeof(GameplayMenuType), "Start")]
    internal static class GameplayMenuStartPatches
    {
        private static void Postfix(GameplayMenuType __instance)
        {
            if (!DedicatedServerPatchCommon.IsDedicatedHeadlessServer())
            {
                return;
            }

            GameplayMenuPatchCommon.ForceClosed(__instance);
            GameplayMenuPatchCommon.ForceCharacterScreen(__instance);
        }
    }

    /// <summary>
    /// Prevents gameplay-menu UI chrome from registering an overlay GraphicRaycaster on dedicated servers.
    /// </summary>
    [HarmonyPatch(typeof(GameplayMenuInterfaceType), "Start")]
    internal static class GameplayMenuInterfaceStartPatches
    {
        private static bool Prefix(GameplayMenuInterfaceType __instance)
        {
            if (!DedicatedServerPatchCommon.IsDedicatedHeadlessServer())
            {
                return true;
            }

            if (__instance?.Canvas != null)
            {
                __instance.Canvas.enabled = false;
            }

            return false;
        }
    }
}
