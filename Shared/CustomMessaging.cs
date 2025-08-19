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
using ScheduleOne.Vehicles;
using UnityEngine;
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

		// Use a small id to avoid transport truncation
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

		// API hooks: mods can subscribe without Harmony
		public delegate void ClientMessageHook(string command, string data);
		public delegate void ServerMessageHook(NetworkConnection conn, string command, string data);
		public static event ClientMessageHook ClientMessageReceived;
		public static event ServerMessageHook ServerMessageReceived;

		#region Send helpers
		public static void SendToServer(string command, string data = "")
		{
			try
			{
				var ds = NetworkSingleton<DailySummary>.Instance;
				if (ds == null)
				{
					logger.Warning($"SendToServer skipped: DailySummary instance null for cmd='{command}'");
					return;
				}

				var msg = new Message { command = command, data = data };
				string raw = JsonConvert.SerializeObject(msg);
				PooledWriter writer = WriterPool.Retrieve();
				((Writer)writer).WriteString(raw);
				((NetworkBehaviour)ds).SendServerRpc(MessageId, writer, Channel.Reliable, DataOrderType.Default);
				writer.Store();
				logger.Msg($"SendToServer cmd='{command}' len={data?.Length ?? 0}");
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
				{
					logger.Warning($"SendToClient skipped: ds null? {ds==null}, conn null? {conn==null} for cmd='{command}'");
					return;
				}

				var msg = new Message { command = command, data = data };
				string raw = JsonConvert.SerializeObject(msg);
				PooledWriter writer = WriterPool.Retrieve();
				((Writer)writer).WriteString(raw);
				((NetworkBehaviour)ds).SendTargetRpc(MessageId, writer, Channel.Reliable, DataOrderType.Default, conn, false, true);
				writer.Store();
				logger.Msg($"SendToClient cmd='{command}' len={data?.Length ?? 0} to={conn.ClientId}");
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
				
				var msg = JsonConvert.DeserializeObject<Message>(raw);
				if (msg.command == null)
				{
					logger.Warning("OnClientMessageReceived: Message command is null");
					return;
				}

				logger.Msg($"OnClientMessageReceived cmd='{msg.command}' len={msg.data?.Length ?? 0}");

				// First, raise API event for mods
				try { ClientMessageReceived?.Invoke(msg.command, msg.data); } catch {}
				// Then, built-in routing if any
				HandleClientMessage(msg.command, msg.data);
			}
			catch (Exception ex)
			{
				logger.Error($"OnClientMessageReceived error: {ex}");
			}
		}

		private static void OnServerMessageReceived(PooledReader reader, Channel channel, NetworkConnection conn)
		{	
			if (!InstanceFinder.IsServer)
			{
				logger.Warning("OnServerMessageReceived: Not on server, ignoring message");
				return;
			}
			
			try
			{
				string raw = ((Reader)reader).ReadString();
				
				var msg = JsonConvert.DeserializeObject<Message>(raw);
				if (msg.command == null)
				{
					logger.Warning("OnServerMessageReceived: Message command is null");
					return;
				}

				logger.Msg($"OnServerMessageReceived cmd='{msg.command}' len={msg.data?.Length ?? 0} from={conn?.ClientId}");

				// First, raise API event for mods
				try { ServerMessageReceived?.Invoke(conn, msg.command, msg.data); } catch {}
				// Then, built-in routing if any
				HandleServerMessage(conn, msg.command, msg.data);
			}
			catch (Exception ex)
			{
				logger.Error($"OnServerMessageReceived error: {ex}");
			}
		}
		#endregion

		#region Routing
		private static void HandleClientMessage(string command, string data)
		{
			try
			{
				if (command == "exec_console")
				{
					var parts = new List<string>(data.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
					if (parts.Count == 0)
					{
						logger.Warning("HandleClientMessage: No command parts found for exec_console");
						return;
					}

					string cmd = parts[0].ToLower();
					parts.RemoveAt(0);
					
					var commandsField = typeof(Console).GetField("commands", BindingFlags.NonPublic | BindingFlags.Static);
					var commands = commandsField?.GetValue(null) as Dictionary<string, Console.ConsoleCommand>;
					if (commands == null)
					{
						logger.Error("HandleClientMessage: Could not access Console.commands on client");
						return;
					}
					
					if (!commands.ContainsKey(cmd))
					{
						logger.Warning($"HandleClientMessage: Command '{cmd}' not found on client");
						ScheduleOne.Console.LogWarning($"Command '{cmd}' not found.");
						return;
					}

					commands[cmd].Execute(parts);
					return;
				}
			}
			catch (Exception ex)
			{
				logger.Error($"HandleClientMessage error: {ex}");
			}
		}

		private static void HandleServerMessage(NetworkConnection conn, string command, string data)
		{
			// Example command: execute console command on server if sender is admin.
			if (command == "admin_console")
			{
				try
				{
					var player = FindPlayerByConnection(conn);
					if (player == null)
					{
						logger.Warning("HandleServerMessage: Could not find player for connection");
						return;
					}

					// Permission check
					if (!ServerConfig.CanUseConsole(player))
					{
						logger.Warning($"HandleServerMessage: Player {player.PlayerName} not permitted to use admin console.");
						return;
					}

					var parts = new List<string>(data.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
					
					if (parts.Count == 0)
					{
						logger.Warning("HandleServerMessage: No command parts found");
						return;
					}
					
					string cmd = parts[0].ToLower();
					
					if (!ServerConfig.CanUseCommand(player, cmd))
					{
						logger.Warning($"HandleServerMessage: Player {player.PlayerName} not permitted to run '{cmd}'.");
						return;
					}

					// Server-authoritative handling for vehicle spawns
					if (cmd == "spawnvehicle")
					{
						try
						{
							parts.RemoveAt(0);
							if (parts.Count == 0)
							{
								Console.LogWarning("Unrecognized command format. Correct format example(s): 'spawnvehicle shitbox'");
								return;
							}
							string vehicleCode = parts[0].ToLower();
							var vm = NetworkSingleton<VehicleManager>.Instance;
							if (vm == null)
							{
								logger.Error("HandleServerMessage: VehicleManager instance not found on server");
								return;
							}
							if (vm.GetVehiclePrefab(vehicleCode) == null)
							{
								Console.LogWarning($"Unrecognized vehicle code '{vehicleCode}'");
								return;
							}

							Vector3 position = player.transform.position + player.transform.forward * 4f + player.transform.up * 1f;
							Quaternion rotation = player.transform.rotation;
							var spawned = vm.SpawnAndReturnVehicle(vehicleCode, position, rotation, playerOwned: true);
							if (spawned != null)
							{
								try { spawned.NetworkObject.GiveOwnership(player.Owner); } catch {}
								ServerConfig.LogAdminAction(player, cmd, vehicleCode);
							}
							else
							{
								logger.Warning("HandleServerMessage: SpawnAndReturnVehicle returned null");
							}
							return; // handled on server
						}
						catch (Exception ex)
						{
							logger.Error($"HandleServerMessage: Error spawning vehicle server-side: {ex}");
							return;
						}
					}

					// Execute via Console commands map to bypass host guard for other commands
					var commandsField = typeof(Console).GetField("commands", BindingFlags.NonPublic | BindingFlags.Static);
					var commands = commandsField?.GetValue(null) as Dictionary<string, Console.ConsoleCommand>;
					if (commands == null)
					{
						logger.Error("HandleServerMessage: Could not access Console.commands field");
						return;
					}
					// Ensure a few core defaults exist
					if (!commands.ContainsKey("settime") || !commands.ContainsKey("give"))
					{
						InitializeConsoleCommands(commands);
					}
					
					if (commands.TryGetValue(cmd, out var handler))
					{
						
						parts.RemoveAt(0);
						string argsString = string.Join(" ", parts);
						
						// Relay to the specific client's context so Player.Local refers to their player, not loopback
						SendToClient(player.Owner, "exec_console", string.IsNullOrEmpty(argsString) ? cmd : ($"{cmd} {argsString}"));
						
						ServerConfig.LogAdminAction(player, cmd, argsString);
					}
					else
					{
						logger.Warning($"HandleServerMessage: Command '{cmd}' not found in available commands");
						Console.LogWarning($"Command '{cmd}' not found.");
					}
				}
				catch (Exception ex)
				{
					logger.Error($"HandleServerMessage: Error executing admin console command: {ex}");
				}
			}
		}

		private static Player FindPlayerByConnection(NetworkConnection conn)
		{
			if (conn == null)
			{
				logger.Warning("FindPlayerByConnection: Connection is null");
				return null;
			}
			
			foreach (var p in Player.PlayerList)
			{
				if (p?.Owner == conn)
				{
					return p;
				}
			}
			
			logger.Warning($"FindPlayerByConnection: No player found for connection {conn.ClientId}");
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
			}
			catch (Exception ex)
			{
				logger.Error($"InitializeConsoleCommands: Error initializing console commands: {ex}");
			}
		}
		#endregion
	}
}
