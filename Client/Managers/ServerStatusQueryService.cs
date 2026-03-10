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

        public Task<ServerStatusQueryResult> QueryAsync(string host, int port)
        {
            return Task.Run(() => Query(host, port));
        }

        private static ServerStatusQueryResult Query(string host, int port)
        {
            var stopwatch = Stopwatch.StartNew();

            using (var client = new TcpClient())
            {
                client.SendTimeout = 2500;
                client.ReceiveTimeout = 2500;
                client.Connect(host, port);

                using (NetworkStream stream = client.GetStream())
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true) { AutoFlush = true })
                using (var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true))
                {
                    writer.WriteLine(StatusRequestCommand);
                    string json = reader.ReadLine();
                    ServerStatusSnapshot snapshot = JsonConvert.DeserializeObject<ServerStatusSnapshot>(json ?? string.Empty);
                    if (snapshot == null)
                    {
                        throw new InvalidOperationException("Server returned an empty status response.");
                    }

                    stopwatch.Stop();
                    return new ServerStatusQueryResult(snapshot, (int)Math.Max(0, stopwatch.ElapsedMilliseconds));
                }
            }
        }
    }

    internal sealed class ServerStatusQueryResult
    {
        public ServerStatusQueryResult(ServerStatusSnapshot snapshot, int pingMilliseconds)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            PingMilliseconds = pingMilliseconds;
        }

        public ServerStatusSnapshot Snapshot { get; }

        public int PingMilliseconds { get; }
    }
}
