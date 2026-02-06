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
using ScheduleOne.DevUtilities;
using ScheduleOne.PlayerScripts;
using ScheduleOne.UI;
using ScheduleOne.Vehicles;
using UnityEngine;
using Console = ScheduleOne.Console;

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
            _logger.Msg($"RouteServerMessage: cmd='{command}' from ClientId={conn?.ClientId}");

            switch (command)
            {
                case "admin_console":
                    HandleAdminConsoleCommand(conn, data);
                    break;

                case "auth_response":
                    HandleAuthenticationResponse(conn, data);
                    break;

                case "client_ready":
                    HandleClientReady(conn);
                    break;

                case "request_server_data":
                    HandleServerDataRequest(conn);
                    break;

                default:
                    _logger.Msg($"Unhandled server message: {command}");
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
            _logger.Msg($"RouteClientMessage: cmd='{command}' data_length={data?.Length ?? 0}");

            switch (command)
            {
                case "exec_console":
                    HandleClientConsoleCommand(data);
                    break;

                case "auth_challenge":
                    _logger.Msg("Routing to HandleAuthenticationChallenge");
                    HandleAuthenticationChallenge(data);
                    break;

                case "auth_result":
                    _logger.Msg("Routing to HandleAuthenticationResult");
                    HandleAuthenticationResult(data);
                    break;

                default:
                    _logger.Msg($"Unhandled client message: {command}");
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
                var commandsField = typeof(ScheduleOne.Console).GetField("commands",
                    BindingFlags.NonPublic | BindingFlags.Static);
                var commands = commandsField?.GetValue(null) as Dictionary<string, ScheduleOne.Console.ConsoleCommand>;

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

        #region Authentication Handling

        /// <summary>
        /// Handles an authentication response from a client (server-side).
        /// </summary>
        /// <param name="conn">The connection that sent the response</param>
        /// <param name="data">The authentication response data</param>
        private static void HandleAuthenticationResponse(NetworkConnection conn, string data)
        {
#if SERVER
            try
            {
                var response = JsonConvert.DeserializeObject<AuthenticationResponseMessage>(data);
                if (response == null)
                {
                    _logger.Warning("HandleAuthenticationResponse: Failed to deserialize response");
                    return;
                }

                _logger.Msg($"Received auth response from ClientId {conn.ClientId}");

                // Get player manager instance
                var playerManager = DedicatedServerMod.Server.Core.ServerBootstrap.Players;
                if (playerManager == null)
                {
                    _logger.Error("HandleAuthenticationResponse: PlayerManager not found");
                    SendAuthenticationResult(conn, false, "Server error");
                    conn.Disconnect(true);
                    return;
                }

                var playerInfo = playerManager.GetPlayer(conn);
                if (playerInfo == null)
                {
                    _logger.Warning($"HandleAuthenticationResponse: No player info for ClientId {conn.ClientId}");
                    SendAuthenticationResult(conn, false, "Player not found");
                    conn.Disconnect(true);
                    return;
                }

                // Authenticate player with password hash
                var authResult = playerManager.Authentication.AuthenticatePlayer(playerInfo, response.PasswordHash);
                
                if (authResult.IsSuccessful)
                {
                    playerInfo.IsAuthenticated = true;
                    _logger.Msg($"Player authenticated successfully: ClientId {conn.ClientId}");
                    SendAuthenticationResult(conn, true, "Authentication successful");
                }
                else
                {
                    _logger.Warning($"Authentication failed for ClientId {conn.ClientId}: {authResult.Message}");
                    SendAuthenticationResult(conn, false, authResult.Message);
                    
                    if (authResult.ShouldDisconnect)
                    {
                        // Delay disconnect slightly to ensure message is sent
                        MelonCoroutines.Start(DelayedDisconnect(conn, 0.5f));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"HandleAuthenticationResponse: Error: {ex}");
                SendAuthenticationResult(conn, false, "Authentication error");
                conn.Disconnect(true);
            }
#endif
        }

        /// <summary>
        /// Sends an authentication result to a client (server-side).
        /// </summary>
        private static void SendAuthenticationResult(NetworkConnection conn, bool success, string message)
        {
#if SERVER
            try
            {
                var result = new AuthenticationResultMessage
                {
                    Success = success,
                    ErrorMessage = success ? null : message
                };

                var json = JsonConvert.SerializeObject(result);
                CustomMessaging.SendToClient(conn, "auth_result", json);
            }
            catch (Exception ex)
            {
                _logger.Error($"SendAuthenticationResult: Error: {ex}");
            }
#endif
        }

        /// <summary>
        /// Sends an authentication challenge to a client (server-side).
        /// </summary>
        public static void SendAuthenticationChallenge(NetworkConnection conn, bool requiresPassword, string serverName)
        {
#if SERVER
            try
            {
                var challenge = new AuthenticationChallengeMessage
                {
                    RequiresPassword = requiresPassword,
                    ServerName = serverName
                };

                var json = JsonConvert.SerializeObject(challenge);
                CustomMessaging.SendToClient(conn, "auth_challenge", json);
                _logger.Msg($"Sent auth challenge to ClientId {conn.ClientId} (password required: {requiresPassword})");
            }
            catch (Exception ex)
            {
                _logger.Error($"SendAuthenticationChallenge: Error: {ex}");
            }
#endif
        }

        /// <summary>
        /// Handles an authentication challenge from the server (client-side).
        /// </summary>
        private static void HandleAuthenticationChallenge(string data)
        {
#if CLIENT
            try
            {
                var challenge = JsonConvert.DeserializeObject<AuthenticationChallengeMessage>(data);
                if (challenge == null)
                {
                    _logger.Warning("HandleAuthenticationChallenge: Failed to deserialize challenge");
                    return;
                }

                _logger.Msg($"Received auth challenge from server: {challenge.ServerName} (password required: {challenge.RequiresPassword})");

                if (challenge.RequiresPassword)
                {
                    // Trigger password prompt via ClientUIManager
                    var uiManager = DedicatedServerMod.Client.Core.ClientBootstrap.Instance?.UIManager;
                    if (uiManager != null)
                    {
                        uiManager.ShowPasswordPrompt(challenge.ServerName);
                    }
                    else
                    {
                        _logger.Error("HandleAuthenticationChallenge: UIManager not found");
                    }
                }
                else
                {
                    // No password required, send empty response to continue
                    SendAuthenticationResponse(null);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"HandleAuthenticationChallenge: Error: {ex}");
            }
#else
            _logger.Warning("HandleAuthenticationChallenge called but CLIENT directive not defined");
#endif
        }

        /// <summary>
        /// Sends an authentication response to the server (client-side).
        /// </summary>
        public static void SendAuthenticationResponse(string passwordHash)
        {
#if CLIENT
            try
            {
                var response = new AuthenticationResponseMessage
                {
                    PasswordHash = passwordHash ?? string.Empty,
                    ClientVersion = DedicatedServerMod.Utils.Constants.MOD_VERSION
                };

                var json = JsonConvert.SerializeObject(response);
                CustomMessaging.SendToServer("auth_response", json);
                _logger.Msg("Sent auth response to server");
            }
            catch (Exception ex)
            {
                _logger.Error($"SendAuthenticationResponse: Error: {ex}");
            }
#endif
        }

        /// <summary>
        /// Handles an authentication result from the server (client-side).
        /// </summary>
        private static void HandleAuthenticationResult(string data)
        {
#if CLIENT
            try
            {
                _logger.Msg($"HandleAuthenticationResult called with data length: {data?.Length ?? 0}");
                _logger.Msg($"Raw data: {data}");
                
                var result = JsonConvert.DeserializeObject<AuthenticationResultMessage>(data);
                if (result == null)
                {
                    _logger.Warning("HandleAuthenticationResult: Failed to deserialize result");
                    return;
                }

                _logger.Msg($"Deserialized result - Success: {result.Success}, ErrorMessage: {result.ErrorMessage}");
                _logger.Msg($"Received auth result: {(result.Success ? "SUCCESS" : "FAILED")}");

                if (result.Success)
                {
                    _logger.Msg("? Authentication successful - connection established");
                    
                    // Hide password prompt if shown
                    var uiManager = DedicatedServerMod.Client.Core.ClientBootstrap.Instance?.UIManager;
                    _logger.Msg($"UIManager found: {uiManager != null}");
                    
                    if (uiManager != null)
                    {
                        _logger.Msg("Calling OnAuthenticationSuccess()");
                        uiManager.OnAuthenticationSuccess(); // Notify success
                        
                        _logger.Msg("Calling HidePasswordPrompt()");
                        uiManager.HidePasswordPrompt();
                        
                        _logger.Msg("Password dialog should now be hidden");
                    }
                    else
                    {
                        _logger.Error("UIManager is null - cannot hide password dialog!");
                    }
                }
                else
                {
                    _logger.Warning($"? Authentication failed: {result.ErrorMessage}");
                    
                    // Show error to user via UI manager
                    var uiManager = DedicatedServerMod.Client.Core.ClientBootstrap.Instance?.UIManager;
                    if (uiManager != null)
                    {
                        uiManager.ShowAuthenticationError(result.ErrorMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"HandleAuthenticationResult: Error: {ex}");
            }
#endif
        }

        /// <summary>
        /// Handles the client_ready message from a client.
        /// Sends authentication challenge immediately when client is ready.
        /// </summary>
        private static void HandleClientReady(NetworkConnection conn)
        {
#if SERVER
            try
            {
                _logger.Msg($"Client ready message received from ClientId {conn.ClientId}");
                
                // Get the server's PlayerManager
                var playerManager = DedicatedServerMod.Server.Core.ServerBootstrap.Players;
                if (playerManager == null)
                {
                    _logger.Warning("PlayerManager not available, cannot send auth challenge");
                    return;
                }

                // Check if password is required
                bool requiresPassword = playerManager.Authentication.RequiresPassword();
                _logger.Msg($"Sending immediate auth challenge to ClientId {conn.ClientId} (password required: {requiresPassword})");

                // Send authentication challenge immediately
                SendAuthenticationChallenge(
                    conn,
                    requiresPassword,
                    DedicatedServerMod.Shared.Configuration.ServerConfig.Instance.ServerName
                );
            }
            catch (Exception ex)
            {
                _logger.Error($"Error handling client_ready: {ex}");
            }
#endif
        }

        /// <summary>
        /// Coroutine to delay a disconnection (allows message to be sent first).
        /// </summary>
        private static System.Collections.IEnumerator DelayedDisconnect(NetworkConnection conn, float delay)
        {
            yield return new UnityEngine.WaitForSeconds(delay);
            if (conn != null)
            {
                conn.Disconnect(true);
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

                var commandsField = typeof(ScheduleOne.Console).GetField("commands",
                    BindingFlags.NonPublic | BindingFlags.Static);
                var commands = commandsField?.GetValue(null) as Dictionary<string, ScheduleOne.Console.ConsoleCommand>;

                if (commands == null)
                {
                    _logger.Error("HandleClientConsoleCommand: Could not access Console.commands on client");
                    return;
                }

                if (!commands.ContainsKey(cmd))
                {
                    _logger.Warning($"HandleClientConsoleCommand: Command '{cmd}' not found on client");
                    ScheduleOne.Console.LogCommandError($"Command '{cmd}' not found.");
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
        private static void EnsureCoreCommandsExist(Dictionary<string, ScheduleOne.Console.ConsoleCommand> commands)
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
                    commands.Add("disable", new Console.Enable());
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
                ScheduleOne.Console.LogCommandError(message);
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
