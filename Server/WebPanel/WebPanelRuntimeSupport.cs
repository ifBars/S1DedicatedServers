using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using DedicatedServerMod.Server.Commands;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Commands.Output;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DedicatedServerMod.Server.WebPanel
{
    /// <summary>
    /// Provides consistent JSON serialization rules for browser-panel payloads.
    /// </summary>
    internal static class WebPanelJson
    {
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Include
        };

        public static string Serialize(object payload)
        {
            return JsonConvert.SerializeObject(payload ?? new { }, JsonSettings);
        }
    }

    /// <summary>
    /// Stores recent browser-panel logs in memory for quick bootstrap hydration.
    /// </summary>
    internal sealed class WebPanelLogBuffer(int capacity)
    {
        private readonly object _sync = new object();
        private readonly LinkedList<WebPanelLogEntry> _entries = new LinkedList<WebPanelLogEntry>();
        private readonly int _capacity = Math.Max(10, capacity);

        public void Add(WebPanelLogEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            lock (_sync)
            {
                _entries.AddLast(entry);
                while (_entries.Count > _capacity)
                {
                    _entries.RemoveFirst();
                }
            }
        }

        public List<WebPanelLogEntry> GetSnapshot()
        {
            lock (_sync)
            {
                return _entries.ToList();
            }
        }
    }

    /// <summary>
    /// Broadcasts server-side events to browser clients over Server-Sent Events.
    /// </summary>
    internal sealed class WebPanelEventStream
    {
        private readonly object _sync = new object();
        private readonly List<WebPanelEventSubscription> _subscriptions = new List<WebPanelEventSubscription>();

        public WebPanelEventSubscription Subscribe()
        {
            WebPanelEventSubscription subscription = new WebPanelEventSubscription();
            lock (_sync)
            {
                _subscriptions.Add(subscription);
            }

            return subscription;
        }

        public void Unsubscribe(WebPanelEventSubscription subscription)
        {
            if (subscription == null)
            {
                return;
            }

            lock (_sync)
            {
                _subscriptions.Remove(subscription);
            }

            subscription.Dispose();
        }

        public void Publish(string eventName, object payload)
        {
            string serializedPayload = WebPanelJson.Serialize(payload);
            string message = BuildMessage(eventName, serializedPayload);
            WebPanelEventSubscription[] subscriptions;

            lock (_sync)
            {
                subscriptions = _subscriptions.ToArray();
            }

            for (int i = 0; i < subscriptions.Length; i++)
            {
                subscriptions[i].Enqueue(message);
            }
        }

        private static string BuildMessage(string eventName, string payload)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("event: ");
            builder.Append(string.IsNullOrWhiteSpace(eventName) ? "message" : eventName);
            builder.Append("\n");

            string[] payloadLines = (payload ?? string.Empty).Split('\n');
            for (int i = 0; i < payloadLines.Length; i++)
            {
                builder.Append("data: ");
                builder.Append(payloadLines[i]);
                builder.Append("\n");
            }

            builder.Append("\n");
            return builder.ToString();
        }
    }

    /// <summary>
    /// Represents a browser-panel event stream subscription.
    /// </summary>
    internal sealed class WebPanelEventSubscription : IDisposable
    {
        private readonly BlockingCollection<string> _queue = new BlockingCollection<string>();

        public bool TryTake(out string message, int timeoutMilliseconds)
        {
            return _queue.TryTake(out message, timeoutMilliseconds);
        }

        public void Enqueue(string message)
        {
            if (!_queue.IsAddingCompleted)
            {
                try
                {
                    _queue.Add(message ?? string.Empty);
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        public void Dispose()
        {
            if (!_queue.IsAddingCompleted)
            {
                _queue.CompleteAdding();
            }

            _queue.Dispose();
        }
    }

    /// <summary>
    /// Manages one-time launch tokens and localhost-only browser sessions.
    /// </summary>
    internal sealed class WebPanelSessionService
    {
        private const string SessionCookieName = "s1ds_panel_session";

        private readonly object _sync = new object();
        private readonly Dictionary<string, DateTime> _sessions = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        private readonly int _sessionMinutes;

        private string _launchToken = string.Empty;
        private bool _launchTokenConsumed;

        public WebPanelSessionService(int sessionMinutes)
        {
            _sessionMinutes = Math.Max(1, sessionMinutes);
            RotateLaunchToken();
        }

        public string LaunchToken
        {
            get
            {
                lock (_sync)
                {
                    return _launchToken;
                }
            }
        }

        public bool TryExchangeLaunchToken(string token, out string sessionId, out DateTime expiresAtUtc)
        {
            lock (_sync)
            {
                PruneExpiredSessions();
                if (_launchTokenConsumed || !string.Equals(token ?? string.Empty, _launchToken, StringComparison.Ordinal))
                {
                    sessionId = string.Empty;
                    expiresAtUtc = DateTime.MinValue;
                    return false;
                }

                _launchTokenConsumed = true;
                sessionId = GenerateToken();
                expiresAtUtc = DateTime.UtcNow.AddMinutes(_sessionMinutes);
                _sessions[sessionId] = expiresAtUtc;
                return true;
            }
        }

        public bool TryValidateSession(string sessionId, out DateTime expiresAtUtc)
        {
            lock (_sync)
            {
                PruneExpiredSessions();
                if (!string.IsNullOrWhiteSpace(sessionId) && _sessions.TryGetValue(sessionId, out expiresAtUtc))
                {
                    return true;
                }

                expiresAtUtc = DateTime.MinValue;
                return false;
            }
        }

        public string CreateSessionCookie(string sessionId, DateTime expiresAtUtc)
        {
            return $"{SessionCookieName}={sessionId}; Path=/; HttpOnly; SameSite=Strict; Max-Age={Math.Max(60, (int)(expiresAtUtc - DateTime.UtcNow).TotalSeconds)}";
        }

        public string GetSessionIdFromCookie(IDictionary<string, string> cookies)
        {
            if (cookies == null)
            {
                return string.Empty;
            }

            return cookies.TryGetValue(SessionCookieName, out string sessionId) ? sessionId ?? string.Empty : string.Empty;
        }

        public string BuildLaunchUrl(string bindAddress, int port)
        {
            string host = string.IsNullOrWhiteSpace(bindAddress) ? "127.0.0.1" : bindAddress.Trim();
            if (string.Equals(host, "0.0.0.0", StringComparison.Ordinal))
            {
                host = "127.0.0.1";
            }

            if (host.Contains(":") && !host.StartsWith("[", StringComparison.Ordinal))
            {
                host = $"[{host}]";
            }

            return $"http://{host}:{port}/?token={Uri.EscapeDataString(LaunchToken)}";
        }

        public void RotateLaunchToken()
        {
            lock (_sync)
            {
                _launchToken = GenerateToken();
                _launchTokenConsumed = false;
            }
        }

        private void PruneExpiredSessions()
        {
            DateTime now = DateTime.UtcNow;
            string[] expired = _sessions.Where(pair => pair.Value <= now).Select(pair => pair.Key).ToArray();
            for (int i = 0; i < expired.Length; i++)
            {
                _sessions.Remove(expired[i]);
            }
        }

        private static string GenerateToken()
        {
            byte[] bytes = new byte[32];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }

    /// <summary>
    /// Resolves the embedded static files generated by the Bun/Vite workspace.
    /// </summary>
    internal sealed class WebPanelStaticFileProvider
    {
        private readonly Dictionary<string, string> _resourceMap;

        public WebPanelStaticFileProvider()
        {
            _resourceMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string prefix = typeof(WebPanelStaticFileProvider).Namespace + ".Static.";
            string[] resourceNames = typeof(WebPanelStaticFileProvider).Assembly.GetManifestResourceNames();
            for (int i = 0; i < resourceNames.Length; i++)
            {
                string resourceName = resourceNames[i];
                if (!resourceName.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                string relativePath = resourceName.Substring(prefix.Length);
                if (string.Equals(relativePath, "index.html", StringComparison.OrdinalIgnoreCase))
                {
                    _resourceMap["/index.html"] = resourceName;
                    _resourceMap["/"] = resourceName;
                    continue;
                }

                if (relativePath.StartsWith("assets.", StringComparison.OrdinalIgnoreCase))
                {
                    string assetName = relativePath.Substring("assets.".Length);
                    if (!string.IsNullOrWhiteSpace(assetName))
                    {
                        _resourceMap["/assets/" + assetName] = resourceName;
                    }

                    continue;
                }
            }
        }

        public bool TryRead(string path, out byte[] content, out string contentType)
        {
            string normalizedPath = string.IsNullOrWhiteSpace(path) ? "/" : path;
            if (!_resourceMap.TryGetValue(normalizedPath, out string resourceName))
            {
                if (!normalizedPath.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
                {
                    _resourceMap.TryGetValue("/", out resourceName);
                }
            }

            if (string.IsNullOrWhiteSpace(resourceName))
            {
                content = null;
                contentType = "text/plain; charset=utf-8";
                return false;
            }

            using (Stream resourceStream = typeof(WebPanelStaticFileProvider).Assembly.GetManifestResourceStream(resourceName))
            {
                if (resourceStream == null)
                {
                    content = null;
                    contentType = "text/plain; charset=utf-8";
                    return false;
                }

                using (MemoryStream memoryStream = new MemoryStream())
                {
                    resourceStream.CopyTo(memoryStream);
                    content = memoryStream.ToArray();
                }
            }

            contentType = GetContentType(normalizedPath);
            return true;
        }

        public byte[] BuildMissingAssetPage()
        {
            const string html = "<!doctype html><html><head><meta charset=\"utf-8\"><title>Web Panel Build Missing</title></head><body><h1>Web panel assets are missing.</h1><p>Run <code>bun install</code> and <code>bun run build</code> in the <code>webpanel</code> workspace, then rebuild the mod.</p></body></html>";
            return Encoding.UTF8.GetBytes(html);
        }

        private static string GetContentType(string path)
        {
            if (path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
            {
                return "image/x-icon";
            }

            if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                return "image/png";
            }

            if (path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                return "image/svg+xml";
            }

            if (path.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
            {
                return "text/css; charset=utf-8";
            }

            if (path.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
            {
                return "application/javascript; charset=utf-8";
            }

            if (path.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || string.Equals(path, "/", StringComparison.Ordinal))
            {
                return "text/html; charset=utf-8";
            }

            return "application/octet-stream";
        }
    }

    /// <summary>
    /// Executes server commands for browser-panel callers and captures line output.
    /// </summary>
    internal sealed class WebPanelCommandBridge(
        CommandManager commandManager,
        WebPanelEventStream eventStream,
        WebPanelLogBuffer logBuffer)
    {
        private readonly CommandManager _commandManager = commandManager ?? throw new ArgumentNullException(nameof(commandManager));
        private readonly WebPanelEventStream _eventStream = eventStream ?? throw new ArgumentNullException(nameof(eventStream));
        private readonly WebPanelLogBuffer _logBuffer = logBuffer ?? throw new ArgumentNullException(nameof(logBuffer));

        public WebPanelCommandResult Execute(string commandLine)
        {
            BrowserCommandOutput output = new BrowserCommandOutput(_eventStream, _logBuffer);
            CommandExecutionResult executionResult = _commandManager.ExecuteConsoleLine(commandLine ?? string.Empty, output);

            return new WebPanelCommandResult
            {
                Succeeded = executionResult.Succeeded,
                Status = executionResult.Status.ToString(),
                CommandWord = executionResult.CommandWord ?? string.Empty,
                Message = executionResult.Message ?? string.Empty,
                Output = output.Lines
            };
        }

        private sealed class BrowserCommandOutput(WebPanelEventStream eventStream, WebPanelLogBuffer logBuffer)
            : ICommandOutput
        {
            public List<WebPanelCommandOutputLine> Lines { get; } = new();

            public void WriteInfo(string message)
            {
                Add("info", message);
            }

            public void WriteWarning(string message)
            {
                Add("warning", message);
            }

            public void WriteError(string message)
            {
                Add("error", message);
            }

            private void Add(string level, string message)
            {
                WebPanelCommandOutputLine line = new WebPanelCommandOutputLine
                {
                    Level = level,
                    Message = message ?? string.Empty
                };

                Lines.Add(line);

                WebPanelLogEntry logEntry = new WebPanelLogEntry
                {
                    TimestampUtc = DateTime.UtcNow,
                    Level = level,
                    Message = message ?? string.Empty,
                    Source = "console"
                };

                logBuffer.Add(logEntry);
                eventStream.Publish("log.append", logEntry);
                eventStream.Publish("console.output", line);
            }
        }
    }
}
