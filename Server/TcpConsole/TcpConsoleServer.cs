using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using DedicatedServerMod.Server.Commands;
using DedicatedServerMod.Shared.Configuration;
using DedicatedServerMod.Utils;
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
		private readonly int maxConnections;
		private readonly string passwordOrNull;
		private readonly Func<string, string> handleLine;
		private readonly MelonLogger.Instance logger;
		private TcpListener listener;
		private Thread acceptThread;
		private int activeClientCount;
		private volatile bool isRunning;

		/// <summary>
		/// Initializes a new TCP console server instance.
		/// </summary>
		/// <param name="bindAddress">IP address to bind to. Falls back to loopback when blank.</param>
		/// <param name="port">TCP port to listen on. Falls back to the default TCP console port when invalid.</param>
		/// <param name="maxConnections">Maximum number of concurrent console clients. Falls back to the default limit when invalid.</param>
		/// <param name="passwordOrNull">Optional password required for authentication.</param>
		/// <param name="handleLine">Delegate used to process a single console command line.</param>
		/// <param name="logger">Logger used for runtime diagnostics.</param>
		public TcpConsoleServer(string bindAddress, int port, int maxConnections, string passwordOrNull, Func<string, string> handleLine, MelonLogger.Instance logger)
		{
			this.bindAddress = string.IsNullOrWhiteSpace(bindAddress) ? Constants.DefaultTcpConsoleBindAddress : bindAddress;
			this.port = port <= 0 ? Constants.DefaultTcpConsolePort : port;
			this.maxConnections = maxConnections > 0 ? maxConnections : Constants.DefaultTcpConsoleMaxConnections;
			this.passwordOrNull = string.IsNullOrEmpty(passwordOrNull) ? null : passwordOrNull;
			this.handleLine = handleLine ?? throw new ArgumentNullException(nameof(handleLine));
			this.logger = logger ?? new MelonLogger.Instance("TcpConsole");
		}

		/// <summary>
		/// Creates and starts the TCP console server from the active server configuration.
		/// </summary>
		/// <param name="commandManager">Command manager used to execute console commands.</param>
		/// <param name="logger">Logger instance used for startup and runtime diagnostics.</param>
		/// <returns>The started TCP console server, or <see langword="null"/> when disabled or startup fails.</returns>
		public static TcpConsoleServer TryStart(CommandManager commandManager, MelonLogger.Instance logger)
		{
			if (commandManager == null)
			{
				throw new ArgumentNullException(nameof(commandManager));
			}

			if (logger == null)
			{
				throw new ArgumentNullException(nameof(logger));
			}

			try
			{
				ServerConfig cfg = ServerConfig.Instance;
				if (!cfg.TcpConsoleEnabled)
				{
					return null;
				}

				
				bool hasPassword = cfg.TcpConsoleRequirePassword && !string.IsNullOrWhiteSpace(cfg.TcpConsolePassword);

				if (!IsLoopbackBindAddress(cfg.TcpConsoleBindAddress))
				{
					logger.Warning("TCP console is bound to a non-loopback address. Keep this interface LAN-only and prefer loopback unless you are deliberately securing access another way.");
					if (!hasPassword)
					{
						logger.Warning("TCP console is exposed beyond loopback without a password requirement. This is not recommended.");
					}
				}

				TcpConsoleServer tcpConsole = new(
					cfg.TcpConsoleBindAddress,
					cfg.TcpConsolePort,
					cfg.TcpConsoleMaxConnections,
					hasPassword ? cfg.TcpConsolePassword : null,
					line => ExecuteConsoleCommand(commandManager, logger, line),
					logger);

				tcpConsole.Start();
				logger.Msg($"✓ TCP console listening on {tcpConsole.bindAddress}:{tcpConsole.port} (max {tcpConsole.maxConnections} clients, {Constants.TcpConsoleSocketTimeoutMs / 1000}s timeout)");
				return tcpConsole;
			}
			catch (Exception ex)
			{
				logger.Warning($"TCP console failed to start: {ex.Message}");
				return null;
			}
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
					TcpClient client = listener.AcceptTcpClient();
					ConfigureClient(client);

					int currentClientCount = Interlocked.Increment(ref activeClientCount);
					if (currentClientCount > maxConnections)
					{
						Interlocked.Decrement(ref activeClientCount);
						logger.Warning($"TCP console rejected a connection because the max client limit of {maxConnections} was reached.");
						RejectClient(client, $"TCP console connection limit reached ({maxConnections}).\r\n");
						continue;
					}

					Thread clientThread = new Thread(() => HandleClient(client))
					{
						IsBackground = true,
						Name = "TcpConsole-Client"
					};
					clientThread.Start();
				}
			}
			catch (SocketException)
			{
				// Listener stopped during shutdown.
			}
			catch (ObjectDisposedException)
			{
				// Listener disposed during shutdown.
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
				try
				{
					NetworkStream stream = client.GetStream();
					Encoding encoding = Encoding.UTF8;

					void Write(string s)
					{
						byte[] data = encoding.GetBytes(s);
						stream.Write(data, 0, data.Length);
					}

					Write("ScheduleOne Dedicated Server Console\r\n");
					Write($"Type 'help' for commands. Connected at {DateTime.Now}.\r\n");
					if (passwordOrNull != null)
					{
						Write("Password: ");
						string password = ReadLine(stream, encoding);
						if (!string.Equals(password ?? string.Empty, passwordOrNull, StringComparison.Ordinal))
						{
							Write("Authentication failed.\r\n");
							return;
						}

						Write("Authenticated.\r\n");
					}

					while (isRunning && client.Connected)
					{
						Write("> ");
						string line = ReadLine(stream, encoding);
						if (line == null)
						{
							break;
						}

						line = line.Trim();
						if (line.Length == 0)
						{
							Write("\r\n");
							continue;
						}

						if (line.Equals("exit", StringComparison.OrdinalIgnoreCase) || line.Equals("quit", StringComparison.OrdinalIgnoreCase))
						{
							Write("Goodbye.\r\n");
							break;
						}

						try
						{
							string response = handleLine(line) ?? string.Empty;
							if (!response.EndsWith("\r\n", StringComparison.Ordinal))
							{
								response += "\r\n";
							}

							Write(response);
						}
						catch (Exception ex)
						{
							Write($"Error: {ex.Message}\r\n");
						}
					}
				}
				catch (IOException)
				{
					// Client timed out or disconnected.
				}
				finally
				{
					Interlocked.Decrement(ref activeClientCount);
				}
			}
		}

		private static bool IsLoopbackBindAddress(string candidate)
		{
			string normalizedAddress = string.IsNullOrWhiteSpace(candidate) ? Constants.DefaultTcpConsoleBindAddress : candidate.Trim();
			if (string.Equals(normalizedAddress, "localhost", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			if (!IPAddress.TryParse(normalizedAddress, out IPAddress address))
			{
				return false;
			}

			return IPAddress.IsLoopback(address);
		}

		private static void ConfigureClient(TcpClient client)
		{
			client.ReceiveTimeout = Constants.TcpConsoleSocketTimeoutMs;
			client.SendTimeout = Constants.TcpConsoleSocketTimeoutMs;
			client.NoDelay = true;

			NetworkStream stream = client.GetStream();
			stream.ReadTimeout = Constants.TcpConsoleSocketTimeoutMs;
			stream.WriteTimeout = Constants.TcpConsoleSocketTimeoutMs;
		}

		private static void RejectClient(TcpClient client, string message)
		{
			using (client)
			{
				try
				{
					NetworkStream stream = client.GetStream();
					byte[] data = Encoding.UTF8.GetBytes(message);
					stream.Write(data, 0, data.Length);
				}
				catch
				{
				}
			}
		}

		private static string ReadLine(NetworkStream stream, Encoding encoding)
		{
			StringBuilder sb = new StringBuilder();
			byte[] buffer = new byte[1];
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

		private static string ExecuteConsoleCommand(CommandManager commandManager, MelonLogger.Instance logger, string line)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(line))
				{
					return string.Empty;
				}

				List<string> parts = new List<string>(line.Trim().Split(' '));
				string cmd = parts[0];
				parts.RemoveAt(0);

				StringBuilder output = new StringBuilder();
				bool ok = commandManager.ExecuteCommand(
					cmd,
					parts,
					s => output.AppendLine(s),
					s => output.AppendLine("[WARN] " + s),
					s => output.AppendLine("[ERR] " + s));

				if (!ok)
				{
					return $"Unknown or unauthorized command: {cmd}\r\n";
				}

				return output.ToString();
			}
			catch (Exception ex)
			{
				logger.Error($"TCP console command error: {ex}");
				return $"Error: {ex.Message}\r\n";
			}
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
