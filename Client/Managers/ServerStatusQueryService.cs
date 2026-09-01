using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using DedicatedServerMod.Shared;
using Newtonsoft.Json;

namespace DedicatedServerMod.Client.Managers
{
    /// <summary>
    /// Queries dedicated servers for browser metadata without joining them.
    /// </summary>
    internal sealed class ServerStatusQueryService
    {
        private const string StatusRequestCommand = "DS_STATUS";
        private const int QUERY_TIMEOUT_MILLISECONDS = 2500;
        private const int MAX_RESPONSE_BYTES = 16 * 1024;

        internal async Task<ServerStatusQueryResult> QueryAsync(string host, int port, string expectedListingId = null)
        {
            using var client = new TcpClient();
            client.SendTimeout = QUERY_TIMEOUT_MILLISECONDS;
            client.ReceiveTimeout = QUERY_TIMEOUT_MILLISECONDS;

            var connectStopwatch = Stopwatch.StartNew();
            Task connectTask = client.ConnectAsync(host, port);
            Task completedTask = await Task.WhenAny(connectTask, Task.Delay(QUERY_TIMEOUT_MILLISECONDS)).ConfigureAwait(false);
            if (completedTask != connectTask)
            {
                _ = connectTask.ContinueWith(
                    task =>
                    {
                        _ = task.Exception;
                    },
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
                throw new TimeoutException($"Status query connection to {host}:{port} timed out.");
            }

            await connectTask.ConfigureAwait(false);
            connectStopwatch.Stop();
            int connectMilliseconds = (int)Math.Max(0, connectStopwatch.ElapsedMilliseconds);

            using NetworkStream stream = client.GetStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
            writer.WriteLine(StatusRequestCommand);
            string json = await ReadBoundedLineAsync(stream, QUERY_TIMEOUT_MILLISECONDS - (int)connectStopwatch.ElapsedMilliseconds).ConfigureAwait(false);
            ServerStatusSnapshot snapshot = JsonConvert.DeserializeObject<ServerStatusSnapshot>(json ?? string.Empty);
            if (snapshot == null)
            {
                throw new InvalidOperationException("Server returned an empty status response.");
            }

            if (!string.IsNullOrWhiteSpace(expectedListingId) &&
                (snapshot.ProtocolVersion != PublicServerListProtocol.Version ||
                 !string.Equals(snapshot.ListingId, expectedListingId, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Server status identity does not match the public directory listing.");
            }

            return new ServerStatusQueryResult(snapshot, connectMilliseconds);
        }

        private static async Task<string> ReadBoundedLineAsync(NetworkStream stream, int remainingMilliseconds)
        {
            if (remainingMilliseconds <= 0)
            {
                throw new TimeoutException("Status query exceeded its overall deadline.");
            }

            var deadline = Stopwatch.StartNew();
            using var buffer = new MemoryStream();
            var chunk = new byte[1024];
            while (buffer.Length <= MAX_RESPONSE_BYTES)
            {
                int waitMilliseconds = remainingMilliseconds - (int)deadline.ElapsedMilliseconds;
                if (waitMilliseconds <= 0)
                {
                    throw new TimeoutException("Status query response timed out.");
                }

                Task<int> readTask = stream.ReadAsync(chunk, 0, chunk.Length);
                Task completed = await Task.WhenAny(readTask, Task.Delay(waitMilliseconds)).ConfigureAwait(false);
                if (completed != readTask)
                {
                    _ = readTask.ContinueWith(
                        task => { _ = task.Exception; },
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
                    throw new TimeoutException("Status query response timed out.");
                }

                int count = await readTask.ConfigureAwait(false);
                if (count == 0)
                {
                    throw new IOException("Server closed the connection before completing its status response.");
                }

                int newline = Array.IndexOf(chunk, (byte)'\n', 0, count);
                int bytesToWrite = newline >= 0 ? newline : count;
                if (buffer.Length + bytesToWrite > MAX_RESPONSE_BYTES)
                {
                    throw new InvalidDataException($"Status query response exceeded {MAX_RESPONSE_BYTES} bytes.");
                }
                buffer.Write(chunk, 0, bytesToWrite);
                if (newline >= 0)
                {
                    byte[] payload = buffer.ToArray();
                    int length = payload.Length > 0 && payload[payload.Length - 1] == '\r' ? payload.Length - 1 : payload.Length;
                    return new UTF8Encoding(false, true).GetString(payload, 0, length);
                }
            }

            throw new InvalidDataException($"Status query response exceeded {MAX_RESPONSE_BYTES} bytes.");
        }

        private static string SendCommand(string host, int port, string command, int receiveTimeoutMs)
        {
            using var client = new TcpClient();
            client.SendTimeout = receiveTimeoutMs;
            client.ReceiveTimeout = receiveTimeoutMs;
            client.Connect(host, port);

            using NetworkStream stream = client.GetStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
            using var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true);
            writer.WriteLine(command);
            string response = reader.ReadLine();
            if (response == null)
            {
                throw new IOException($"Server closed the connection while handling '{command}'.");
            }

            return response;
        }
    }

    internal sealed class ServerStatusQueryResult
    {
        internal ServerStatusQueryResult(ServerStatusSnapshot snapshot, int statusQueryMilliseconds)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            StatusQueryMilliseconds = statusQueryMilliseconds;
        }

        internal ServerStatusSnapshot Snapshot { get; }

        internal int StatusQueryMilliseconds { get; }
    }
}
