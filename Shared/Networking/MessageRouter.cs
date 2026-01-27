using System;
using System.Collections.Generic;
using System.Reflection;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using MelonLoader;
using Newtonsoft.Json;
using ScheduleOne;
using ScheduleOne.Console;
using ScheduleOne.DevUtilities;
using ScheduleOne.PlayerScripts;
using ScheduleOne.UI;
using ScheduleOne.Vehicles;
using UnityEngine;

namespace DedicatedServerMod.Shared.Networking
{
    /// <summary>
    /// Routes custom messages to appropriate handlers.
    /// Separates networking concerns from business logic.
    /// </summary>
    /// <remarks>
    /// This class handles the routing and execution of custom messages received
    /// from clients on the server. It delegates to appropriate handlers based
    /// on the message command type.
    /// </remarks>
    public static class MessageRouter
    {
        /// <summary>
        /// The logger instance for this router.
        /// </summary>
        private static MelonLogger.Instance _logger;

        /// <summary>
        /// Initializes the message router with a logger.
        /// </summary>
        /// <param name="logger">The logger to use</param>
        public static void Initialize(MelonLogger.Instance logger)
        {
            _logger = logger;
        }

        #region Server Message Routing

        /// <summary>
        /// Routes a message received from a client to the appropriate handler.
        /// </summary>
        /// <param name="conn">The connection that sent the message</param>
        /// <param name="command">The message command type</param>
        /// <param name="data">The message payload</param>
        public static void RouteServerMessage(NetworkConnection conn, string command, string data)
        {
            if (conn == null)
            {
                _logger.Warning("RouteServerMessage: Connection is null");
                return;
            }

            _logger.Msg($"RouteServerMessage: cmd='{command}' from={conn.ClientId}");

            switch (command)
            {
                case "admin_console":
                    HandleAdminConsoleCommand(conn, data);
                    break;

                case "request_server_data":
                    HandleServerDataRequest(conn);
                    break;

                default:
                    _logger.Verbose($"Unhandled server message: {command}");
                    break;
            }
        }

        /// <summary>
        /// Routes a message received from the server to the appropriate client handler.
        /// </summary>
        /// <param name="command">The message command type</param>
        /// <param name="data">The message payload</param>
        public static void RouteClientMessage(string command, string data)
        {
            _logger.Verbose($"RouteClientMessage: cmd='{command}'");

            switch (command)
            {
                case "exec_console":
                    HandleClientConsoleCommand(data);
                    break;

                default:
                    _logger.Verbose($"Unhandled client message: {command}");
                    break;
            }
        }

        #endregion

        #region Admin Console Command Handling

        /// <summary>
        /// Handles an admin console command from a client.
        /// </summary>
        /// <param name="conn">The connection that sent the command</param>
        /// <param name="data">The command data</param>
        private static void HandleAdminConsoleCommand(NetworkConnection conn, string data)
        {
            try
            {
                var player = FindPlayerByConnection(conn);
                if (player == null)
                {
                    _logger.Warning("HandleAdminConsoleCommand: Could not find player for connection");
                    return;
                }

                // Permission check - can they use the console?
                if (!Permissions.PermissionManager.CanUseConsole(player))
                {
                    _logger.Warning($"HandleAdminConsoleCommand: Player {player.PlayerName} not permitted to use admin console.");
                    return;
                }

                var parts = new List<string>(data.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

                if (parts.Count == 0)
                {
                    _logger.Warning("HandleAdminConsoleCommand: No command parts found");
                    return;
                }

                string cmd = parts[0].ToLower();

                // Permission check - can they use this specific command?
                if (!Permissions.PermissionManager.CanUseCommand(player, cmd))
                {
                    _logger.Warning($"HandleAdminConsoleCommand: Player {player.PlayerName} not permitted to run '{cmd}'.");
                    return;
                }

                // Handle server-authoritative commands
                if (cmd == "spawnvehicle")
                {
                    HandleSpawnVehicleCommand(player, parts);
                    return;
                }

                // Execute other commands via console (relay to client for Player.Local context)
                ExecuteConsoleCommandRelay(player, cmd, parts);
            }
            catch (Exception ex)
            {
                _logger.Error($"HandleAdminConsoleCommand: Error executing admin console command: {ex}");
            }
        }

        /// <summary>
        /// Handles the spawnvehicle command server-side for proper ownership.
        /// </summary>
        private static void HandleSpawnVehicleCommand(Player player, List<string> parts)
        {
            try
            {
                parts.RemoveAt(0); // Remove command name
                if (parts.Count == 0)
                {
                    LogCommandError("Unrecognized command format. Correct format example(s): 'spawnvehicle shitbox'");
                    return;
                }

                string vehicleCode = parts[0].ToLower();
                var vm = NetworkSingleton<VehicleManager>.Instance;
                if (vm == null)
                {
                    _logger.Error("HandleSpawnVehicleCommand: VehicleManager instance not found on server");
                    return;
                }

                if (vm.GetVehiclePrefab(vehicleCode) == null)
                {
                    LogCommandError($"Unrecognized vehicle code '{vehicleCode}'");
                    return;
                }

                Vector3 position = player.transform.position + player.transform.forward * 4f + player.transform.up * 1f;
                Quaternion rotation = player.transform.rotation;
                var spawned = vm.SpawnAndReturnVehicle(vehicleCode, position, rotation, playerOwned: true);

                if (spawned != null)
                {
                    try
                    {
                        spawned.NetworkObject.GiveOwnership(player.Owner);
                    }
                    catch { }

                    _logger.Msg($"Spawned vehicle '{vehicleCode}' for player {player.PlayerName}");
                }
                else
                {
                    _logger.Warning("HandleSpawnVehicleCommand: SpawnAndReturnVehicle returned null");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"HandleSpawnVehicleCommand: Error spawning vehicle server-side: {ex}");
            }
        }

        /// <summary>
        /// Relays a console command to the specific client so Player.Local refers to their player.
        /// </summary>
        private static void ExecuteConsoleCommandRelay(Player player, string cmd, List<string> parts)
        {
            try
            {
                var commandsField = typeof(Console).GetField("commands",
                    BindingFlags.NonPublic | BindingFlags.Static);
                var commands = commandsField?.GetValue(null) as Dictionary<string, Console.ConsoleCommand>;

                if (commands == null)
                {
                    _logger.Error("ExecuteConsoleCommandRelay: Could not access Console.commands field");
                    return;
                }

                // Ensure core commands exist
                if (!commands.ContainsKey("settime") || !commands.ContainsKey("give"))
                {
                    EnsureCoreCommandsExist(commands);
                }

                if (commands.TryGetValue(cmd, out var handler))
                {
                    parts.RemoveAt(0); // Remove command name
                    string argsString = string.Join(" ", parts);

                    // Relay to the specific client's context
                    var payload = string.IsNullOrEmpty(argsString) ? cmd : $"{cmd} {argsString}";
                    CustomMessaging.SendToClient(player.Owner, "exec_console", payload);

                    // Log admin action
                    Permissions.PlayerResolver.LogAdminAction(player, cmd, argsString);
                }
                else
                {
                    _logger.Warning($"ExecuteConsoleCommandRelay: Command '{cmd}' not found in available commands");
                    LogCommandError($"Command '{cmd}' not found.");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"ExecuteConsoleCommandRelay: Error: {ex}");
            }
        }

        #endregion

        #region Client Console Command Handling

        /// <summary>
        /// Executes a console command on the client.
        /// </summary>
        /// <param name="data">The command data</param>
        private static void HandleClientConsoleCommand(string data)
        {
            try
            {
                var parts = new List<string>(data.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                if (parts.Count == 0)
                {
                    _logger.Warning("HandleClientConsoleCommand: No command parts found");
                    return;
                }

                string cmd = parts[0].ToLower();
                parts.RemoveAt(0);

                var commandsField = typeof(Console).GetField("commands",
                    BindingFlags.NonPublic | BindingFlags.Static);
                var commands = commandsField?.GetValue(null) as Dictionary<string, Console.ConsoleCommand>;

                if (commands == null)
                {
                    _logger.Error("HandleClientConsoleCommand: Could not access Console.commands on client");
                    return;
                }

                if (!commands.ContainsKey(cmd))
                {
                    _logger.Warning($"HandleClientConsoleCommand: Command '{cmd}' not found on client");
                    Console.LogCommandError($"Command '{cmd}' not found.");
                    return;
                }

                commands[cmd].Execute(parts);
            }
            catch (Exception ex)
            {
                _logger.Error($"HandleClientConsoleCommand error: {ex}");
            }
        }

        #endregion

        #region Server Data Request Handling

        /// <summary>
        /// Handles a request for server data from a client.
        /// </summary>
        /// <param name="conn">The requesting connection</param>
        private static void HandleServerDataRequest(NetworkConnection conn)
        {
            try
            {
                var config = Shared.Configuration.ServerConfig.Instance;
                var serverData = new ServerData
                {
                    ServerName = config.ServerName,
                    AllowSleeping = config.AllowSleeping,
                    TimeNeverStops = config.TimeNeverStops,
                    PublicServer = config.PublicServer
                };

                string payload = JsonConvert.SerializeObject(serverData);
                CustomMessaging.SendToClient(conn, "server_data", payload);
            }
            catch (Exception ex)
            {
                _logger.Error($"HandleServerDataRequest: Error: {ex}");
            }
        }

        #endregion

        #region Console Command Initialization

        /// <summary>
        /// Ensures core console commands are registered (critical for dedicated servers).
        /// </summary>
        private static void EnsureCoreCommandsExist(Dictionary<string, Console.ConsoleCommand> commands)
        {
            try
            {
                // Add missing core commands
                if (!commands.ContainsKey("freecam"))
                    commands.Add("freecam", new FreeCamCommand());
                if (!commands.ContainsKey("save"))
                    commands.Add("save", new Save());
                if (!commands.ContainsKey("settime"))
                    commands.Add("settime", new SetTimeCommand());
                if (!commands.ContainsKey("give"))
                    commands.Add("give", new AddItemToInventoryCommand());
                if (!commands.ContainsKey("clearinventory"))
                    commands.Add("clearinventory", new ClearInventoryCommand());
                if (!commands.ContainsKey("changecash"))
                    commands.Add("changecash", new ChangeCashCommand());
                if (!commands.ContainsKey("changebalance"))
                    commands.Add("changebalance", new ChangeOnlineBalanceCommand());
                if (!commands.ContainsKey("addxp"))
                    commands.Add("addxp", new GiveXP());
                if (!commands.ContainsKey("spawnvehicle"))
                    commands.Add("spawnvehicle", new SpawnVehicleCommand());
                if (!commands.ContainsKey("setmovespeed"))
                    commands.Add("setmovespeed", new SetMoveSpeedCommand());
                if (!commands.ContainsKey("setjumpforce"))
                    commands.Add("setjumpforce", new SetJumpMultiplier());
                if (!commands.ContainsKey("teleport"))
                    commands.Add("teleport", new Teleport());
                if (!commands.ContainsKey("setowned"))
                    commands.Add("setowned", new SetPropertyOwned());
                if (!commands.ContainsKey("packageproduct"))
                    commands.Add("packageproduct", new PackageProduct());
                if (!commands.ContainsKey("setstaminareserve"))
                    commands.Add("setstaminareserve", new SetStaminaReserve());
                if (!commands.ContainsKey("raisewanted"))
                    commands.Add("raisewanted", new RaisedWanted());
                if (!commands.ContainsKey("lowerwanted"))
                    commands.Add("lowerwanted", new LowerWanted());
                if (!commands.ContainsKey("clearwanted"))
                    commands.Add("clearwanted", new ClearWanted());
                if (!commands.ContainsKey("sethealth"))
                    commands.Add("sethealth", new SetHealth());
                if (!commands.ContainsKey("settimescale"))
                    commands.Add("settimescale", new SetTimeScale());
                if (!commands.ContainsKey("setvar"))
                    commands.Add("setvar", new SetVariableValue());
                if (!commands.ContainsKey("setqueststate"))
                    commands.Add("setqueststate", new SetQuestState());
                if (!commands.ContainsKey("setquestentrystate"))
                    commands.Add("setquestentrystate", new SetQuestEntryState());
                if (!commands.ContainsKey("setemotion"))
                    commands.Add("setemotion", new SetEmotion());
                if (!commands.ContainsKey("setunlocked"))
                    commands.Add("setunlocked", new SetUnlocked());
                if (!commands.ContainsKey("setrelationship"))
                    commands.Add("setrelationship", new SetRelationship());
                if (!commands.ContainsKey("addemployee"))
                    commands.Add("addemployee", new AddEmployeeCommand());
                if (!commands.ContainsKey("setdiscovered"))
                    commands.Add("setdiscovered", new SetDiscovered());
                if (!commands.ContainsKey("growplants"))
                    commands.Add("growplants", new GrowPlants());
                if (!commands.ContainsKey("setlawintensity"))
                    commands.Add("setlawintensity", new SetLawIntensity());
                if (!commands.ContainsKey("setquality"))
                    commands.Add("setquality", new SetQuality());
                if (!commands.ContainsKey("bind"))
                    commands.Add("bind", new Bind());
                if (!commands.ContainsKey("unbind"))
                    commands.Add("unbind", new Unbind());
                if (!commands.ContainsKey("clearbinds"))
                    commands.Add("clearbinds", new ClearBinds());
                if (!commands.ContainsKey("hideui"))
                    commands.Add("hideui", new HideUI());
                if (!commands.ContainsKey("disable"))
                    commands.Add("disable", new Disable());
                if (!commands.ContainsKey("enable"))
                    commands.Add("enable", new Enable());
                if (!commands.ContainsKey("endtutorial"))
                    commands.Add("endtutorial", new EndTutorial());
                if (!commands.ContainsKey("disablenpcasset"))
                    commands.Add("disablenpcasset", new DisableNPCAsset());
                if (!commands.ContainsKey("showfps"))
                    commands.Add("showfps", new ShowFPS());
                if (!commands.ContainsKey("hidefps"))
                    commands.Add("hidefps", new HideFPS());
                if (!commands.ContainsKey("cleartrash"))
                    commands.Add("cleartrash", new ClearTrash());
            }
            catch (Exception ex)
            {
                _logger.Error($"EnsureCoreCommandsExist: Error: {ex}");
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Finds a player by their network connection.
        /// </summary>
        private static Player FindPlayerByConnection(NetworkConnection conn)
        {
            if (conn == null)
            {
                _logger.Warning("FindPlayerByConnection: Connection is null");
                return null;
            }

            foreach (var p in Player.PlayerList)
            {
                if (p?.Owner == conn)
                    return p;
            }

            _logger.Warning($"FindPlayerByConnection: No player found for connection {conn.ClientId}");
            return null;
        }

        /// <summary>
        /// Logs a command error to the player's console.
        /// </summary>
        private static void LogCommandError(string message)
        {
            try
            {
                Console.LogCommandError(message);
            }
            catch
            {
                _logger.Warning(message);
            }
        }

        #endregion

        #region Data Types

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
