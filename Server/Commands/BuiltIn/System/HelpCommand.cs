using System.Text;
using DedicatedServerMod.Server.Commands;
using DedicatedServerMod.Server.Commands.Contracts;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Player;

namespace DedicatedServerMod.Server.Commands.BuiltIn.System
{
	/// <summary>
	/// Lists available commands and shows detailed help for a specific command.
	/// </summary>
	public class HelpCommand(PlayerManager playerMgr, CommandManager manager) : BaseServerCommand(playerMgr)
	{
		private readonly CommandManager commandManager = manager ?? throw new ArgumentNullException(nameof(manager));

		public override string CommandWord => "help";
		public override string Description => "List available commands or show help for a specific command.";
		public override string Usage => "help [command]";
		public override string RequiredPermissionNode => Shared.Permissions.PermissionBuiltIns.Nodes.ServerHelp;

		public override void Execute(CommandContext context)
		{
			var args = context.Arguments ?? new List<string>();
			if (args.Count == 0)
			{
				var commands = commandManager
					.GetAvailableCommands(context.Executor)
					.OrderBy(c => c.CommandWord)
					.ToList();

				var sb = new StringBuilder();
				sb.AppendLine("Available commands:");
				foreach (var cmd in commands)
				{
					sb.Append(" - ")
						.Append(cmd.CommandWord)
						.Append(": ")
						.AppendLine(cmd.Description);
				}
				sb.AppendLine("Use 'help <command>' for details.");
				context.Reply(sb.ToString().TrimEnd());
				return;
			}

			// Detailed help for a specific command
			string target = args[0].ToLowerInvariant();
			var command = commandManager
				.GetAvailableCommands(context.Executor)
				.FirstOrDefault(candidate => string.Equals(candidate.CommandWord, target, StringComparison.OrdinalIgnoreCase));
			if (command == null)
			{
				context.ReplyWarning($"Unknown or unavailable command '{target}'. Type 'help' for a list of commands.");
				return;
			}

			context.Reply($"{command.CommandWord}: {command.Description}\nUsage: {command.Usage}");
		}
	}
}


