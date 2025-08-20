using System;
using System.Linq;
using System.Text;
using DedicatedServerMod.Server.Player;

namespace DedicatedServerMod.Server.Commands.Server
{
	/// <summary>
	/// Lists available commands and shows detailed help for a specific command.
	/// </summary>
	public class HelpCommand : BaseServerCommand
	{
		private readonly CommandManager commandManager;

		public HelpCommand(MelonLoader.MelonLogger.Instance loggerInstance, PlayerManager playerMgr, CommandManager manager)
			: base(loggerInstance, playerMgr)
		{
			commandManager = manager ?? throw new ArgumentNullException(nameof(manager));
		}

		public override string CommandWord => "help";
		public override string Description => "List available commands or show help for a specific command.";
		public override string Usage => "help [command]";
		public override PermissionLevel RequiredPermission => PermissionLevel.Player;

		public override void Execute(CommandContext context)
		{
			var args = context.Arguments ?? new System.Collections.Generic.List<string>();
			if (args.Count == 0)
			{
				var commands = commandManager
					.GetAllCommands()
					.Values
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
			var command = commandManager.GetCommand(target);
			if (command == null)
			{
				context.ReplyWarning($"Unknown command '{target}'. Type 'help' for a list of commands.");
				return;
			}

			context.Reply($"{command.CommandWord}: {command.Description}\nUsage: {command.Usage}");
		}
	}
}


