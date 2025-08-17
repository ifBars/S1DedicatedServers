using System;
using System.Reflection;
using FishNet;
using HarmonyLib;
using MelonLoader;
using ScheduleOne;
using ScheduleOne.DevUtilities;
using ScheduleOne.PlayerScripts;
using ScheduleOne.UI;
using UnityEngine;

namespace DedicatedServerMod.Client
{
    /// <summary>
    /// Handles client-side console access for admin/operator players on dedicated servers.
    /// Patches ConsoleUI to allow admin players to open and use the console.
    /// </summary>
    public class ClientConsoleManager
    {
        private readonly MelonLogger.Instance logger;
        private HarmonyLib.Harmony harmony;
        private static MelonLogger.Instance _logger;

        public ClientConsoleManager(MelonLogger.Instance logger)
        {
            this.logger = logger;
            _logger = logger;
        }

        public void Initialize()
        {
            try
            {
                logger.Msg("Initializing ClientConsoleManager");
                
                // Initialize admin status manager
                AdminStatusManager.Initialize(logger);
                
                harmony = new HarmonyLib.Harmony("DedicatedServerMod.ClientConsoleManager");
                ApplyConsolePatches();
                
                logger.Msg("ClientConsoleManager initialized successfully");
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to initialize ClientConsoleManager: {ex}");
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
                
                logger.Msg("Console patches applied successfully");
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to apply console patches: {ex}");
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
                    BindingFlags.Static | BindingFlags.Public);
                harmony.Patch(consoleEnabledProperty.GetGetMethod(), new HarmonyMethod(prefixMethod));
                logger.Msg("Patched ConsoleUI.IS_CONSOLE_ENABLED");
            }
            else
            {
                logger.Error("Could not find ConsoleUI.IS_CONSOLE_ENABLED property");
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
                    BindingFlags.Static | BindingFlags.Public);
                harmony.Patch(setIsOpenMethod, new HarmonyMethod(prefixMethod));
                logger.Msg("Patched ConsoleUI.SetIsOpen");
            }
            else
            {
                logger.Error("Could not find ConsoleUI.SetIsOpen method");
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
                    BindingFlags.Static | BindingFlags.Public);
                harmony.Patch(submitCommandMethod, new HarmonyMethod(prefixMethod));
                logger.Msg("Patched Console.SubmitCommand for client validation");
            }
            else
            {
                logger.Error("Could not find Console.SubmitCommand method");
            }
        }

        #region Harmony Prefix Methods

        /// <summary>
        /// Harmony prefix for ConsoleUI.IS_CONSOLE_ENABLED property
        /// Allows admin/operator players to access console on dedicated servers
        /// </summary>
        public static bool ConsoleEnabledPrefix(ConsoleUI __instance, ref bool __result)
        {
            try
            {
                // Check if we're connected to a dedicated server (not host)
                if (InstanceFinder.IsClient && !InstanceFinder.IsHost)
                {
                    // Check if the local player is an admin/operator
                    if (AdminStatusManager.IsLocalPlayerAdmin())
                    {
                        __result = true;
                        return false; // Skip original method
                    }
                }

                // Let original method run for hosts or non-admin players
                return true;
            }
            catch (Exception ex)
            {
                _logger?.Error($"Error in ConsoleEnabled patch: {ex}");
                return true; // Let original method run as fallback
            }
        }

        /// <summary>
        /// Harmony prefix for ConsoleUI.SetIsOpen method
        /// Ensures admin players can open the console on dedicated servers
        /// </summary>
        public static bool ConsoleSetIsOpenPrefix(ConsoleUI __instance, bool open)
        {
            try
            {
                // Check if we're connected to a dedicated server (not host)
                if (InstanceFinder.IsClient && !InstanceFinder.IsHost)
                {
                    // Check if the local player is an admin/operator
                    if (AdminStatusManager.IsLocalPlayerAdmin())
                    {
                        // Allow console opening by modifying the original check
                        // We'll manually implement the SetIsOpen logic for admin players
                        if (open)
                        {
                            _logger?.Msg($"Admin player opening console on dedicated server ({AdminStatusManager.GetPermissionInfo()})");
                        }
                        
                        // Allow the original method to run, but ensure console settings are correct
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
                                // Set up console UI state for admin player
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
                }

                // Let original method run for hosts or non-admin players
                return true;
            }
            catch (Exception ex)
            {
                _logger?.Error($"Error in ConsoleSetIsOpen patch: {ex}");
                return true; // Let original method run as fallback
            }
        }

        /// <summary>
        /// Harmony prefix for Console.SubmitCommand (string version)
        /// Provides client-side command validation for admin players
        /// </summary>
        public static bool ConsoleSubmitCommandPrefix(string args)
        {
            try
            {
                // Only intervene on dedicated servers for admin players
                if (InstanceFinder.IsClient && !InstanceFinder.IsHost && AdminStatusManager.IsLocalPlayerAdmin())
                {
                    _logger?.Msg($"Admin player executing command: {args}");
                    
                    // Check if the command is allowed (basic client-side validation)
                    var argsList = new System.Collections.Generic.List<string>(
                        args.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                    
                    if (argsList.Count > 0)
                    {
                        string command = argsList[0].ToLower();
                        
                        // Use AdminStatusManager for command permission checking
                        if (!AdminStatusManager.CanUseCommand(command))
                        {
                            string permissionLevel = AdminStatusManager.IsLocalPlayerOperator() ? "operator" : "admin";
                            ScheduleOne.Console.LogWarning($"Command '{command}' requires higher privileges than {permissionLevel}");
                            return false; // Block the command
                        }
                    }
                }

                return true; // Let original method handle the command
            }
            catch (Exception ex)
            {
                _logger?.Error($"Error in ConsoleSubmitCommand patch: {ex}");
                return true; // Let original method run as fallback
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Coroutine to focus the input field after a frame delay
        /// </summary>
        private static System.Collections.IEnumerator FocusInputField(TMPro.TMP_InputField inputField)
        {
            yield return null;
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(inputField.gameObject);
        }

        /// <summary>
        /// Invalidate the admin status cache (call when player status might have changed)
        /// </summary>
        public static void InvalidateAdminCache()
        {
            AdminStatusManager.InvalidateCache();
        }

        /// <summary>
        /// Clear admin cache (call when disconnecting)
        /// </summary>
        public static void ClearAdminCache()
        {
            AdminStatusManager.ClearCache();
        }

        #endregion
    }
}
