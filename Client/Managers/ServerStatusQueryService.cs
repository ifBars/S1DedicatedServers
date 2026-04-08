using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using DedicatedServerMod.Shared;
using DedicatedServerMod.Shared.CustomClothing;
using Newtonsoft.Json;

namespace DedicatedServerMod.Client.Managers
{
    /// <summary>
    /// Queries dedicated servers for browser metadata without joining them.
    /// </summary>
    internal sealed class ServerStatusQueryService
    {
        private const string StatusRequestCommand = "DS_STATUS";
        private const string ClothingManifestCommand = "DS_CLOTHING_MANIFEST";
        private const string ClothingAssetCommand = "DS_CLOTHING_ASSET";

        internal Task<ServerStatusQueryResult> QueryAsync(string host, int port)
        {
            return Task.Run(() => Query(host, port));
        }

        internal Task<CustomClothingManifest> FetchCustomClothingManifestAsync(string host, int port)
        {
            return Task.Run(() =>
            {
                string json = SendCommand(host, port, ClothingManifestCommand, receiveTimeoutMs: 10000);
                CustomClothingManifestResponse response = JsonConvert.DeserializeObject<CustomClothingManifestResponse>(json ?? string.Empty);
                if (response == null)
                {
                    throw new InvalidOperationException("Server returned an empty custom clothing manifest response.");
                }

                if (!response.Success)
                {
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(response.Error)
                        ? "Server rejected the custom clothing manifest request."
                        : response.Error);
                }

                return response.Manifest ?? new CustomClothingManifest();
            });
        }

        internal Task<CustomClothingAssetPayload> FetchCustomClothingAssetAsync(string host, int port, string itemId)
        {
            return Task.Run(() =>
            {
                string json = SendCommand(host, port, $"{ClothingAssetCommand} {itemId}", receiveTimeoutMs: 15000);
                CustomClothingAssetResponse response = JsonConvert.DeserializeObject<CustomClothingAssetResponse>(json ?? string.Empty);
                if (response == null)
                {
                    throw new InvalidOperationException($"Server returned an empty custom clothing asset response for '{itemId}'.");
                }

                if (!response.Success)
                {
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(response.Error)
                        ? $"Server rejected the custom clothing asset request for '{itemId}'."
                        : response.Error);
                }

                return response.Asset;
            });
        }

        private static ServerStatusQueryResult Query(string host, int port)
        {
            using var client = new TcpClient();
            client.SendTimeout = 2500;
            client.ReceiveTimeout = 2500;
            var connectStopwatch = Stopwatch.StartNew();
            client.Connect(host, port);
            connectStopwatch.Stop();

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

            return new ServerStatusQueryResult(snapshot, (int)Math.Max(0, connectStopwatch.ElapsedMilliseconds));
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
