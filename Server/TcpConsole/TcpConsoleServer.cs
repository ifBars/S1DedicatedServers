using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MelonLoader;

namespace DedicatedServerMod.Server.TcpConsole
{
	/// <summary>
	/// Lightweight TCP line-based console compatible with telnet. Each connection is handled on a background thread.
	/// </summary>
	public class TcpConsoleServer : IDisposable
	{
		private readonly string bindAddress;
		private readonly int port;
		private readonly string passwordOrNull;
		private readonly Func<string, string> handleLine;
		private readonly MelonLogger.Instance logger;
		private TcpListener listener;
		private Thread acceptThread;
		private volatile bool isRunning;

		public TcpConsoleServer(string bindAddress, int port, string passwordOrNull, Func<string, string> handleLine, MelonLogger.Instance logger)
		{
			this.bindAddress = string.IsNullOrWhiteSpace(bindAddress) ? "127.0.0.1" : bindAddress;
			this.port = port <= 0 ? 4050 : port;
			this.passwordOrNull = string.IsNullOrEmpty(passwordOrNull) ? null : passwordOrNull;
			this.handleLine = handleLine ?? throw new ArgumentNullException(nameof(handleLine));
			this.logger = logger ?? new MelonLogger.Instance("TcpConsole");
		}

		public void Start()
		{
			if (isRunning) return;
			listener = new TcpListener(IPAddress.Parse(bindAddress), port);
			listener.Start();
			isRunning = true;
			acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "TcpConsole-Accept" };
			acceptThread.Start();
		}

		private void AcceptLoop()
		{
			try
			{
				while (isRunning)
				{
					var client = listener.AcceptTcpClient();
					var thread = new Thread(() => HandleClient(client)) { IsBackground = true, Name = "TcpConsole-Client" };
					thread.Start();
				}
			}
			catch (SocketException)
			{
				// Listener stopped
			}
			catch (Exception ex)
			{
				logger.Warning($"TCP console accept loop error: {ex.Message}");
			}
		}

		private void HandleClient(TcpClient client)
		{
			using (client)
			{
				var stream = client.GetStream();
				var encoding = Encoding.UTF8;
				void Write(string s)
				{
					var data = encoding.GetBytes(s);
					stream.Write(data, 0, data.Length);
				}

				Write("ScheduleOne Dedicated Server Console\r\n");
				Write($"Type 'help' for commands. Connected at {DateTime.Now}.\r\n");
				if (passwordOrNull != null)
				{
					Write("Password: ");
					var pw = ReadLine(stream, encoding);
					if (!string.Equals(pw ?? string.Empty, passwordOrNull))
					{
						Write("Authentication failed.\r\n");
						return;
					}
					Write("Authenticated.\r\n");
				}

				while (isRunning && client.Connected)
				{
					Write("> ");
					var line = ReadLine(stream, encoding);
					if (line == null) break;
					line = line.Trim();
					if (line.Length == 0) { Write("\r\n"); continue; }
					if (line.Equals("exit", StringComparison.OrdinalIgnoreCase) || line.Equals("quit", StringComparison.OrdinalIgnoreCase))
					{
						Write("Goodbye.\r\n");
						break;
					}
					try
					{
						var response = handleLine(line) ?? string.Empty;
						if (!response.EndsWith("\r\n")) response += "\r\n";
						Write(response);
					}
					catch (Exception ex)
					{
						Write($"Error: {ex.Message}\r\n");
					}
				}
			}
		}

		private static string ReadLine(NetworkStream stream, Encoding encoding)
		{
			var sb = new StringBuilder();
			var buffer = new byte[1];
			while (true)
			{
				int read;
				try { read = stream.Read(buffer, 0, 1); }
				catch { return null; }
				if (read <= 0) return null;
				char c = (char)buffer[0];
				if (c == '\n')
				{
					break;
				}
				if (c == '\r')
				{
					continue;
				}
				sb.Append(c);
			}
			return sb.ToString();
		}

		public void Dispose()
		{
			try
			{
				isRunning = false;
				listener?.Stop();
			}
			catch { }
		}
	}
}
