using System;
using FishNet.Object.Delegating;
using FishNet.Object.Hosting;
using FishNet.Serializing;
using FishNet.Transporting;
using HarmonyLib;
using MelonLoader;
using ScheduleOne;
using ScheduleOne.DevUtilities;

namespace DedicatedServerMod.Client.Patches
{
    /// <summary>
    /// Harmony patches for the custom messaging system.
    /// Registers custom RPC handlers on the DailySummary NetworkBehaviour.
    /// </summary>
    /// <remarks>
    /// This class handles the registration of custom RPC methods for
    /// bidirectional communication between server and client mods.
    /// </remarks>
    internal static class MessagingPatches
    {
        /// <summary>
        /// The logger instance for this patch class.
        /// </summary>
        private static MelonLogger.Instance _logger;

        /// <summary>
        /// The custom message ID used for RPC registration.
        /// </summary>
        private const uint MESSAGE_ID = 105u;

        #region Initialization

        /// <summary>
        /// Applies messaging-related Harmony patches.
        /// </summary>
        /// <param name="harmony">The Harmony instance to patch with</param>
        /// <param name="logger">The logger instance to use</param>
        public static void ApplyPatches(Harmony harmony, MelonLogger.Instance logger)
        {
            _logger = logger;

            // Patch DailySummary.Awake to register RPC handlers
            PatchDailySummaryAwake(harmony);

            _logger.Msg("Messaging patches applied successfully");
        }

        /// <summary>
        /// Removes messaging-related Harmony patches.
        /// </summary>
        /// <param name="harmony">The Harmony instance to unpatch</param>
        public static void RemovePatches(Harmony harmony)
        {
            UnpatchDailySummaryAwake(harmony);
            _logger.Msg("Messaging patches removed");
        }

        #endregion

        #region DailySummary.Awake Patch

        /// <summary>
        /// Patches DailySummary.Awake to register custom RPC handlers.
        /// </summary>
        private static void PatchDailySummaryAwake(Harmony harmony)
        {
            try
            {
                var dailySummaryType = typeof(DailySummary);
                var awakeMethod = dailySummaryType.GetMethod("Awake",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

                if (awakeMethod != null)
                {
                    var postfix = typeof(MessagingPatches).GetMethod(
                        nameof(DailySummaryAwakePostfix),
                        BindingFlags.Static | BindingFlags.NonPublic);

                    harmony.Patch(awakeMethod, postfix: new HarmonyMethod(postfix));
                    _logger.Msg("Patched DailySummary.Awake to register custom messaging RPCs");
                }
                else
                {
                    _logger.Warning("Could not find DailySummary.Awake method to patch");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to patch DailySummary.Awake: {ex}");
            }
        }

        /// <summary>
        /// Unpatches DailySummary.Awake.
        /// </summary>
        private static void UnpatchDailySummaryAwake(Harmony harmony)
        {
            try
            {
                var dailySummaryType = typeof(DailySummary);
                var awakeMethod = dailySummaryType.GetMethod("Awake",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

                if (awakeMethod != null)
                {
                    harmony.Unpatch(awakeMethod, HarmonyPatchType.Postfix);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to unpatch DailySummary.Awake: {ex}");
            }
        }

        /// <summary>
        /// Harmony postfix patch for DailySummary.Awake.
        /// Registers custom RPC handlers for the messaging system.
        /// </summary>
        /// <param name="__instance">The DailySummary instance being initialized</param>
        private static void DailySummaryAwakePostfix(DailySummary __instance)
        {
            try
            {
                var nb = (NetworkBehaviour)__instance;

                // Register server -> client Target RPC
                nb.RegisterTargetRpc(MESSAGE_ID, new ClientRpcDelegate(OnClientMessageReceived));

                // Register client -> server Server RPC
                nb.RegisterServerRpc(MESSAGE_ID, new ServerRpcDelegate(OnServerMessageReceived));

                _logger.Msg("Registered custom messaging RPCs on DailySummary");
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to register custom RPCs: {ex}");
            }
        }

        #endregion

        #region Message Handlers

        /// <summary>
        /// Handles custom messages received from the server.
        /// </summary>
        /// <param name="reader">The message data reader</param>
        /// <param name="channel">The transport channel</param>
        private static void OnClientMessageReceived(PooledReader reader, Channel channel)
        {
            try
            {
                string raw = ((Reader)reader).ReadString();
                var message = Newtonsoft.Json.JsonConvert.DeserializeObject<CustomMessage>(raw);

                if (message?.command == null)
                {
                    _logger.Warning("OnClientMessageReceived: Message command is null");
                    return;
                }

                _logger.Msg($"OnClientMessageReceived cmd='{message.command}' len={message.data?.Length ?? 0}");

                // Raise API event for mods
                RaiseClientMessageEvent(message.command, message.data);

                // Handle built-in routing
                HandleClientMessage(message.command, message.data);
            }
            catch (Exception ex)
            {
                _logger.Error($"OnClientMessageReceived error: {ex}");
            }
        }

        /// <summary>
        /// Handles custom messages sent to the server.
        /// </summary>
        /// <param name="reader">The message data reader</param>
        /// <param name="channel">The transport channel</param>
        /// <param name="conn">The sending connection</param>
        private static void OnServerMessageReceived(PooledReader reader, Channel channel, FishNet.Connection.NetworkConnection conn)
        {
            try
            {
                string raw = ((Reader)reader).ReadString();
                var message = Newtonsoft.Json.JsonConvert.DeserializeObject<CustomMessage>(raw);

                if (message?.command == null)
                {
                    _logger.Warning("OnServerMessageReceived: Message command is null");
                    return;
                }

                _logger.Msg($"OnServerMessageReceived cmd='{message.command}' len={message.data?.Length ?? 0} from={conn?.ClientId}");

                // Raise API event for server mods
                RaiseServerMessageEvent(conn, message.command, message.data);

                // Handle built-in routing (server-side)
                // Note: Server-side routing is handled by Shared.Networking.MessageRouter
            }
            catch (Exception ex)
            {
                _logger.Error($"OnServerMessageReceived error: {ex}");
            }
        }

        #endregion

        #region Message Routing

        /// <summary>
        /// Handles built-in client-side message routing.
        /// </summary>
        /// <param name="command">The message command</param>
        /// <param name="data">The message data</param>
        private static void HandleClientMessage(string command, string data)
        {
            try
            {
                switch (command)
                {
                    case "exec_console":
                        ExecuteClientConsoleCommand(data);
                        break;

                    case "server_data":
                        HandleServerData(data);
                        break;

                    case "welcome_message":
                        // Already logged by the message system
                        break;

                    default:
                        _logger.Verbose($"Unhandled client message: {command}");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error handling client message: {ex}");
            }
        }

        /// <summary>
        /// Executes a console command sent from the server.
        /// </summary>
        /// <param name="data">The command data (command name + arguments)</param>
        private static void ExecuteClientConsoleCommand(string data)
        {
            try
            {
                var parts = new System.Collections.Generic.List<string>(
                    data.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

                if (parts.Count == 0)
                {
                    _logger.Warning("ExecuteClientConsoleCommand: No command parts found");
                    return;
                }

                string cmd = parts[0].ToLower();
                parts.RemoveAt(0);

                // Access the console commands registry
                var commandsField = typeof(Console).GetField("commands",
                    BindingFlags.NonPublic | BindingFlags.Static);

                var commands = commandsField?.GetValue(null) as System.Collections.Generic.Dictionary<string, Console.ConsoleCommand>;

                if (commands == null)
                {
                    _logger.Error("ExecuteClientConsoleCommand: Could not access Console.commands on client");
                    return;
                }

                if (!commands.ContainsKey(cmd))
                {
                    _logger.Warning($"ExecuteClientConsoleCommand: Command '{cmd}' not found on client");
                    Console.LogCommandError($"Command '{cmd}' not found.");
                    return;
                }

                commands[cmd].Execute(parts);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error executing client console command: {ex}");
            }
        }

        /// <summary>
        /// Handles server data received from the server.
        /// </summary>
        /// <param name="data">The server data JSON</param>
        private static void HandleServerData(string data)
        {
            try
            {
                var serverData = Newtonsoft.Json.JsonConvert.DeserializeObject<ServerData>(data);
                if (serverData != null)
                {
                    Client.ServerDataStore.Update(serverData);
                    _logger.Msg($"Received server data: {serverData.ServerName}, AllowSleeping={serverData.AllowSleeping}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error handling server data: {ex}");
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Event raised when a custom message is received from the server.
        /// </summary>
        public static event System.Action<string, string> ClientMessageReceived;

        /// <summary>
        /// Event raised when a custom message is received from a client.
        /// </summary>
        public static event System.Action<FishNet.Connection.NetworkConnection, string, string> ServerMessageReceived;

        /// <summary>
        /// Raises the ClientMessageReceived event.
        /// </summary>
        private static void RaiseClientMessageEvent(string command, string data)
        {
            try
            {
                ClientMessageReceived?.Invoke(command, data);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error in ClientMessageReceived event: {ex}");
            }
        }

        /// <summary>
        /// Raises the ServerMessageReceived event.
        /// </summary>
        private static void RaiseServerMessageEvent(FishNet.Connection.NetworkConnection conn, string command, string data)
        {
            try
            {
                ServerMessageReceived?.Invoke(conn, command, data);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error in ServerMessageReceived event: {ex}");
            }
        }

        #endregion

        #region Helper Types

        /// <summary>
        /// Represents a custom message sent between server and client.
        /// </summary>
        private struct CustomMessage
        {
            public string command;
            public string data;
        }

        /// <summary>
        /// Represents server configuration data sent to clients.
        /// </summary>
        public struct ServerData
        {
            public string ServerName;
            public bool AllowSleeping;
            public bool TimeNeverStops;
            public bool PublicServer;
        }

        #endregion
    }
}
