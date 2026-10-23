using UnityEngine;

namespace DedicatedServerMod.Server.Persistence
{
    /// <summary>
    /// Collects native persistence errors without retaining serialized save contents.
    /// </summary>
    internal sealed class NativeSaveDiagnostics
    {
        private const int MaxMessages = 8;
        private const int MaxMessageLength = 2048;
        private readonly List<string> _messages = new List<string>();

        internal int ErrorCount { get; private set; }

        internal IReadOnlyList<string> Messages => _messages;

        internal void Capture(string message, string stackTrace, LogType type)
        {
            if ((type != LogType.Error && type != LogType.Exception) || string.IsNullOrEmpty(message))
                return;

            // ISaveable and SaveRequest can log failures without setting SaveManager.SaveError.
            // Match their messages too, since players may disable Unity stack traces.
            bool persistenceError = stackTrace?.IndexOf("ScheduleOne.Persistence.", StringComparison.Ordinal) >= 0 ||
                message.StartsWith("Failed to write save ", StringComparison.Ordinal) ||
                message.StartsWith("Failed to write sub file.", StringComparison.Ordinal) ||
                message.StartsWith("Failed to write sub folder.", StringComparison.Ordinal) ||
                message.StartsWith("Failed to write subfile:", StringComparison.Ordinal) ||
                message.StartsWith("Error generating save string for ", StringComparison.Ordinal) ||
                message.StartsWith("Failed to save base saveable:", StringComparison.Ordinal) ||
                message.StartsWith("Error completing save request for ", StringComparison.Ordinal) ||
                message.StartsWith("Save completed with errors!", StringComparison.Ordinal);
            if (!persistenceError)
                return;

            ErrorCount++;
            if (_messages.Count >= MaxMessages)
                return;

            int dataStart = message.IndexOf("\nSave string:", StringComparison.Ordinal);
            if (dataStart >= 0)
                message = message.Substring(0, dataStart).TrimEnd('\r');
            if (message.Length > MaxMessageLength)
                message = message.Substring(0, MaxMessageLength) + " [truncated]";
            _messages.Add(message);
        }
    }
}
