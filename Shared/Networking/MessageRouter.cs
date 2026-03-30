using System.Reflection;
using DedicatedServerMod.Shared.Configuration;
#if SERVER
using DedicatedServerMod.Server.Commands;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Core;
using DedicatedServerMod.Server.Network;
using DedicatedServerMod.Server.Player;
using UnityEngine;
using ServerPlayerManager = DedicatedServerMod.Server.Player.PlayerManager;
using ServerPlayerInfo = DedicatedServerMod.Server.Player.ConnectedPlayerInfo;
#endif
using DedicatedServerMod.Utils;
#if IL2CPP
using Il2CppFishNet;
using Il2CppFishNet.Connection;
using Il2CppFishNet.Object;
using Il2CppFishNet.Serializing;
using Il2CppFishNet.Transporting;
#else
using FishNet.Connection;
#endif
using DedicatedServerMod.Shared.ConsoleSupport;
using DedicatedServerMod.Shared.ModVerification;
using ScheduleOne.DevUtilities;
using ScheduleOne.Vehicles;
#if IL2CPP
using Newtonsoft.Json;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.Vehicles;
#else
using Newtonsoft.Json;
using ScheduleOne.PlayerScripts;
#endif
#if IL2CPP
using Console = Il2CppScheduleOne.Console;
#else
using Console = ScheduleOne.Console;
#endif

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
        /// Initializes the message router with a logger.
        /// </summary>
        public static void Initialize()
        {
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
#if SERVER
            if (conn == null)
            {
                DebugLog.Warning("RouteServerMessage: Connection is null");
                return;
            }

            DebugLog.MessageRoutingDebug($"RouteServerMessage: cmd='{command}' from={conn.ClientId}");

            if (!IsCommandAllowedForConnection(conn, command))
            {
                DebugLog.Warning($"RouteServerMessage: rejecting unauthenticated command '{command}' from ClientId {conn.ClientId}");
                return;
            }

            switch (command)
            {
                case Constants.Messages.AuthHello:
                    HandleAuthHello(conn);
                    break;

                case Constants.Messages.AuthTicket:
                    HandleAuthTicket(conn, data);
                    break;

                case Constants.Messages.ModVerifyReport:
                    HandleModVerificationReport(conn, data);
                    break;

                case Constants.Messages.AdminConsole:
                    HandleAdminConsoleCommand(conn, data);
                    break;

                case Constants.Messages.RequestServerData:
                    HandleServerDataRequest(conn);
                    break;

                case Constants.Messages.SnlDedicatedRegister:
                    HandleSteamNetworkLibRegister(conn);
                    break;

                case Constants.Messages.SnlDedicatedSetLobbyData:
                    HandleSteamNetworkLibSetLobbyData(conn, data);
                    break;

                case Constants.Messages.SnlDedicatedSetMemberData:
                    HandleSteamNetworkLibSetMemberData(conn, data);
                    break;

                case Constants.Messages.SnlDedicatedP2PSend:
                    HandleSteamNetworkLibP2PSend(conn, data);
                    break;

                default:
                    DebugLog.MessageRoutingDebug($"Unhandled server message: {command}");
                    break;
            }
#else
            DebugLog.MessageRoutingDebug($"RouteServerMessage ignored on client build: cmd='{command}'");
#endif
        }

        /// <summary>
        /// Routes a message received from the server to the appropriate client handler.
        /// </summary>
        /// <param name="command">The message command type</param>
        /// <param name="data">The message payload</param>
        public static void RouteClientMessage(string command, string data)
        {
            DebugLog.MessageRoutingDebug($"RouteClientMessage: cmd='{command}'");

            switch (command)
            {
                case Constants.Messages.ExecConsole:
                    HandleClientConsoleCommand(data);
                    break;

                case Constants.Messages.AuthChallenge:
                case Constants.Messages.AuthResult:
                case Constants.Messages.ModVerifyChallenge:
                case Constants.Messages.ModVerifyResult:
                case Constants.Messages.DisconnectNotice:
                case Constants.Messages.ServerData:
                case Constants.Messages.PermissionSnapshot:
                    // Handled by dedicated client managers via CustomMessaging events.
                    break;

                default:
                    DebugLog.MessageRoutingDebug($"Unhandled client message: {command}");
                    break;
            }
        }

        #endregion

        #region Authentication Message Handling

#if SERVER

        private static void HandleAuthHello(NetworkConnection conn)
        {
            try
            {
                ServerPlayerManager playerManager = ServerBootstrap.Players;
                ConnectedPlayerInfo playerInfo = playerManager?.GetPlayer(conn);
                if (playerInfo == null)
                {
                    DebugLog.Warning($"HandleAuthHello: no player tracked for ClientId {conn.ClientId}");
                    return;
                }

                AuthChallengeMessage challenge = playerManager.Authentication.CreateChallenge(playerInfo);
                if (challenge != null)
                {
                    string payload = JsonConvert.SerializeObject(challenge);
                    CustomMessaging.SendToClientOrDeferUntilReady(conn, Constants.Messages.AuthChallenge, payload);
                    return;
                }

                if (playerInfo.IsAuthenticated)
                {
                    var result = new AuthResultMessage
                    {
                        Success = true,
                        Message = "Authentication already satisfied",
                        SteamId = playerInfo.AuthenticatedSteamId ?? playerInfo.SteamId ?? string.Empty
                    };

                    CustomMessaging.SendToClientOrDeferUntilReady(conn, Constants.Messages.AuthResult, JsonConvert.SerializeObject(result));
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error($"HandleAuthHello threw an exception", ex);
            }
        }

        private static void HandleAuthTicket(NetworkConnection conn, string data)
        {
            try
            {
                ServerPlayerManager playerManager = ServerBootstrap.Players;
                ConnectedPlayerInfo playerInfo = playerManager?.GetPlayer(conn);
                if (playerInfo == null)
                {
                    DebugLog.Warning($"HandleAuthTicket: no player tracked for ClientId {conn.ClientId}");
                    return;
                }

                AuthTicketMessage ticketMessage;
                try
                {
                    ticketMessage = JsonConvert.DeserializeObject<AuthTicketMessage>(data ?? string.Empty);
                }
                catch (JsonException ex)
                {
                    DebugLog.Error($"HandleAuthTicket: invalid payload from ClientId {conn.ClientId}", ex);
                    conn.Disconnect(true);
                    return;
                }

                AuthenticationResult beginResult = playerManager.Authentication.SubmitTicket(playerInfo, ticketMessage);
                if (beginResult.IsPending)
                {
                    DebugLog.MessageRoutingDebug($"HandleAuthTicket: pending auth validation for ClientId {conn.ClientId}");
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error("HandleAuthTicket threw an exception", ex);
            }
        }

        private static bool IsCommandAllowedForConnection(NetworkConnection conn, string command)
        {
            if (string.Equals(command, Constants.Messages.AuthHello, StringComparison.Ordinal) ||
                string.Equals(command, Constants.Messages.AuthTicket, StringComparison.Ordinal))
            {
                return true;
            }

            ServerPlayerManager playerManager = ServerBootstrap.Players;
            ConnectedPlayerInfo playerInfo = playerManager?.GetPlayer(conn);
            if (playerInfo == null)
            {
                return false;
            }

            bool authenticationRequired = ServerConfig.Instance.AuthenticationEnabled &&
                                          !playerManager.Authentication.ShouldBypassAuthentication(playerInfo);
            if (authenticationRequired && !playerInfo.IsAuthenticated)
            {
                return false;
            }

            if (string.Equals(command, Constants.Messages.ModVerifyReport, StringComparison.Ordinal))
            {
                return !playerInfo.IsModVerificationComplete;
            }

            bool verificationRequired = playerManager.ModVerification.IsVerificationRequiredForPlayer(playerInfo);
            if (!verificationRequired)
            {
                return true;
            }

            return playerInfo.IsModVerificationComplete;
        }

        private static void HandleModVerificationReport(NetworkConnection conn, string data)
        {
            try
            {
                ServerPlayerManager playerManager = ServerBootstrap.Players;
                ConnectedPlayerInfo playerInfo = playerManager?.GetPlayer(conn);
                if (playerInfo == null)
                {
                    DebugLog.Warning($"HandleModVerificationReport: no player tracked for ClientId {conn.ClientId}");
                    return;
                }

                ModVerificationReportMessage reportMessage;
                try
                {
                    reportMessage = JsonConvert.DeserializeObject<ModVerificationReportMessage>(data ?? string.Empty);
                }
                catch (JsonException ex)
                {
                    DebugLog.Error($"HandleModVerificationReport: invalid payload from ClientId {conn.ClientId}", ex);
                    playerManager.NotifyAndDisconnectPlayer(playerInfo, "Verification Failed", "Client mod verification payload was invalid.");
                    return;
                }

                playerManager.ModVerification.SubmitReport(playerInfo, reportMessage);
            }
            catch (Exception ex)
            {
                DebugLog.Error($"HandleModVerificationReport threw an exception", ex);
            }
        }

#endif

        #endregion

        #region Admin Console Command Handling

#if SERVER

        private static void HandleSteamNetworkLibRegister(NetworkConnection conn)
        {
            try
            {
                SteamNetworkLibCompatService compat = ServerBootstrap.SteamNetworkLibCompat;
                compat?.HandleRegister(conn);
            }
            catch (Exception ex)
            {
                DebugLog.Error($"HandleSteamNetworkLibRegister threw an exception", ex);
            }
        }

        private static void HandleSteamNetworkLibSetLobbyData(NetworkConnection conn, string data)
        {
            try
            {
                SteamNetworkLibCompatService compat = ServerBootstrap.SteamNetworkLibCompat;
                compat?.HandleSetLobbyData(conn, data);
            }
            catch (Exception ex)
            {
                DebugLog.Error($"HandleSteamNetworkLibSetLobbyData threw an exception", ex);
            }
        }

        private static void HandleSteamNetworkLibSetMemberData(NetworkConnection conn, string data)
        {
            try
            {
                SteamNetworkLibCompatService compat = ServerBootstrap.SteamNetworkLibCompat;
                compat?.HandleSetMemberData(conn, data);
            }
            catch (Exception ex)
            {
                DebugLog.Error($"HandleSteamNetworkLibSetMemberData threw an exception", ex);
            }
        }

        private static void HandleSteamNetworkLibP2PSend(NetworkConnection conn, string data)
        {
            try
            {
                SteamNetworkLibCompatService compat = ServerBootstrap.SteamNetworkLibCompat;
                compat?.HandleP2PSend(conn, data);
            }
            catch (Exception ex)
            {
                DebugLog.Error($"HandleSteamNetworkLibP2PSend threw an exception", ex);
            }
        }

        /// <summary>
        /// Handles an admin console command from a client.
        /// </summary>
        /// <param name="conn">The connection that sent the command</param>
        /// <param name="data">The command data</param>
        private static void HandleAdminConsoleCommand(NetworkConnection conn, string data)
        {
            try
            {
                ServerPlayerInfo playerInfo = ServerBootstrap.Players?.GetPlayer(conn);
                var player = FindPlayerByConnection(conn);
                if (player == null || playerInfo == null)
                {
                    DebugLog.Warning("HandleAdminConsoleCommand: Could not find player for connection");
                    return;
                }

                if (ServerBootstrap.Permissions?.CanOpenConsole(playerInfo.TrustedUniqueId) != true)
                {
                    DebugLog.Warning($"HandleAdminConsoleCommand: Player {playerInfo.DisplayName} not permitted to use admin console.");
                    return;
                }

                CommandLineParseResult parseResult = CommandLineParser.TryParse(data);
                if (parseResult.IsEmpty)
                {
                    DebugLog.Warning("HandleAdminConsoleCommand: No command parts found");
                    return;
                }

                if (!parseResult.Success)
                {
                    DebugLog.Warning($"HandleAdminConsoleCommand: Parse error: {parseResult.ErrorMessage}");
                    LogCommandError(parseResult.ErrorMessage);
                    return;
                }

                ParsedCommandLine parsedCommand = parseResult.CommandLine;
                string cmd = parsedCommand.CommandWord;

                if (ServerBootstrap.Commands?.GetCommand(cmd) != null)
                {
                    CommandExecutionResult result = ServerBootstrap.Commands.ExecuteConsoleLine(parsedCommand, output: null, executor: playerInfo);
                    ServerBootstrap.Permissions?.LogCommand(playerInfo, cmd, result.Succeeded, result.Message);

                    if (!result.Succeeded && !string.IsNullOrWhiteSpace(result.Message))
                    {
                        LogCommandError(result.Message);
                    }

                    return;
                }

                if (ServerBootstrap.Permissions?.CanExecuteRemoteConsoleCommand(playerInfo.TrustedUniqueId, cmd) != true)
                {
                    DebugLog.Warning($"HandleAdminConsoleCommand: Player {playerInfo.DisplayName} not permitted to run remote console command '{cmd}'.");
                    return;
                }

                if (cmd == "spawnvehicle")
                {
                    HandleSpawnVehicleCommand(player, parsedCommand);
                    ServerBootstrap.Permissions?.LogCommand(playerInfo, cmd, succeeded: true, "spawnvehicle");
                    return;
                }

                ExecuteConsoleCommandRelay(playerInfo, player, parsedCommand);
            }
            catch (Exception ex)
            {
                DebugLog.Error("HandleAdminConsoleCommand: Error executing admin console command", ex);
            }
        }

        /// <summary>
        /// Handles the spawnvehicle command server-side for proper ownership.
        /// </summary>
        private static void HandleSpawnVehicleCommand(Player player, ParsedCommandLine parsedCommand)
        {
            try
            {
                if (parsedCommand.Arguments.Count == 0)
                {
                    LogCommandError("Unrecognized command format. Correct format example(s): 'spawnvehicle shitbox'");
                    return;
                }

                string vehicleCode = parsedCommand.Arguments[0].ToLowerInvariant();
                var vm = NetworkSingleton<VehicleManager>.Instance;
                if (vm == null)
                {
                    DebugLog.Error("HandleSpawnVehicleCommand: VehicleManager instance not found on server");
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
                    DebugLog.MessageRoutingDebug($"Spawned vehicle '{vehicleCode}' for player {player.PlayerName}");
                }
                else
                {
                    DebugLog.Warning("HandleSpawnVehicleCommand: SpawnAndReturnVehicle returned null");
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error($"HandleSpawnVehicleCommand: Error spawning vehicle server-side: {ex}");
            }
        }
#endif

        /// <summary>
        /// Relays a console command to the specific client so Player.Local refers to their player.
        /// </summary>
        private static void ExecuteConsoleCommandRelay(
#if SERVER
            ServerPlayerInfo playerInfo,
#else
            object playerInfo,
#endif
            Player player,
            ParsedCommandLine parsedCommand)
        {
            try
            {
                var commandsField = typeof(Console).GetField("commands",
                    BindingFlags.NonPublic | BindingFlags.Static);
                var commands = commandsField?.GetValue(null) as Dictionary<string, Console.ConsoleCommand>;

                if (commands == null)
                {
                    DebugLog.Error("ExecuteConsoleCommandRelay: Could not access Console.commands field");
                    return;
                }

                // Ensure core commands exist
                if (!commands.ContainsKey("settime") || !commands.ContainsKey("give"))
                {
                    EnsureCoreCommandsExist(commands);
                }

                if (commands.TryGetValue(parsedCommand.CommandWord, out var handler))
                {
                    // Relay to the specific client's context
                    string payload = CommandLineParser.BuildLine(parsedCommand);
                    CustomMessaging.SendToClient(player.Owner, "exec_console", payload);
#if SERVER
                    ServerBootstrap.Permissions?.LogCommand(playerInfo, parsedCommand.CommandWord, succeeded: true, string.Join(" ", parsedCommand.Arguments));
#endif
                }
                else
                {
                    DebugLog.Warning($"ExecuteConsoleCommandRelay: Command '{parsedCommand.CommandWord}' not found in available commands");
                    LogCommandError($"Command '{parsedCommand.CommandWord}' not found.");
#if SERVER
                    ServerBootstrap.Permissions?.LogCommand(playerInfo, parsedCommand.CommandWord, succeeded: false, "command_not_found");
#endif
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error("ExecuteConsoleCommandRelay threw an exception", ex);
#if SERVER
                ServerBootstrap.Permissions?.LogCommand(playerInfo, parsedCommand.CommandWord, succeeded: false, ex.Message);
#endif
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
                CommandLineParseResult parseResult = CommandLineParser.TryParse(data);
                if (parseResult.IsEmpty)
                {
                    DebugLog.Warning("HandleClientConsoleCommand: No command parts found");
                    return;
                }

                if (!parseResult.Success)
                {
                    DebugLog.Warning($"HandleClientConsoleCommand: Parse error: {parseResult.ErrorMessage}");
                    Console.LogCommandError(parseResult.ErrorMessage);
                    return;
                }

                ParsedCommandLine parsedCommand = parseResult.CommandLine;
                string cmd = parsedCommand.CommandWord;

                var commandsField = typeof(Console).GetField("commands",
                    BindingFlags.NonPublic | BindingFlags.Static);
                var commands = commandsField?.GetValue(null) as Dictionary<string, Console.ConsoleCommand>;

                if (commands == null)
                {
                    DebugLog.Error("HandleClientConsoleCommand: Could not access Console.commands on client");
                    return;
                }

                if (!commands.ContainsKey(cmd))
                {
                    DebugLog.Warning($"HandleClientConsoleCommand: Command '{cmd}' not found on client");
                    Console.LogCommandError($"Command '{cmd}' not found.");
                    return;
                }

#if IL2CPP
                var il2cppArgs = new Il2CppSystem.Collections.Generic.List<string>();
                for (int i = 0; i < parsedCommand.Arguments.Count; i++)
                {
                    il2cppArgs.Add(parsedCommand.Arguments[i]);
                }

                commands[cmd].Execute(il2cppArgs);
#else
                commands[cmd].Execute(new List<string>(parsedCommand.Arguments));
#endif
            }
            catch (Exception ex)
            {
                DebugLog.Error("HandleClientConsoleCommand error", ex);
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
                var config = ServerConfig.Instance;
                var serverData = new DedicatedServerMod.Shared.ServerData
                {
                    ServerName = config.ServerName,
                    ServerDescription = config.ServerDescription,
#if SERVER
                    CurrentPlayers = ServerBootstrap.Players?.GetVisiblePlayerCount() ?? 0,
#else
                    CurrentPlayers = 0,
#endif
                    MaxPlayers = config.MaxPlayers,
                    AllowSleeping = config.AllowSleeping
                };

                string payload = JsonConvert.SerializeObject(serverData);
                CustomMessaging.SendToClient(conn, Constants.Messages.ServerData, payload);
            }
            catch (Exception ex)
            {
                DebugLog.Error($"HandleServerDataRequest threw an exception", ex);
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
                    commands.Add("freecam", new Console.FreeCamCommand());
                if (!commands.ContainsKey("save"))
                    commands.Add("save", new Console.Save());
                if (!commands.ContainsKey("settime"))
                    commands.Add("settime", new Console.SetTimeCommand());
                if (!commands.ContainsKey("give"))
                    commands.Add("give", new Console.AddItemToInventoryCommand());
                if (!commands.ContainsKey("clearinventory"))
                    commands.Add("clearinventory", new Console.ClearInventoryCommand());
                if (!commands.ContainsKey("changecash"))
                    commands.Add("changecash", new Console.ChangeCashCommand());
                if (!commands.ContainsKey("changebalance"))
                    commands.Add("changebalance", new Console.ChangeOnlineBalanceCommand());
                if (!commands.ContainsKey("addxp"))
                    commands.Add("addxp", new Console.GiveXP());
                if (!commands.ContainsKey("spawnvehicle"))
                    commands.Add("spawnvehicle", new Console.SpawnVehicleCommand());
                if (!commands.ContainsKey("setmovespeed"))
                    commands.Add("setmovespeed", new Console.SetMoveSpeedCommand());
                if (!commands.ContainsKey("setjumpforce"))
                    commands.Add("setjumpforce", new Console.SetJumpMultiplier());
                if (!commands.ContainsKey("teleport"))
                    commands.Add("teleport", new Console.Teleport());
                if (!commands.ContainsKey("setowned"))
                    commands.Add("setowned", new Console.SetPropertyOwned());
                if (!commands.ContainsKey("packageproduct"))
                    commands.Add("packageproduct", new Console.PackageProduct());
                if (!commands.ContainsKey("setstaminareserve"))
                    commands.Add("setstaminareserve", new Console.SetStaminaReserve());
                if (!commands.ContainsKey("raisewanted"))
                    commands.Add("raisewanted", new Console.RaisedWanted());
                if (!commands.ContainsKey("lowerwanted"))
                    commands.Add("lowerwanted", new Console.LowerWanted());
                if (!commands.ContainsKey("clearwanted"))
                    commands.Add("clearwanted", new Console.ClearWanted());
                if (!commands.ContainsKey("sethealth"))
                    commands.Add("sethealth", new Console.SetHealth());
                if (!commands.ContainsKey("settimescale"))
                    commands.Add("settimescale", new Console.SetTimeScale());
                if (!commands.ContainsKey("setvar"))
                    commands.Add("setvar", new Console.SetVariableValue());
                if (!commands.ContainsKey("setqueststate"))
                    commands.Add("setqueststate", new Console.SetQuestState());
                if (!commands.ContainsKey("setquestentrystate"))
                    commands.Add("setquestentrystate", new Console.SetQuestEntryState());
                if (!commands.ContainsKey("setemotion"))
                    commands.Add("setemotion", new Console.SetEmotion());
                if (!commands.ContainsKey("setunlocked"))
                    commands.Add("setunlocked", new Console.SetUnlocked());
                if (!commands.ContainsKey("setrelationship"))
                    commands.Add("setrelationship", new Console.SetRelationship());
                if (!commands.ContainsKey("addemployee"))
                    commands.Add("addemployee", new Console.AddEmployeeCommand());
                if (!commands.ContainsKey("setdiscovered"))
                    commands.Add("setdiscovered", new Console.SetDiscovered());
                if (!commands.ContainsKey("growplants"))
                    commands.Add("growplants", new Console.GrowPlants());
                if (!commands.ContainsKey("setlawintensity"))
                    commands.Add("setlawintensity", new Console.SetLawIntensity());
                if (!commands.ContainsKey("setquality"))
                    commands.Add("setquality", new Console.SetQuality());
                if (!commands.ContainsKey("bind"))
                    commands.Add("bind", new Console.Bind());
                if (!commands.ContainsKey("unbind"))
                    commands.Add("unbind", new Console.Unbind());
                if (!commands.ContainsKey("clearbinds"))
                    commands.Add("clearbinds", new Console.ClearBinds());
                if (!commands.ContainsKey("hideui"))
                    commands.Add("hideui", new Console.HideUI());
                if (!commands.ContainsKey("disable"))
                    commands.Add("disable", new Console.Disable());
                if (!commands.ContainsKey("enable"))
                    commands.Add("enable", new Console.Enable());
                if (!commands.ContainsKey("endtutorial"))
                    commands.Add("endtutorial", new Console.EndTutorial());
                if (!commands.ContainsKey("disablenpcasset"))
                    commands.Add("disablenpcasset", new Console.DisableNPCAsset());
                if (!commands.ContainsKey("showfps"))
                    commands.Add("showfps", new Console.ShowFPS());
                if (!commands.ContainsKey("hidefps"))
                    commands.Add("hidefps", new Console.HideFPS());
                if (!commands.ContainsKey("cleartrash"))
                    commands.Add("cleartrash", new Console.ClearTrash());
            }
            catch (Exception ex)
            {
                DebugLog.Error($"EnsureCoreCommandsExist threw an exception", ex);
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
                DebugLog.Warning("FindPlayerByConnection: Connection is null");
                return null;
            }

            foreach (var p in Player.PlayerList)
            {
                if (p?.Owner == conn)
                    return p;
            }

            DebugLog.Warning($"FindPlayerByConnection: No player found for connection {conn.ClientId}");
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
                DebugLog.Warning(message);
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
            public string ServerDescription;
            public int CurrentPlayers;
            public int MaxPlayers;
            public bool AllowSleeping;
        }

        #endregion
    }
}

