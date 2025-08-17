using System;
using System.Collections.Generic;
using System.Reflection;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Delegating;
using FishNet.Serializing;
using FishNet.Transporting;
using MelonLoader;
using Newtonsoft.Json;
using ScheduleOne;
using ScheduleOne.DevUtilities;
using ScheduleOne.PlayerScripts;
using ScheduleOne.UI;
using Console = ScheduleOne.Console;

namespace DedicatedServerMod.Shared
{
	/// <summary>
	/// Shared custom messaging hub. Registers custom RPCs on an existing NetworkBehaviour (DailySummary)
	/// and exposes helpers to send messages client<->server.
	/// </summary>
	public static class CustomMessaging
	{
		public struct Message
		{
			public string command;
			public string data;
		}

		// Use a small id to avoid transport truncation (matches Bisect pattern)
		public static readonly uint MessageId = 105u;

		private static MelonLogger.Instance logger = new MelonLogger.Instance("CustomMessaging");

		/// <summary>
		/// Harmony postfix for DailySummary.Awake. Registers our custom RPC handlers.
		/// </summary>
		public static void DailySummaryAwakePostfix(DailySummary __instance)
		{
			try
			{
				var nb = (NetworkBehaviour)__instance;
				// Register server->client target rpc
				nb.RegisterTargetRpc(MessageId, new ClientRpcDelegate(OnClientMessageReceived));
				// Register client->server server rpc
				nb.RegisterServerRpc(MessageId, new ServerRpcDelegate(OnServerMessageReceived));
				logger.Msg("Registered custom messaging RPCs on DailySummary");
			}
			catch (Exception ex)
			{
				logger.Error($"Failed to register custom RPCs: {ex}");
			}
		}

		#region Send helpers
		public static void SendToServer(string command, string data = "")
		{
			try
			{
				var ds = NetworkSingleton<DailySummary>.Instance;
				if (ds == null)
					return;

				var msg = new Message { command = command, data = data };
				string raw = JsonConvert.SerializeObject(msg);
				PooledWriter writer = WriterPool.Retrieve();
				((Writer)writer).WriteString(raw);
				((NetworkBehaviour)ds).SendServerRpc(MessageId, writer, Channel.Reliable, DataOrderType.Default);
				writer.Store();
			}
			catch (Exception ex)
			{
				logger.Error($"SendToServer error: {ex}");
			}
		}

		public static void SendToClient(NetworkConnection conn, string command, string data = "")
		{
			try
			{
				var ds = NetworkSingleton<DailySummary>.Instance;
				if (ds == null || conn == null)
					return;

				var msg = new Message { command = command, data = data };
				string raw = JsonConvert.SerializeObject(msg);
				PooledWriter writer = WriterPool.Retrieve();
				((Writer)writer).WriteString(raw);
				((NetworkBehaviour)ds).SendTargetRpc(MessageId, writer, Channel.Reliable, DataOrderType.Default, conn, false, true);
				writer.Store();
			}
			catch (Exception ex)
			{
				logger.Error($"SendToClient error: {ex}");
			}
		}

		public static void BroadcastToClients(string command, string data = "")
		{
			try
			{
				var ds = NetworkSingleton<DailySummary>.Instance;
				if (ds == null || !InstanceFinder.IsServer)
					return;

				foreach (var kvp in InstanceFinder.ServerManager.Clients)
				{
					var client = kvp.Value;
					if (client != null)
					{
						SendToClient(client, command, data);
					}
				}
			}
			catch (Exception ex)
			{
				logger.Error($"BroadcastToClients error: {ex}");
			}
		}
		#endregion

		#region Receive handlers
		private static void OnClientMessageReceived(PooledReader reader, Channel channel)
		{
			try
			{
				string raw = ((Reader)reader).ReadString();
				logger.Msg($"[DEBUG] OnClientMessageReceived: Raw message: {raw}");
				
				var msg = JsonConvert.DeserializeObject<Message>(raw);
				if (msg.command == null)
				{
					logger.Warning("[DEBUG] OnClientMessageReceived: Message command is null");
					return;
				}

				logger.Msg($"[DEBUG] OnClientMessageReceived: Parsed message - command: {msg.command}, data: {msg.data}");
				HandleClientMessage(msg.command, msg.data);
			}
			catch (Exception ex)
			{
				logger.Error($"[DEBUG] OnClientMessageReceived error: {ex}");
			}
		}

		private static void OnServerMessageReceived(PooledReader reader, Channel channel, NetworkConnection conn)
		{
			logger.Msg($"[DEBUG] OnServerMessageReceived: Called with connection ClientId: {conn?.ClientId}");
			
			if (!InstanceFinder.IsServer)
			{
				logger.Warning("[DEBUG] OnServerMessageReceived: Not on server, ignoring message");
				return;
			}
			
			try
			{
				string raw = ((Reader)reader).ReadString();
				logger.Msg($"[DEBUG] OnServerMessageReceived: Raw message: {raw}");
				
				var msg = JsonConvert.DeserializeObject<Message>(raw);
				if (msg.command == null)
				{
					logger.Warning("[DEBUG] OnServerMessageReceived: Message command is null");
					return;
				}

				logger.Msg($"[DEBUG] OnServerMessageReceived: Parsed message - command: {msg.command}, data: {msg.data}");
				HandleServerMessage(conn, msg.command, msg.data);
			}
			catch (Exception ex)
			{
				logger.Error($"[DEBUG] OnServerMessageReceived error: {ex}");
			}
		}
		#endregion

		#region Routing
		private static void HandleClientMessage(string command, string data)
		{
			logger.Msg($"[DEBUG] HandleClientMessage: command: {command}, data: {data}");
			
			try
			{
				if (command == "exec_console")
				{
					logger.Msg("[DEBUG] HandleClientMessage: Executing console command locally on client context");
					var parts = new List<string>(data.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
					if (parts.Count == 0)
					{
						logger.Warning("[DEBUG] HandleClientMessage: No command parts found for exec_console");
						return;
					}

					string cmd = parts[0].ToLower();
					parts.RemoveAt(0);
					
					var commandsField = typeof(Console).GetField("commands", BindingFlags.NonPublic | BindingFlags.Static);
					var commands = commandsField?.GetValue(null) as Dictionary<string, Console.ConsoleCommand>;
					if (commands == null)
					{
						logger.Error("[DEBUG] HandleClientMessage: Could not access Console.commands on client");
						return;
					}
					
					if (!commands.ContainsKey(cmd))
					{
						logger.Warning($"[DEBUG] HandleClientMessage: Command '{cmd}' not found on client");
						ScheduleOne.Console.LogWarning($"Command '{cmd}' not found.");
						return;
					}

					logger.Msg($"[DEBUG] HandleClientMessage: Invoking command '{cmd}' on client with args: [{string.Join(" ", parts)}]");
					commands[cmd].Execute(parts);
					return;
				}
			}
			catch (Exception ex)
			{
				logger.Error($"[DEBUG] HandleClientMessage error: {ex}");
			}
		}

		private static void HandleServerMessage(NetworkConnection conn, string command, string data)
		{
			logger.Msg($"[DEBUG] HandleServerMessage: Processing message from connection {conn?.ClientId} - command: {command}, data: {data}");

			// Example command: execute console command on server if sender is admin.
			if (command == "admin_console")
			{
				logger.Msg("[DEBUG] HandleServerMessage: Processing admin_console command");
				
				try
				{
					var player = FindPlayerByConnection(conn);
					if (player == null)
					{
						logger.Warning("[DEBUG] HandleServerMessage: Could not find player for connection");
						return;
					}

					logger.Msg($"[DEBUG] HandleServerMessage: Found player: {player.PlayerName}");

					// Permission check
					logger.Msg("[DEBUG] HandleServerMessage: Checking if player can use console");
					if (!ServerConfig.CanUseConsole(player))
					{
						logger.Warning($"[DEBUG] HandleServerMessage: Player {player.PlayerName} not permitted to use admin console.");
						return;
					}

					logger.Msg($"[DEBUG] HandleServerMessage: Player {player.PlayerName} has console permission, checking command permission");

					var parts = new List<string>(data.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
					logger.Msg($"[DEBUG] HandleServerMessage: Command parts: [{string.Join(", ", parts)}]");
					
					if (parts.Count == 0)
					{
						logger.Warning("[DEBUG] HandleServerMessage: No command parts found");
						return;
					}
					
					string cmd = parts[0].ToLower();
					logger.Msg($"[DEBUG] HandleServerMessage: Command to execute: {cmd}");
					
					if (!ServerConfig.CanUseCommand(player, cmd))
					{
						logger.Warning($"[DEBUG] HandleServerMessage: Player {player.PlayerName} not permitted to run '{cmd}'.");
						return;
					}

					logger.Msg($"[DEBUG] HandleServerMessage: Player {player.PlayerName} has permission to run '{cmd}', executing...");

					// Execute via Console commands map to bypass host guard.
				    var commandsField = typeof(Console).GetField("commands", BindingFlags.NonPublic | BindingFlags.Static);
				    var commands = commandsField?.GetValue(null) as Dictionary<string, Console.ConsoleCommand>;
				
				    if (commands == null)
				    {
					    logger.Error("[DEBUG] HandleServerMessage: Could not access Console.commands field");
					    return;
				    }
				
				    // Ensure default console commands are registered even if some custom ones were added first
				    // On dedicated servers, admin commands may be registered before defaults; check for a few core defaults
				    if (!commands.ContainsKey("settime") || !commands.ContainsKey("give"))
				    {
					    logger.Msg("[DEBUG] HandleServerMessage: Default console commands missing, initializing now...");
					    InitializeConsoleCommands(commands);
				    }
				
				    logger.Msg($"[DEBUG] HandleServerMessage: Found {commands.Count} available console commands");
					
					if (commands.TryGetValue(cmd, out var handler))
					{
						logger.Msg($"[DEBUG] HandleServerMessage: Found handler for command '{cmd}', relaying to client {player.PlayerName} for execution...");
						
						parts.RemoveAt(0);
						string argsString = string.Join(" ", parts);
						logger.Msg($"[DEBUG] HandleServerMessage: Relaying '{cmd} {argsString}' to client via exec_console");
						
						// Relay to the specific client's context so Player.Local refers to their player, not loopback
						SendToClient(player.Owner, "exec_console", string.IsNullOrEmpty(argsString) ? cmd : ($"{cmd} {argsString}"));
						logger.Msg($"[DEBUG] HandleServerMessage: Relay for '{cmd}' sent to client {player.PlayerName}");
						
						ServerConfig.LogAdminAction(player, cmd, argsString);
					}
					else
					{
						logger.Warning($"[DEBUG] HandleServerMessage: Command '{cmd}' not found in available commands");
						Console.LogWarning($"Command '{cmd}' not found.");
					}
				}
				catch (Exception ex)
				{
					logger.Error($"[DEBUG] HandleServerMessage: Error executing admin console command: {ex}");
				}
			}
			else
			{
				logger.Msg($"[DEBUG] HandleServerMessage: Unknown command '{command}', ignoring");
			}
		}

		private static Player FindPlayerByConnection(NetworkConnection conn)
		{
			logger.Msg($"[DEBUG] FindPlayerByConnection: Looking for player with connection ClientId: {conn?.ClientId}");
			
			if (conn == null)
			{
				logger.Warning("[DEBUG] FindPlayerByConnection: Connection is null");
				return null;
			}
			
			logger.Msg($"[DEBUG] FindPlayerByConnection: Player.PlayerList count: {Player.PlayerList.Count}");
			
			foreach (var p in Player.PlayerList)
			{
				if (p?.Owner == conn)
				{
					logger.Msg($"[DEBUG] FindPlayerByConnection: Found player {p.PlayerName} for connection {conn.ClientId}");
					return p;
				}
				else
				{
					logger.Msg($"[DEBUG] FindPlayerByConnection: Player {p?.PlayerName ?? "null"} has owner ClientId: {p?.Owner?.ClientId}");
				}
			}
			
			logger.Warning($"[DEBUG] FindPlayerByConnection: No player found for connection {conn.ClientId}");
			return null;
		}
		#endregion

		#region Console Commands Initialization
		/// <summary>
		/// Initialize console commands if they're not already initialized (critical for dedicated servers)
		/// This replicates the initialization logic from Console.Awake()
		/// </summary>
		public static void InitializeConsoleCommands(Dictionary<string, Console.ConsoleCommand> commands)
		{
			try
			{
				logger.Msg("[DEBUG] InitializeConsoleCommands: Initializing console commands for dedicated server");
				
				// Replicate the exact initialization from Console.Awake(), but add only if missing
				if (!commands.ContainsKey("freecam")) commands.Add("freecam", new Console.FreeCamCommand());
				if (!commands.ContainsKey("save")) commands.Add("save", new Console.Save());
				if (!commands.ContainsKey("settime")) commands.Add("settime", new Console.SetTimeCommand());
				if (!commands.ContainsKey("give")) commands.Add("give", new Console.AddItemToInventoryCommand());
				if (!commands.ContainsKey("clearinventory")) commands.Add("clearinventory", new Console.ClearInventoryCommand());
				if (!commands.ContainsKey("changecash")) commands.Add("changecash", new Console.ChangeCashCommand());
				if (!commands.ContainsKey("changebalance")) commands.Add("changebalance", new Console.ChangeOnlineBalanceCommand());
				if (!commands.ContainsKey("addxp")) commands.Add("addxp", new Console.GiveXP());
				if (!commands.ContainsKey("spawnvehicle")) commands.Add("spawnvehicle", new Console.SpawnVehicleCommand());
				if (!commands.ContainsKey("setmovespeed")) commands.Add("setmovespeed", new Console.SetMoveSpeedCommand());
				if (!commands.ContainsKey("setjumpforce")) commands.Add("setjumpforce", new Console.SetJumpMultiplier());
				if (!commands.ContainsKey("teleport")) commands.Add("teleport", new Console.Teleport());
				if (!commands.ContainsKey("setowned")) commands.Add("setowned", new Console.SetPropertyOwned());
				if (!commands.ContainsKey("packageproduct")) commands.Add("packageproduct", new Console.PackageProduct());
				if (!commands.ContainsKey("setstaminareserve")) commands.Add("setstaminareserve", new Console.SetStaminaReserve());
				if (!commands.ContainsKey("raisewanted")) commands.Add("raisewanted", new Console.RaisedWanted());
				if (!commands.ContainsKey("lowerwanted")) commands.Add("lowerwanted", new Console.LowerWanted());
				if (!commands.ContainsKey("clearwanted")) commands.Add("clearwanted", new Console.ClearWanted());
				if (!commands.ContainsKey("sethealth")) commands.Add("sethealth", new Console.SetHealth());
				if (!commands.ContainsKey("settimescale")) commands.Add("settimescale", new Console.SetTimeScale());
				if (!commands.ContainsKey("setvar")) commands.Add("setvar", new Console.SetVariableValue());
				if (!commands.ContainsKey("setqueststate")) commands.Add("setqueststate", new Console.SetQuestState());
				if (!commands.ContainsKey("setquestentrystate")) commands.Add("setquestentrystate", new Console.SetQuestEntryState());
				if (!commands.ContainsKey("setemotion")) commands.Add("setemotion", new Console.SetEmotion());
				if (!commands.ContainsKey("setunlocked")) commands.Add("setunlocked", new Console.SetUnlocked());
				if (!commands.ContainsKey("setrelationship")) commands.Add("setrelationship", new Console.SetRelationship());
				if (!commands.ContainsKey("addemployee")) commands.Add("addemployee", new Console.AddEmployeeCommand());
				if (!commands.ContainsKey("setdiscovered")) commands.Add("setdiscovered", new Console.SetDiscovered());
				if (!commands.ContainsKey("growplants")) commands.Add("growplants", new Console.GrowPlants());
				if (!commands.ContainsKey("setlawintensity")) commands.Add("setlawintensity", new Console.SetLawIntensity());
				if (!commands.ContainsKey("setquality")) commands.Add("setquality", new Console.SetQuality());
				if (!commands.ContainsKey("bind")) commands.Add("bind", new Console.Bind());
				if (!commands.ContainsKey("unbind")) commands.Add("unbind", new Console.Unbind());
				if (!commands.ContainsKey("clearbinds")) commands.Add("clearbinds", new Console.ClearBinds());
				if (!commands.ContainsKey("hideui")) commands.Add("hideui", new Console.HideUI());
				if (!commands.ContainsKey("disable")) commands.Add("disable", new Console.Disable());
				if (!commands.ContainsKey("enable")) commands.Add("enable", new Console.Enable());
				if (!commands.ContainsKey("endtutorial")) commands.Add("endtutorial", new Console.EndTutorial());
				if (!commands.ContainsKey("disablenpcasset")) commands.Add("disablenpcasset", new Console.DisableNPCAsset());
				if (!commands.ContainsKey("showfps")) commands.Add("showfps", new Console.ShowFPS());
				if (!commands.ContainsKey("hidefps")) commands.Add("hidefps", new Console.HideFPS());
				if (!commands.ContainsKey("cleartrash")) commands.Add("cleartrash", new Console.ClearTrash());

				logger.Msg($"[DEBUG] InitializeConsoleCommands: Successfully initialized {commands.Count} console commands");
			}
			catch (Exception ex)
			{
				logger.Error($"[DEBUG] InitializeConsoleCommands: Error initializing console commands: {ex}");
			}
		}
		#endregion
	}
}
