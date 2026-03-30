using System;
using System.Reflection;
using DedicatedServerMod.Shared;
using DedicatedServerMod.Shared.Networking;
using DedicatedServerMod.Utils;
#if IL2CPP
using Il2CppFishNet;
#else
using FishNet;
#endif
using HarmonyLib;
using MelonLoader;
#if IL2CPP
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI;
using TMPInputField = Il2CppTMPro.TMP_InputField;
#else
using ScheduleOne;
using ScheduleOne.DevUtilities;
using ScheduleOne.PlayerScripts;
using ScheduleOne.UI;
using TMPInputField = TMPro.TMP_InputField;
#endif
using UnityEngine;

namespace DedicatedServerMod.Client.Managers
{
    /// <summary>
    /// Handles client-side console access for admin/operator players on dedicated servers.
    /// Patches ConsoleUI to allow admin players to open and use the console.
    /// </summary>
    public sealed class ClientConsoleManager
    {
        private HarmonyLib.Harmony harmony;

        internal ClientConsoleManager()
        {
        }

        internal void Initialize()
        {
            try
            {
                DebugLog.Info("Initializing ClientConsoleManager");
                
                // Initialize admin status manager
                AdminStatusManager.Initialize();
                
                harmony = new HarmonyLib.Harmony("DedicatedServerMod.ClientConsoleManager");
                ApplyConsolePatches();
                
                DebugLog.Info("ClientConsoleManager initialized successfully");
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Failed to initialize ClientConsoleManager: {ex}");
            }
        }

        /// <summary>
        /// Apply console-related Harmony patches
        /// </summary>
        private void ApplyConsolePatches()
        {
            try
            {
                // Patch ConsoleUI.IS_CONSOLE_ENABLED property getter
                PatchConsoleEnabled();
                
                // Patch ConsoleUI.SetIsOpen method
                PatchConsoleSetIsOpen();
                
                // Patch Console.SubmitCommand to ensure client admin permissions are checked
                PatchConsoleSubmitCommand();

                // Hook client console submit to send to server via custom messaging as well
                // so servers without our Console.SubmitCommand server patch can still receive text.
                
                DebugLog.Info("Console patches applied successfully");
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Failed to apply console patches: {ex}");
            }
        }

        /// <summary>
        /// Patch the IS_CONSOLE_ENABLED property to allow admin access
        /// </summary>
        private void PatchConsoleEnabled()
        {
            var consoleUIType = typeof(ConsoleUI);
            var consoleEnabledProperty = consoleUIType.GetProperty("IS_CONSOLE_ENABLED", 
                BindingFlags.Public | BindingFlags.Instance);
            
            if (consoleEnabledProperty?.GetGetMethod() != null)
            {
                var prefixMethod = typeof(ClientConsoleManager).GetMethod(nameof(ConsoleEnabledPrefix), 
                    BindingFlags.Static | BindingFlags.NonPublic);
                harmony.Patch(consoleEnabledProperty.GetGetMethod(), new HarmonyMethod(prefixMethod));
                DebugLog.Info("Patched ConsoleUI.IS_CONSOLE_ENABLED");
            }
            else
            {
                DebugLog.Error("Could not find ConsoleUI.IS_CONSOLE_ENABLED property");
            }
        }

        /// <summary>
        /// Patch the SetIsOpen method to allow admin console opening
        /// </summary>
        private void PatchConsoleSetIsOpen()
        {
            var consoleUIType = typeof(ConsoleUI);
            var setIsOpenMethod = consoleUIType.GetMethod("SetIsOpen", 
                BindingFlags.Public | BindingFlags.Instance);
            
            if (setIsOpenMethod != null)
            {
                var prefixMethod = typeof(ClientConsoleManager).GetMethod(nameof(ConsoleSetIsOpenPrefix), 
                    BindingFlags.Static | BindingFlags.NonPublic);
                harmony.Patch(setIsOpenMethod, new HarmonyMethod(prefixMethod));
                DebugLog.Info("Patched ConsoleUI.SetIsOpen");
            }
            else
            {
                DebugLog.Error("Could not find ConsoleUI.SetIsOpen method");
            }
        }

        /// <summary>
        /// Patch Console.SubmitCommand for client-side validation
        /// </summary>
        private void PatchConsoleSubmitCommand()
        {
            var consoleType = typeof(ScheduleOne.Console);
            var submitCommandMethod = consoleType.GetMethod("SubmitCommand", 
                BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string) }, null);
            
            if (submitCommandMethod != null)
            {
                var prefixMethod = typeof(ClientConsoleManager).GetMethod(nameof(ConsoleSubmitCommandPrefix), 
                    BindingFlags.Static | BindingFlags.NonPublic);
                harmony.Patch(submitCommandMethod, new HarmonyMethod(prefixMethod));
                DebugLog.Info("Patched Console.SubmitCommand for client validation");
            }
            else
            {
                DebugLog.Error("Could not find Console.SubmitCommand method");
            }
        }

        #region Harmony Prefix Methods

        /// <summary>
        /// Harmony prefix for ConsoleUI.IS_CONSOLE_ENABLED property
        /// Allows admin/operator players to access console on dedicated servers
        /// </summary>
        private static bool ConsoleEnabledPrefix(ConsoleUI __instance, ref bool __result)
        {
            try
            {
                // On dedicated servers, allow console UI for all clients; server will enforce per-command permissions
                if (InstanceFinder.IsClient && !InstanceFinder.IsHost)
                {
                    __result = AdminStatusManager.CanOpenConsole();
                    return false; // Skip original method
                }

                // Let original method run for hosts or offline
                return true;
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error in ConsoleEnabled patch: {ex}");
                return true; // Let original method run as fallback
            }
        }

        /// <summary>
        /// Harmony prefix for ConsoleUI.SetIsOpen method
        /// Ensures admin players can open the console on dedicated servers
        /// </summary>
        private static bool ConsoleSetIsOpenPrefix(ConsoleUI __instance, bool open)
        {
            try
            {
                // On dedicated servers, allow console UI for all clients and manage UI state here
                if (InstanceFinder.IsClient && !InstanceFinder.IsHost)
                {
                    if (open && !AdminStatusManager.CanOpenConsole())
                    {
                        DebugLog.Warning("Console open rejected by latest permission snapshot");
                        return false;
                    }

                    if (open)
                    {
                        DebugLog.Info($"Opening console on dedicated server ({AdminStatusManager.GetPermissionInfo()})");
                    }
                    
                    var canvas = __instance.canvas;
                    var container = __instance.Container;
                    var inputField = __instance.InputField;
                    
                    if (canvas != null && container != null && inputField != null)
                    {
                        canvas.enabled = open;
                        container.gameObject.SetActive(open);
                        inputField.SetTextWithoutNotify("");
                        
                        if (open)
                        {
                            PlayerSingleton<PlayerCamera>.Instance.AddActiveUIElement(__instance.name);
                            GameInput.IsTyping = true;
                            
                            // Focus the input field
                            MelonCoroutines.Start(FocusInputField(inputField));
                        }
                        else
                        {
                            PlayerSingleton<PlayerCamera>.Instance.RemoveActiveUIElement(__instance.name);
                            GameInput.IsTyping = false;
                        }
                    }
                    
                    return false; // Skip original method
                }

                // Let original method run for hosts or offline
                return true;
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error in ConsoleSetIsOpen patch: {ex}");
                return true; // Let original method run as fallback
            }
        }

        /// <summary>
        /// Harmony prefix for Console.SubmitCommand (string version)
        /// Provides client-side command validation for admin players
        /// </summary>
        private static bool ConsoleSubmitCommandPrefix(string args)
        {
            try
            {
                // On dedicated servers, route all commands to server for centralized validation and execution
                if (InstanceFinder.IsClient && !InstanceFinder.IsHost)
                {
                    string commandWord = ExtractCommandWord(args);
                    if (!string.IsNullOrWhiteSpace(commandWord) && !AdminStatusManager.CanUseCommand(commandWord))
                    {
                        ScheduleOne.Console.LogCommandError($"You do not have permission to use '{commandWord}'.");
                        return false;
                    }

                    DebugLog.Info($"Submitting command to server: {args}");
                    CustomMessaging.SendToServer("admin_console", args);
                    return false; // Prevent local execution to avoid duplication; server will handle and relay if needed
                }

                return true; // Let original method handle the command when hosting/offline
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error in ConsoleSubmitCommand patch: {ex}");
                return true; // Let original method run as fallback
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Coroutine to focus the input field after a frame delay
        /// </summary>
        private static System.Collections.IEnumerator FocusInputField(TMPInputField inputField)
        {
            yield return null;
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(inputField.gameObject);
        }

        private static string ExtractCommandWord(string rawInput)
        {
            if (string.IsNullOrWhiteSpace(rawInput))
            {
                return string.Empty;
            }

            string trimmed = rawInput.Trim();
            int firstWhitespace = trimmed.IndexOfAny(new[] { ' ', '\t' });
            return firstWhitespace >= 0
                ? trimmed.Substring(0, firstWhitespace).Trim().ToLowerInvariant()
                : trimmed.ToLowerInvariant();
        }

        /// <summary>
        /// Invalidate the admin status cache (call when player status might have changed)
        /// </summary>
        internal static void InvalidateAdminCache()
        {
            AdminStatusManager.InvalidateCache();
        }

        /// <summary>
        /// Clear admin cache (call when disconnecting)
        /// </summary>
        internal static void ClearAdminCache()
        {
            AdminStatusManager.ClearCache();
        }

        #endregion
    }
}
