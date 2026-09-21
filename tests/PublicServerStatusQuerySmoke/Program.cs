using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using DedicatedServerMod.Client.Managers;
using DedicatedServerMod.Shared;
using Newtonsoft.Json;

const string ListingId = "11111111-1111-4111-8111-111111111111";

await RunValidResponseTest();
await RunIdentityMismatchTest();
await RunOversizedResponseTest();
await RunStalledResponseTest();
Console.WriteLine("PASS|public-server-status-query-smoke");

static async Task RunValidResponseTest()
{
    string response = JsonConvert.SerializeObject(new ServerStatusSnapshot
    {
        ProtocolVersion = PublicServerListProtocol.Version,
        ListingId = ListingId,
        ServerName = "Verified fixture",
        CurrentPlayers = 1,
        MaxPlayers = 8
    });
    (int port, Task server) = StartServer(stream => WriteLineAsync(stream, response));
    var result = await new ServerStatusQueryService().QueryAsync("127.0.0.1", port, ListingId);
    await server;
    Assert(result.Snapshot.ServerName == "Verified fixture", "valid response metadata");
}

static async Task RunIdentityMismatchTest()
{
    string response = JsonConvert.SerializeObject(new ServerStatusSnapshot
    {
        ProtocolVersion = PublicServerListProtocol.Version,
        ListingId = "22222222-2222-4222-8222-222222222222"
    });
    (int port, Task server) = StartServer(stream => WriteLineAsync(stream, response));
    await AssertThrows<InvalidOperationException>(() => new ServerStatusQueryService().QueryAsync("127.0.0.1", port, ListingId));
    await server;
}

static async Task RunOversizedResponseTest()
{
    (int port, Task server) = StartServer(stream => WriteLineAsync(stream, new string('x', 17 * 1024)));
    await AssertThrows<InvalidDataException>(() => new ServerStatusQueryService().QueryAsync("127.0.0.1", port, ListingId));
    await server;
}

static async Task RunStalledResponseTest()
{
    (int port, _) = StartServer(async _ => await Task.Delay(TimeSpan.FromSeconds(5)));
    var stopwatch = Stopwatch.StartNew();
    await AssertThrows<TimeoutException>(() => new ServerStatusQueryService().QueryAsync("127.0.0.1", port, ListingId));
    Assert(stopwatch.Elapsed < TimeSpan.FromSeconds(4), "stalled response deadline");
}

static (int Port, Task Server) StartServer(Func<NetworkStream, Task> respond)
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    int port = ((IPEndPoint)listener.LocalEndpoint).Port;
    Task server = Task.Run(async () =>
    {
        try
        {
            using TcpClient client = await listener.AcceptTcpClientAsync();
            using NetworkStream stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true);
            string command = await reader.ReadLineAsync() ?? string.Empty;
            Assert(command == "DS_STATUS", "status request command");
            await respond(stream);
        }
        finally
        {
            listener.Stop();
        }
    });
    return (port, server);
}

static async Task WriteLineAsync(NetworkStream stream, string value)
{
    byte[] payload = Encoding.UTF8.GetBytes(value + "\n");
    await stream.WriteAsync(payload);
}

static async Task AssertThrows<TException>(Func<Task> action) where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException)
    {
        return;
    }
    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}

static void Assert(bool condition, string name)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Assertion failed: {name}.");
    }
}
