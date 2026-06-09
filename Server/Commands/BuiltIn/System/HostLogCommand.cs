using System.Text;
using DedicatedServerMod.Server.Commands.Contracts;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Shared.Permissions;
using MelonLoader.Utils;

namespace DedicatedServerMod.Server.Commands.BuiltIn.System
{
    /// <summary>
    /// Prints recent host log output for console operators.
    /// </summary>
    internal abstract class HostLogCommand : BaseServerCommand
    {
        private const int DefaultLineCount = 80;
        private const int MaxLineCount = 200;
        private const int MaxBytesToRead = 256 * 1024;
        private const string LogFileName = "Latest.log";

        protected HostLogCommand(PlayerManager playerMgr)
            : base(playerMgr)
        {
        }

        public override string Description => "Show recent MelonLoader server log output.";

        public override string Usage => $"{CommandWord} [lines]";

        public override string RequiredPermissionNode => PermissionNode.CreateConsoleCommandNode(CommandWord);

        public override void Execute(CommandContext context)
        {
            if (context == null)
            {
                return;
            }

            if (!context.IsConsoleExecution)
            {
                context.ReplyError("Log viewing is only available from the host console.");
                return;
            }

            int lineCount = ParseLineCount(context);
            IReadOnlyList<string> candidates = GetCandidateLogPaths();
            string logPath = candidates.FirstOrDefault(File.Exists);
            if (string.IsNullOrWhiteSpace(logPath))
            {
                context.ReplyWarning("No MelonLoader log file was found yet.");
                context.Reply("Checked:");
                foreach (string candidate in candidates)
                {
                    context.Reply($" - {candidate}");
                }

                context.Reply("If your host panel proxies the TCP console, wait for startup to finish or ask the host to launch with -logFile - for direct stdout logging.");
                return;
            }

            List<string> lines;
            try
            {
                lines = ReadTailLines(logPath, lineCount);
            }
            catch (Exception ex)
            {
                context.ReplyError($"Failed to read log file: {ex.Message}");
                context.Reply($"Path: {logPath}");
                return;
            }

            if (lines.Count == 0)
            {
                context.Reply($"Log file is empty: {logPath}");
                return;
            }

            context.Reply($"Showing last {lines.Count} line(s) from {logPath}");
            foreach (string line in lines)
            {
                context.Reply(line);
            }
        }

        private static int ParseLineCount(CommandContext context)
        {
            if (context.Arguments == null || context.Arguments.Count == 0)
            {
                return DefaultLineCount;
            }

            string rawValue = context.Arguments[0];
            if (!int.TryParse(rawValue, out int requested) || requested <= 0)
            {
                context.ReplyWarning($"Invalid line count '{rawValue}', using {DefaultLineCount}.");
                return DefaultLineCount;
            }

            if (requested > MaxLineCount)
            {
                context.ReplyWarning($"Line count capped at {MaxLineCount}.");
                return MaxLineCount;
            }

            return requested;
        }

        private static IReadOnlyList<string> GetCandidateLogPaths()
        {
            List<string> candidates = new List<string>();

            AddCandidate(candidates, Path.Combine(AppContext.BaseDirectory, "MelonLoader", LogFileName));
            AddCandidate(candidates, Path.Combine(AppContext.BaseDirectory, "UserData", "MelonLoader", LogFileName));
            AddCandidate(candidates, Path.Combine(Environment.CurrentDirectory, "MelonLoader", LogFileName));
            AddCandidate(candidates, Path.Combine(Environment.CurrentDirectory, "UserData", "MelonLoader", LogFileName));

            string userDataDirectory = MelonEnvironment.UserDataDirectory;
            if (!string.IsNullOrWhiteSpace(userDataDirectory))
            {
                AddCandidate(candidates, Path.Combine(userDataDirectory, "MelonLoader", LogFileName));
                DirectoryInfo userDataInfo = Directory.GetParent(userDataDirectory);
                if (userDataInfo != null)
                {
                    AddCandidate(candidates, Path.Combine(userDataInfo.FullName, "MelonLoader", LogFileName));
                    AddCandidate(candidates, Path.Combine(userDataInfo.FullName, "UserData", "MelonLoader", LogFileName));
                }
            }

            return candidates;
        }

        private static void AddCandidate(List<string> candidates, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch
            {
                return;
            }

            if (!candidates.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
            {
                candidates.Add(fullPath);
            }
        }

        private static List<string> ReadTailLines(string path, int lineCount)
        {
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            long bytesToRead = Math.Min(stream.Length, MaxBytesToRead);
            if (bytesToRead <= 0)
            {
                return new List<string>();
            }

            stream.Seek(-bytesToRead, SeekOrigin.End);
            byte[] buffer = new byte[(int)bytesToRead];
            int read = stream.Read(buffer, 0, buffer.Length);
            string text = Encoding.UTF8.GetString(buffer, 0, read).Replace("\r\n", "\n").Replace('\r', '\n');
            string[] allLines = text.Split('\n');
            int start = Math.Max(0, allLines.Length - lineCount);

            List<string> result = new List<string>(lineCount);
            if (bytesToRead == MaxBytesToRead && start == 0 && allLines.Length > 0)
            {
                allLines[0] = "..." + allLines[0];
            }

            for (int i = start; i < allLines.Length; i++)
            {
                if (i == allLines.Length - 1 && allLines[i].Length == 0)
                {
                    continue;
                }

                result.Add(allLines[i]);
            }

            return result;
        }
    }

    /// <summary>
    /// Prints recent host log output.
    /// </summary>
    internal sealed class LogsCommand : HostLogCommand
    {
        public LogsCommand(PlayerManager playerMgr)
            : base(playerMgr)
        {
        }

        public override string CommandWord => "logs";
    }

    /// <summary>
    /// Prints the tail of recent host log output.
    /// </summary>
    internal sealed class TailCommand : HostLogCommand
    {
        public TailCommand(PlayerManager playerMgr)
            : base(playerMgr)
        {
        }

        public override string CommandWord => "tail";
    }
}
