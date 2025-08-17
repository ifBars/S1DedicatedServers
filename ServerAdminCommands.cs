using System;
using System.Collections.Generic;
using System.Linq;
using FishNet;
using ScheduleOne;
using ScheduleOne.PlayerScripts;
using MelonLoader;
using Console = ScheduleOne.Console;

namespace DedicatedServerMod
{
    /// <summary>
    /// Server admin management commands for operator and admin control
    /// Integrates with the native Console system for seamless command execution
    /// </summary>
    public class ServerAdminCommands
    {
        private static MelonLogger.Instance logger;

        public static void Initialize(MelonLogger.Instance loggerInstance)
        {
            logger = loggerInstance;
            RegisterAdminCommands();
        }

        private static void RegisterAdminCommands()
        {
            // Add our admin commands to the console system
            var commands = typeof(Console).GetField("commands", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.GetValue(null) 
                as Dictionary<string, Console.ConsoleCommand>;

            if (commands != null)
            {
                commands.Add("op", new OpCommand());
                commands.Add("deop", new DeopCommand());
                commands.Add("admin", new AdminCommand());
                commands.Add("deadmin", new DeadminCommand());
                commands.Add("listops", new ListOpsCommand());
                commands.Add("listadmins", new ListAdminsCommand());
                commands.Add("serverinfo", new ServerInfoCommand());
                commands.Add("reloadconfig", new ReloadConfigCommand());
                commands.Add("kick", new KickPlayerCommand());
                commands.Add("ban", new BanPlayerCommand());
                commands.Add("unban", new UnbanPlayerCommand());
                commands.Add("listplayers", new ListPlayersCommand());

                logger?.Msg("Registered server admin commands");
            }
        }

        #region Admin Management Commands

        public class OpCommand : Console.ConsoleCommand
        {
            public override string CommandWord => "op";
            public override string CommandDescription => "Grants operator privileges to a player";
            public override string ExampleUsage => "op <player_name_or_steamid>";

            public override void Execute(List<string> args)
            {
                if (!InstanceFinder.IsServer)
                {
                    Console.LogWarning("This command can only be used on a dedicated server");
                    return;
                }

                if (args.Count == 0)
                {
                    Console.LogWarning($"Usage: {ExampleUsage}");
                    return;
                }

                string identifier = args[0];
                Player targetPlayer = FindPlayerByNameOrId(identifier);

                if (targetPlayer != null)
                {
                    string steamId = ServerConfig.GetPlayerSteamId(targetPlayer);
                    if (ServerConfig.AddOperator(steamId))
                    {
                        Console.Log($"Granted operator privileges to {targetPlayer.PlayerName} ({steamId})");
                        ServerManager.BroadcastMessage($"{targetPlayer.PlayerName} has been granted operator privileges");
                    }
                    else
                    {
                        Console.LogWarning($"{targetPlayer.PlayerName} is already an operator");
                    }
                }
                else
                {
                    // Try adding by Steam ID directly
                    if (ServerConfig.AddOperator(identifier))
                    {
                        Console.Log($"Granted operator privileges to Steam ID: {identifier}");
                    }
                    else
                    {
                        Console.LogWarning($"Player not found: {identifier}");
                    }
                }
            }
        }

        public class DeopCommand : Console.ConsoleCommand
        {
            public override string CommandWord => "deop";
            public override string CommandDescription => "Removes operator privileges from a player";
            public override string ExampleUsage => "deop <player_name_or_steamid>";

            public override void Execute(List<string> args)
            {
                if (!InstanceFinder.IsServer)
                {
                    Console.LogWarning("This command can only be used on a dedicated server");
                    return;
                }

                if (args.Count == 0)
                {
                    Console.LogWarning($"Usage: {ExampleUsage}");
                    return;
                }

                string identifier = args[0];
                Player targetPlayer = FindPlayerByNameOrId(identifier);

                if (targetPlayer != null)
                {
                    string steamId = ServerConfig.GetPlayerSteamId(targetPlayer);
                    if (ServerConfig.RemoveOperator(steamId))
                    {
                        Console.Log($"Removed operator privileges from {targetPlayer.PlayerName} ({steamId})");
                        ServerManager.BroadcastMessage($"{targetPlayer.PlayerName} is no longer an operator");
                    }
                    else
                    {
                        Console.LogWarning($"{targetPlayer.PlayerName} is not an operator");
                    }
                }
                else
                {
                    // Try removing by Steam ID directly
                    if (ServerConfig.RemoveOperator(identifier))
                    {
                        Console.Log($"Removed operator privileges from Steam ID: {identifier}");
                    }
                    else
                    {
                        Console.LogWarning($"Operator not found: {identifier}");
                    }
                }
            }
        }

        public class AdminCommand : Console.ConsoleCommand
        {
            public override string CommandWord => "admin";
            public override string CommandDescription => "Grants admin privileges to a player";
            public override string ExampleUsage => "admin <player_name_or_steamid>";

            public override void Execute(List<string> args)
            {
                if (!InstanceFinder.IsServer)
                {
                    Console.LogWarning("This command can only be used on a dedicated server");
                    return;
                }

                if (args.Count == 0)
                {
                    Console.LogWarning($"Usage: {ExampleUsage}");
                    return;
                }

                string identifier = args[0];
                Player targetPlayer = FindPlayerByNameOrId(identifier);

                if (targetPlayer != null)
                {
                    string steamId = ServerConfig.GetPlayerSteamId(targetPlayer);
                    if (ServerConfig.AddAdmin(steamId))
                    {
                        Console.Log($"Granted admin privileges to {targetPlayer.PlayerName} ({steamId})");
                        ServerManager.BroadcastMessage($"{targetPlayer.PlayerName} has been granted admin privileges");
                    }
                    else
                    {
                        Console.LogWarning($"{targetPlayer.PlayerName} is already an admin");
                    }
                }
                else
                {
                    // Try adding by Steam ID directly
                    if (ServerConfig.AddAdmin(identifier))
                    {
                        Console.Log($"Granted admin privileges to Steam ID: {identifier}");
                    }
                    else
                    {
                        Console.LogWarning($"Player not found: {identifier}");
                    }
                }
            }
        }

        public class DeadminCommand : Console.ConsoleCommand
        {
            public override string CommandWord => "deadmin";
            public override string CommandDescription => "Removes admin privileges from a player";
            public override string ExampleUsage => "deadmin <player_name_or_steamid>";

            public override void Execute(List<string> args)
            {
                if (!InstanceFinder.IsServer)
                {
                    Console.LogWarning("This command can only be used on a dedicated server");
                    return;
                }

                if (args.Count == 0)
                {
                    Console.LogWarning($"Usage: {ExampleUsage}");
                    return;
                }

                string identifier = args[0];
                Player targetPlayer = FindPlayerByNameOrId(identifier);

                if (targetPlayer != null)
                {
                    string steamId = ServerConfig.GetPlayerSteamId(targetPlayer);
                    if (ServerConfig.RemoveAdmin(steamId))
                    {
                        Console.Log($"Removed admin privileges from {targetPlayer.PlayerName} ({steamId})");
                        ServerManager.BroadcastMessage($"{targetPlayer.PlayerName} is no longer an admin");
                    }
                    else
                    {
                        Console.LogWarning($"{targetPlayer.PlayerName} is not an admin");
                    }
                }
                else
                {
                    // Try removing by Steam ID directly
                    if (ServerConfig.RemoveAdmin(identifier))
                    {
                        Console.Log($"Removed admin privileges from Steam ID: {identifier}");
                    }
                    else
                    {
                        Console.LogWarning($"Admin not found: {identifier}");
                    }
                }
            }
        }

        public class ListOpsCommand : Console.ConsoleCommand
        {
            public override string CommandWord => "listops";
            public override string CommandDescription => "Lists all server operators";
            public override string ExampleUsage => "listops";

            public override void Execute(List<string> args)
            {
                if (!InstanceFinder.IsServer)
                {
                    Console.LogWarning("This command can only be used on a dedicated server");
                    return;
                }

                var operators = ServerConfig.GetAllOperators();
                if (operators.Count == 0)
                {
                    Console.Log("No operators configured");
                    return;
                }

                Console.Log($"=== Server Operators ({operators.Count}) ===");
                foreach (var steamId in operators)
                {
                    var player = ServerConfig.GetPlayerBySteamId(steamId);
                    if (player != null)
                    {
                        Console.Log($"- {player.PlayerName} ({steamId}) [ONLINE]");
                    }
                    else
                    {
                        Console.Log($"- {steamId} [OFFLINE]");
                    }
                }
            }
        }

        public class ListAdminsCommand : Console.ConsoleCommand
        {
            public override string CommandWord => "listadmins";
            public override string CommandDescription => "Lists all server admins";
            public override string ExampleUsage => "listadmins";

            public override void Execute(List<string> args)
            {
                if (!InstanceFinder.IsServer)
                {
                    Console.LogWarning("This command can only be used on a dedicated server");
                    return;
                }

                var admins = ServerConfig.GetAllAdmins();
                if (admins.Count == 0)
                {
                    Console.Log("No admins configured");
                    return;
                }

                Console.Log($"=== Server Admins ({admins.Count}) ===");
                foreach (var steamId in admins)
                {
                    var player = ServerConfig.GetPlayerBySteamId(steamId);
                    bool isOperator = ServerConfig.IsOperator(steamId);
                    string status = player != null ? "[ONLINE]" : "[OFFLINE]";
                    string role = isOperator ? "OPERATOR" : "ADMIN";
                    
                    if (player != null)
                    {
                        Console.Log($"- {player.PlayerName} ({steamId}) [{role}] {status}");
                    }
                    else
                    {
                        Console.Log($"- {steamId} [{role}] {status}");
                    }
                }
            }
        }

        #endregion

        #region Server Management Commands

        public class ServerInfoCommand : Console.ConsoleCommand
        {
            public override string CommandWord => "serverinfo";
            public override string CommandDescription => "Displays comprehensive server information";
            public override string ExampleUsage => "serverinfo";

            public override void Execute(List<string> args)
            {
                Console.Log(ServerConfig.GetServerInfo());
            }
        }

        public class ReloadConfigCommand : Console.ConsoleCommand
        {
            public override string CommandWord => "reloadconfig";
            public override string CommandDescription => "Reloads the server configuration from file";
            public override string ExampleUsage => "reloadconfig";

            public override void Execute(List<string> args)
            {
                if (!InstanceFinder.IsServer)
                {
                    Console.LogWarning("This command can only be used on a dedicated server");
                    return;
                }

                try
                {
                    ServerConfig.ReloadConfig();
                    Console.Log("Server configuration reloaded successfully");
                }
                catch (Exception ex)
                {
                    Console.LogError($"Failed to reload configuration: {ex.Message}");
                }
            }
        }

        public class ListPlayersCommand : Console.ConsoleCommand
        {
            public override string CommandWord => "listplayers";
            public override string CommandDescription => "Lists all connected players with their roles";
            public override string ExampleUsage => "listplayers";

            public override void Execute(List<string> args)
            {
                if (!InstanceFinder.IsServer)
                {
                    Console.LogWarning("This command can only be used on a dedicated server");
                    return;
                }

                var players = Player.PlayerList;
                if (players.Count == 0)
                {
                    Console.Log("No players currently connected");
                    return;
                }

                Console.Log($"=== Connected Players ({players.Count}) ===");
                foreach (var player in players)
                {
                    if (player?.gameObject?.name == "[DedicatedServerHostLoopback]") continue;

                    string steamId = ServerConfig.GetPlayerSteamId(player);
                    string role = "PLAYER";
                    
                    if (ServerConfig.IsOperator(player))
                        role = "OPERATOR";
                    else if (ServerConfig.IsAdmin(player))
                        role = "ADMIN";

                    Console.Log($"- {player.PlayerName} ({steamId}) [{role}]");
                }
            }
        }

        #endregion

        #region Player Management Commands (Basic Implementation)

        public class KickPlayerCommand : Console.ConsoleCommand
        {
            public override string CommandWord => "kick";
            public override string CommandDescription => "Kicks a player from the server";
            public override string ExampleUsage => "kick <player_name> [reason]";

            public override void Execute(List<string> args)
            {
                if (!InstanceFinder.IsServer)
                {
                    Console.LogWarning("This command can only be used on a dedicated server");
                    return;
                }

                if (args.Count == 0)
                {
                    Console.LogWarning($"Usage: {ExampleUsage}");
                    return;
                }

                string playerName = args[0];
                string reason = args.Count > 1 ? string.Join(" ", args.Skip(1)) : "No reason specified";

                Player targetPlayer = FindPlayerByNameOrId(playerName);
                if (targetPlayer != null)
                {
                    Console.Log($"Kicking player {targetPlayer.PlayerName}: {reason}");
                    
                    // Use ServerManager to kick the player
                    var connectedPlayer = ServerManager.GetPlayer(targetPlayer.Owner);
                    if (connectedPlayer != null)
                    {
                        ServerManager.KickPlayer(connectedPlayer, reason);
                    }
                    else
                    {
                        // Fallback to direct disconnect
                        targetPlayer.Owner.Disconnect(true);
                        ServerManager.BroadcastMessage($"{targetPlayer.PlayerName} was kicked: {reason}");
                    }
                }
                else
                {
                    Console.LogWarning($"Player not found: {playerName}");
                }
            }
        }

        public class BanPlayerCommand : Console.ConsoleCommand
        {
            public override string CommandWord => "ban";
            public override string CommandDescription => "Bans a player from the server (placeholder - requires implementation)";
            public override string ExampleUsage => "ban <player_name> [reason]";

            public override void Execute(List<string> args)
            {
                if (!InstanceFinder.IsServer)
                {
                    Console.LogWarning("This command can only be used on a dedicated server");
                    return;
                }

                if (args.Count == 0)
                {
                    Console.LogWarning($"Usage: {ExampleUsage}");
                    return;
                }

                // TODO: Implement ban system with persistence
                Console.LogWarning("Ban system not yet implemented - use kick for now");
            }
        }

        public class UnbanPlayerCommand : Console.ConsoleCommand
        {
            public override string CommandWord => "unban";
            public override string CommandDescription => "Unbans a player from the server (placeholder - requires implementation)";
            public override string ExampleUsage => "unban <steamid>";

            public override void Execute(List<string> args)
            {
                if (!InstanceFinder.IsServer)
                {
                    Console.LogWarning("This command can only be used on a dedicated server");
                    return;
                }

                if (args.Count == 0)
                {
                    Console.LogWarning($"Usage: {ExampleUsage}");
                    return;
                }

                // TODO: Implement unban system
                Console.LogWarning("Ban system not yet implemented");
            }
        }

        #endregion

        #region Utility Methods

        private static Player FindPlayerByNameOrId(string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return null;

            // First try to find by exact name match
            foreach (var player in Player.PlayerList)
            {
                if (player?.PlayerName?.Equals(identifier, StringComparison.OrdinalIgnoreCase) == true)
                    return player;
            }

            // Then try partial name match
            foreach (var player in Player.PlayerList)
            {
                if (player?.PlayerName?.Contains(identifier, StringComparison.OrdinalIgnoreCase) == true)
                    return player;
            }

            // Finally try by Steam ID
            foreach (var player in Player.PlayerList)
            {
                string steamId = ServerConfig.GetPlayerSteamId(player);
                if (steamId == identifier)
                    return player;
            }

            return null;
        }

        #endregion
    }
}
