using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using DedicatedServerMod.Server.Core;
using DedicatedServerMod.Server.Persistence;
using DedicatedServerMod.Server.Permissions;
using DedicatedServerMod.Shared.Configuration;
using DedicatedServerMod.Utils;
using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace DedicatedServerMod.Server.WebPanel
{
    /// <summary>
    /// Hosts the localhost-only browser panel API and static SPA assets.
    /// </summary>
    internal sealed class WebPanelHttpHost(
        MelonLogger.Instance logger,
        WebPanelSnapshotService snapshotService,
        WebPanelSessionService sessionService,
        WebPanelEventStream eventStream,
        WebPanelStaticFileProvider staticFileProvider,
        WebPanelCommandBridge commandBridge,
        WebPanelLogBuffer logBuffer,
        ServerPermissionService permissionService,
        PersistenceManager persistenceManager)
        : IDisposable
    {
        private readonly MelonLogger.Instance _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly WebPanelSnapshotService _snapshotService = snapshotService ?? throw new ArgumentNullException(nameof(snapshotService));
        private readonly WebPanelSessionService _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        private readonly WebPanelEventStream _eventStream = eventStream ?? throw new ArgumentNullException(nameof(eventStream));
        private readonly WebPanelStaticFileProvider _staticFileProvider = staticFileProvider ?? throw new ArgumentNullException(nameof(staticFileProvider));
        private readonly WebPanelCommandBridge _commandBridge = commandBridge ?? throw new ArgumentNullException(nameof(commandBridge));
        private readonly WebPanelLogBuffer _logBuffer = logBuffer ?? throw new ArgumentNullException(nameof(logBuffer));
        private readonly ServerPermissionService _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
        private readonly PersistenceManager _persistenceManager = persistenceManager ?? throw new ArgumentNullException(nameof(persistenceManager));

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;
        private volatile bool _isRunning;

        public string LaunchUrl { get; private set; } = string.Empty;

        public void Start()
        {
            if (_isRunning)
            {
                return;
            }

            ServerConfig config = ServerConfig.Instance;
            IPAddress address = ParseLoopbackAddress(config.WebPanelBindAddress);
            _listener = new TcpListener(address, config.WebPanelPort);
            _listener.Start();
            _isRunning = true;
            LaunchUrl = _sessionService.BuildLaunchUrl(address.ToString(), config.WebPanelPort);
            _listenerCancellation = new CancellationTokenSource();
            _listenerTask = AcceptLoopAsync(_listener, _listenerCancellation.Token);
        }

        public void Dispose()
        {
            _isRunning = false;
            _listenerCancellation?.Cancel();

            try
            {
                _listener?.Stop();
            }
            catch
            {
            }
        }

        private async Task AcceptLoopAsync(TcpListener listener, CancellationToken cancellationToken)
        {
            try
            {
                while (_isRunning && !cancellationToken.IsCancellationRequested)
                {
                    TcpClient client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    _ = HandleClientAsync(client, cancellationToken);
                }
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (_isRunning)
                {
                    _logger.Warning($"Web panel accept loop error: {ex.Message}");
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                try
                {
                    client.ReceiveTimeout = Constants.TcpConsoleSocketTimeoutMs;
                    client.SendTimeout = Constants.TcpConsoleSocketTimeoutMs;

                    if (!HttpRequestData.TryRead(stream, out HttpRequestData request))
                    {
                        return;
                    }

                    if (!IsLoopbackClient(client))
                    {
                        WriteStatus(stream, 403, "Forbidden", "Loopback access only.");
                        return;
                    }

                    if (string.Equals(request.Path, "/api/session/exchange-token", StringComparison.OrdinalIgnoreCase))
                    {
                        HandleSessionExchange(stream, request);
                        return;
                    }

                    if (request.Path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryAuthenticate(request, out DateTime sessionExpiresAtUtc))
                        {
                            WriteStatus(stream, 401, "Unauthorized", "A valid localhost panel session is required.");
                            return;
                        }

                        await RouteApiRequestAsync(stream, request, sessionExpiresAtUtc, cancellationToken).ConfigureAwait(false);
                        return;
                    }

                    ServeStatic(stream, request.Path);
                }
                catch (Exception ex)
                {
                    _logger.Warning($"Web panel request failed: {ex.Message}");
                    TryWriteInternalServerError(stream, ex.Message);
                }
            }
        }

        private async Task RouteApiRequestAsync(NetworkStream stream, HttpRequestData request, DateTime sessionExpiresAtUtc, CancellationToken cancellationToken)
        {
            if (request.Method == "GET" && string.Equals(request.Path, "/api/bootstrap", StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(stream, 200, new WebPanelBootstrapPayload
                {
                    Version = Constants.FullVersion,
                    ConfigFilePath = ServerConfig.ConfigFilePath,
                    PermissionsFilePath = _permissionService.FilePath,
                    UserDataPath = MelonEnvironment.UserDataDirectory,
                    SessionExpiresAtUtc = sessionExpiresAtUtc,
                    Overview = _snapshotService.CreateOverview(),
                    Config = _snapshotService.CreateConfigSnapshot(),
                    RecentLogs = ServerConfig.Instance.WebPanelExposeLogs ? _logBuffer.GetSnapshot() : new List<WebPanelLogEntry>()
                });
                return;
            }

            if (request.Method == "GET" && string.Equals(request.Path, "/api/overview", StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(stream, 200, _snapshotService.CreateOverview());
                return;
            }

            if (request.Method == "GET" && string.Equals(request.Path, "/api/players", StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(stream, 200, _snapshotService.CreatePlayers());
                return;
            }

            if (request.Method == "GET" && string.Equals(request.Path, "/api/config", StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(stream, 200, _snapshotService.CreateConfigSnapshot());
                return;
            }

            if (request.Method == "POST" && string.Equals(request.Path, "/api/config", StringComparison.OrdinalIgnoreCase))
            {
                WebPanelConfigSnapshot snapshot = DeserializeBody<WebPanelConfigSnapshot>(request.Body);
                WebPanelConfigSnapshot updatedSnapshot = _snapshotService.ApplyConfigSnapshot(snapshot);
                _eventStream.Publish("config.changed", updatedSnapshot);
                _eventStream.Publish("overview.changed", _snapshotService.CreateOverview());
                WriteJson(stream, 200, updatedSnapshot);
                return;
            }

            if (request.Method == "POST" && string.Equals(request.Path, "/api/actions/save", StringComparison.OrdinalIgnoreCase))
            {
                _persistenceManager.TriggerManualSave("manual_save_from_web_panel");
                _eventStream.Publish("save.changed", new { status = "triggered", reason = "manual_save_from_web_panel" });
                WriteJson(stream, 202, new { accepted = true });
                return;
            }

            if (request.Method == "POST" && string.Equals(request.Path, "/api/actions/reload-config", StringComparison.OrdinalIgnoreCase))
            {
                ServerConfig.ReloadConfig();
                _permissionService.Reload();
                WebPanelConfigSnapshot snapshot = _snapshotService.CreateConfigSnapshot();
                _eventStream.Publish("config.changed", snapshot);
                _eventStream.Publish("overview.changed", _snapshotService.CreateOverview());
                WriteJson(stream, 200, snapshot);
                return;
            }

            if (request.Method == "POST" && string.Equals(request.Path, "/api/actions/shutdown", StringComparison.OrdinalIgnoreCase))
            {
                ServerBootstrap.RequestShutdown("Web panel shutdown requested");
                WriteJson(stream, 202, new { accepted = true });
                return;
            }

            if (request.Method == "POST" && string.Equals(request.Path, "/api/console/execute", StringComparison.OrdinalIgnoreCase))
            {
                WebPanelCommandRequest commandRequest = DeserializeBody<WebPanelCommandRequest>(request.Body);
                WebPanelCommandResult result = _commandBridge.Execute(commandRequest?.CommandLine ?? string.Empty);
                _eventStream.Publish("overview.changed", _snapshotService.CreateOverview());
                WriteJson(stream, 200, result);
                return;
            }

            if (request.Method == "GET" && string.Equals(request.Path, "/api/events", StringComparison.OrdinalIgnoreCase))
            {
                await ServeEventStreamAsync(stream, cancellationToken).ConfigureAwait(false);
                return;
            }

            WriteStatus(stream, 404, "Not Found", "Endpoint not found.");
        }

        private void HandleSessionExchange(NetworkStream stream, HttpRequestData request)
        {
            if (!string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase))
            {
                WriteStatus(stream, 405, "Method Not Allowed", "Use POST.");
                return;
            }

            WebPanelTokenRequest tokenRequest = DeserializeBody<WebPanelTokenRequest>(request.Body);
            if (!_sessionService.TryExchangeLaunchToken(tokenRequest?.Token ?? string.Empty, out string sessionId, out DateTime expiresAtUtc))
            {
                WriteStatus(stream, 401, "Unauthorized", "Launch token is invalid or has already been used.");
                return;
            }

            WriteJson(stream, 200, new { authenticated = true, expiresAtUtc }, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Set-Cookie"] = _sessionService.CreateSessionCookie(sessionId, expiresAtUtc)
            });
        }

        private bool TryAuthenticate(HttpRequestData request, out DateTime sessionExpiresAtUtc)
        {
            string sessionId = _sessionService.GetSessionIdFromCookie(request.Cookies);
            return _sessionService.TryValidateSession(sessionId, out sessionExpiresAtUtc);
        }

        private void ServeStatic(NetworkStream stream, string path)
        {
            if (_staticFileProvider.TryRead(path, out byte[] content, out string contentType))
            {
                WriteBytes(stream, 200, "OK", content, contentType);
                return;
            }

            byte[] fallback = _staticFileProvider.BuildMissingAssetPage();
            WriteBytes(stream, 503, "Service Unavailable", fallback, "text/html; charset=utf-8");
        }

        private async Task ServeEventStreamAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            WebPanelEventSubscription subscription = _eventStream.Subscribe();
            try
            {
                WriteRaw(
                    stream,
                    "HTTP/1.1 200 OK\r\n" +
                    "Content-Type: text/event-stream\r\n" +
                    "Cache-Control: no-cache\r\n" +
                    "Connection: keep-alive\r\n" +
                    "X-Accel-Buffering: no\r\n\r\n");

                using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true))
                {
                    await writer.WriteAsync("event: connected\ndata: {}\n\n").ConfigureAwait(false);
                    await writer.FlushAsync().ConfigureAwait(false);

                    while (_isRunning)
                    {
                        string message = await subscription.WaitForMessageAsync(15000, cancellationToken).ConfigureAwait(false);
                        if (!_isRunning)
                        {
                            break;
                        }

                        if (message != null)
                        {
                            await writer.WriteAsync(message).ConfigureAwait(false);
                        }
                        else
                        {
                            await writer.WriteAsync(": keepalive\n\n").ConfigureAwait(false);
                        }

                        await writer.FlushAsync().ConfigureAwait(false);
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _eventStream.Unsubscribe(subscription);
            }
        }

        private static T DeserializeBody<T>(string body)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Invalid JSON payload: {ex.Message}", ex);
            }
        }

        private static void WriteJson(NetworkStream stream, int statusCode, object payload, IDictionary<string, string> extraHeaders = null)
        {
            string json = WebPanelJson.Serialize(payload);
            WriteBytes(stream, statusCode, GetReasonPhrase(statusCode), Encoding.UTF8.GetBytes(json), "application/json; charset=utf-8", extraHeaders);
        }

        private static void WriteStatus(NetworkStream stream, int statusCode, string reasonPhrase, string message)
        {
            WriteBytes(stream, statusCode, reasonPhrase, Encoding.UTF8.GetBytes(message ?? string.Empty), "text/plain; charset=utf-8");
        }

        private static void TryWriteInternalServerError(NetworkStream stream, string message)
        {
            try
            {
                WriteStatus(stream, 500, "Internal Server Error", message);
            }
            catch
            {
            }
        }

        private static void WriteBytes(NetworkStream stream, int statusCode, string reasonPhrase, byte[] body, string contentType, IDictionary<string, string> extraHeaders = null)
        {
            StringBuilder headerBuilder = new StringBuilder();
            headerBuilder.Append("HTTP/1.1 ");
            headerBuilder.Append(statusCode.ToString(CultureInfo.InvariantCulture));
            headerBuilder.Append(' ');
            headerBuilder.Append(reasonPhrase);
            headerBuilder.Append("\r\nContent-Type: ");
            headerBuilder.Append(contentType);
            headerBuilder.Append("\r\nContent-Length: ");
            headerBuilder.Append((body?.Length ?? 0).ToString(CultureInfo.InvariantCulture));
            headerBuilder.Append("\r\nConnection: close\r\n");

            if (extraHeaders != null)
            {
                foreach (KeyValuePair<string, string> header in extraHeaders)
                {
                    headerBuilder.Append(header.Key);
                    headerBuilder.Append(": ");
                    headerBuilder.Append(header.Value);
                    headerBuilder.Append("\r\n");
                }
            }

            headerBuilder.Append("\r\n");
            WriteRaw(stream, headerBuilder.ToString());

            if (body != null && body.Length > 0)
            {
                stream.Write(body, 0, body.Length);
            }
        }

        private static void WriteRaw(NetworkStream stream, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static string GetReasonPhrase(int statusCode)
        {
            switch (statusCode)
            {
                case 200:
                    return "OK";
                case 202:
                    return "Accepted";
                case 401:
                    return "Unauthorized";
                case 403:
                    return "Forbidden";
                case 404:
                    return "Not Found";
                case 405:
                    return "Method Not Allowed";
                case 500:
                    return "Internal Server Error";
                case 503:
                    return "Service Unavailable";
                default:
                    return "OK";
            }
        }

        private static bool IsLoopbackClient(TcpClient client)
        {
            IPEndPoint remoteEndPoint = client?.Client?.RemoteEndPoint as IPEndPoint;
            return remoteEndPoint != null && IPAddress.IsLoopback(remoteEndPoint.Address);
        }

        private static IPAddress ParseLoopbackAddress(string bindAddress)
        {
            string candidate = string.IsNullOrWhiteSpace(bindAddress) ? "127.0.0.1" : bindAddress.Trim();
            if (string.Equals(candidate, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                return IPAddress.Loopback;
            }

            if (IPAddress.TryParse(candidate, out IPAddress address) && IPAddress.IsLoopback(address))
            {
                return address;
            }

            throw new InvalidOperationException("The integrated web panel only supports loopback bind addresses.");
        }

        private sealed class WebPanelTokenRequest
        {
            public string Token { get; set; } = string.Empty;
        }

        private sealed class HttpRequestData
        {
            public string Method { get; private set; } = string.Empty;
            public string Path { get; private set; } = string.Empty;
            public string Body { get; private set; } = string.Empty;
            public Dictionary<string, string> Headers { get; private set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> Cookies { get; private set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public static bool TryRead(Stream stream, out HttpRequestData request)
            {
                request = null;
                byte[] buffer = new byte[8192];
                MemoryStream memoryStream = new MemoryStream();
                int headerEnd = -1;

                while (headerEnd < 0 && memoryStream.Length < 65536)
                {
                    int read = stream.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                    {
                        return false;
                    }

                    memoryStream.Write(buffer, 0, read);
                    headerEnd = FindHeaderEnd(memoryStream.GetBuffer(), (int)memoryStream.Length);
                }

                if (headerEnd < 0)
                {
                    return false;
                }

                byte[] data = memoryStream.ToArray();
                string headerText = Encoding.UTF8.GetString(data, 0, headerEnd);
                string[] headerLines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
                if (headerLines.Length == 0)
                {
                    return false;
                }

                string[] requestLine = headerLines[0].Split(' ');
                if (requestLine.Length < 2)
                {
                    return false;
                }

                Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 1; i < headerLines.Length; i++)
                {
                    string line = headerLines[i];
                    int separatorIndex = line.IndexOf(':');
                    if (separatorIndex <= 0)
                    {
                        continue;
                    }

                    string key = line.Substring(0, separatorIndex).Trim();
                    string value = line.Substring(separatorIndex + 1).Trim();
                    headers[key] = value;
                }

                int contentLength = 0;
                if (headers.TryGetValue("Content-Length", out string contentLengthValue))
                {
                    int.TryParse(contentLengthValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out contentLength);
                }

                int bodyOffset = headerEnd + 4;
                int remainingBodyBytes = Math.Max(0, contentLength - Math.Max(0, data.Length - bodyOffset));
                while (remainingBodyBytes > 0)
                {
                    int read = stream.Read(buffer, 0, Math.Min(buffer.Length, remainingBodyBytes));
                    if (read <= 0)
                    {
                        break;
                    }

                    memoryStream.Write(buffer, 0, read);
                    remainingBodyBytes -= read;
                }

                data = memoryStream.ToArray();
                string body = contentLength > 0 && data.Length >= bodyOffset
                    ? Encoding.UTF8.GetString(data, bodyOffset, Math.Min(contentLength, data.Length - bodyOffset))
                    : string.Empty;

                Uri uri = new Uri("http://localhost" + requestLine[1], UriKind.Absolute);
                request = new HttpRequestData
                {
                    Method = (requestLine[0] ?? string.Empty).ToUpperInvariant(),
                    Path = uri.AbsolutePath,
                    Body = body,
                    Headers = headers,
                    Cookies = ParseCookies(headers)
                };
                return true;
            }

            private static int FindHeaderEnd(byte[] buffer, int length)
            {
                for (int i = 3; i < length; i++)
                {
                    if (buffer[i - 3] == '\r' && buffer[i - 2] == '\n' && buffer[i - 1] == '\r' && buffer[i] == '\n')
                    {
                        return i - 3;
                    }
                }

                return -1;
            }

            private static Dictionary<string, string> ParseCookies(Dictionary<string, string> headers)
            {
                Dictionary<string, string> cookies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (!headers.TryGetValue("Cookie", out string cookieHeader) || string.IsNullOrWhiteSpace(cookieHeader))
                {
                    return cookies;
                }

                string[] parts = cookieHeader.Split(';');
                for (int i = 0; i < parts.Length; i++)
                {
                    string part = parts[i];
                    int separatorIndex = part.IndexOf('=');
                    if (separatorIndex <= 0)
                    {
                        continue;
                    }

                    string name = part.Substring(0, separatorIndex).Trim();
                    string value = part.Substring(separatorIndex + 1).Trim();
                    cookies[name] = value;
                }

                return cookies;
            }
        }
    }
}
