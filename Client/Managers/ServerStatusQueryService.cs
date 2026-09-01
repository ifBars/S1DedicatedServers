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
        private const int QueryTimeoutMilliseconds = 2500;

        internal async Task<ServerStatusQueryResult> QueryAsync(string host, int port)
        {
            using var client = new TcpClient();
            client.SendTimeout = QueryTimeoutMilliseconds;
            client.ReceiveTimeout = QueryTimeoutMilliseconds;

            var connectStopwatch = Stopwatch.StartNew();
            Task connectTask = client.ConnectAsync(host, port);
            Task completedTask = await Task.WhenAny(connectTask, Task.Delay(QueryTimeoutMilliseconds)).ConfigureAwait(false);
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
            using var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true);
            writer.WriteLine(StatusRequestCommand);
            string json = reader.ReadLine();
            ServerStatusSnapshot snapshot = JsonConvert.DeserializeObject<ServerStatusSnapshot>(json ?? string.Empty);
            if (snapshot == null)
            {
                throw new InvalidOperationException("Server returned an empty status response.");
            }

            return new ServerStatusQueryResult(snapshot, connectMilliseconds);
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
