using System;
using System.Collections.Generic;
using DedicatedServerMod.Server.Commands.Output;

namespace DedicatedServerMod.Server.WebPanel
{
    /// <summary>
    /// Captures structured command replies for the embedded web panel.
    /// </summary>
    internal sealed class WebPanelCommandReplyChannel : ICommandReplyChannel
    {
        private readonly WebPanelEventStream _eventStream;
        private readonly WebPanelLogBuffer _logBuffer;

        /// <summary>
        /// Initializes a new web panel command reply channel.
        /// </summary>
        public WebPanelCommandReplyChannel(WebPanelEventStream eventStream, WebPanelLogBuffer logBuffer)
        {
            _eventStream = eventStream ?? throw new ArgumentNullException(nameof(eventStream));
            _logBuffer = logBuffer ?? throw new ArgumentNullException(nameof(logBuffer));
        }

        /// <summary>
        /// Gets the captured output lines for this command invocation.
        /// </summary>
        public List<WebPanelCommandOutputLine> Lines { get; } = new List<WebPanelCommandOutputLine>();

        /// <inheritdoc />
        public void Write(CommandReplyLine line)
        {
            foreach (CommandReplyLine expandedLine in CommandReplyRenderer.Expand(line))
            {
                string level = ToLevelString(expandedLine.Level);
                WebPanelCommandOutputLine outputLine = new WebPanelCommandOutputLine
                {
                    Level = level,
                    Message = expandedLine.Message
                };

                Lines.Add(outputLine);

                WebPanelLogEntry logEntry = new WebPanelLogEntry
                {
                    TimestampUtc = DateTime.UtcNow,
                    Level = level,
                    Message = expandedLine.Message,
                    Source = "console"
                };

                _logBuffer.Add(logEntry);
                _eventStream.Publish("log.append", logEntry);
                _eventStream.Publish("console.output", outputLine);
            }
        }

        private static string ToLevelString(CommandReplyLevel level)
        {
            switch (level)
            {
                case CommandReplyLevel.Warning:
                    return "warning";
                case CommandReplyLevel.Error:
                    return "error";
                default:
                    return "info";
            }
        }
    }
}
