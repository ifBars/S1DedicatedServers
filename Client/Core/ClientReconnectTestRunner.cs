#if IL2CPP
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Persistence;
#else
using ScheduleOne.DevUtilities;
using ScheduleOne.Persistence;
#endif
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using DedicatedServerMod.Client.Managers;
using DedicatedServerMod.Utils;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DedicatedServerMod.Client.Core
{
    /// <summary>
    /// Opt-in real-game reconnect diagnostic runner for IL2CPP loading regressions.
    /// </summary>
    internal sealed class ClientReconnectTestRunner
    {
        private const string TestFlag = "--s1ds-reconnect-test";
        private const string HostFlag = "--s1ds-reconnect-test-host";
        private const string PortFlag = "--s1ds-reconnect-test-port";
        private const string CyclesFlag = "--s1ds-reconnect-test-cycles";
        private const string DwellFlag = "--s1ds-reconnect-test-dwell-seconds";
        private const string StartDelayFlag = "--s1ds-reconnect-test-start-delay-seconds";
        private const string StartMarkerFlag = "--s1ds-reconnect-test-start-marker";
        private const string JoinTimeoutFlag = "--s1ds-reconnect-test-join-timeout-seconds";
        private const string MenuTimeoutFlag = "--s1ds-reconnect-test-menu-timeout-seconds";
        private const string QuitFlag = "--s1ds-reconnect-test-quit";

        private readonly ClientConnectionManager _connectionManager;
        private ReconnectTestOptions _options;
        private bool _enabled;
        private bool _started;
        private bool _failed;
        private bool _connectionEventReceived;
        private int _completedCycles;

        internal ClientReconnectTestRunner(ClientConnectionManager connectionManager)
        {
            _connectionManager = connectionManager;
        }

        internal void Initialize()
        {
            _options = ReconnectTestOptions.Parse(Environment.GetCommandLineArgs());
            _enabled = _options.Enabled;
            if (!_enabled)
            {
                return;
            }

            _connectionManager.DedicatedServerConnected += OnDedicatedServerConnected;
            MelonCoroutines.Start(RunTest());
        }

        internal void Shutdown()
        {
            if (_enabled)
            {
                _connectionManager.DedicatedServerConnected -= OnDedicatedServerConnected;
            }
        }

        private IEnumerator RunTest()
        {
            if (_started)
            {
                yield break;
            }

            _started = true;
            Log($"START cycles={_options.Cycles} dwellSeconds={_options.DwellSeconds:F1} startDelaySeconds={_options.StartDelaySeconds:F1} host={_options.Host} port={_options.Port}");

            if (!string.IsNullOrWhiteSpace(_options.StartMarkerPath))
            {
                yield return WaitForStartMarker();
                if (_failed)
                {
                    yield break;
                }
            }
            else if (_options.StartDelaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(_options.StartDelaySeconds);
            }

            yield return WaitForMenu(_options.MenuTimeoutSeconds, "initial-menu");
            if (_failed)
            {
                yield break;
            }

            for (int cycle = 1; cycle <= _options.Cycles; cycle++)
            {
                _connectionEventReceived = false;
                _connectionManager.SetTargetServer(_options.Host, _options.Port);

                Log($"CYCLE {cycle}/{_options.Cycles} JOIN_START");
                _connectionManager.StartDedicatedConnection();

                float joinStart = Time.realtimeSinceStartup;
                yield return new WaitUntil((Func<bool>)(() =>
                    _connectionEventReceived ||
                    _connectionManager.IsConnectedToDedicatedServer ||
                    !string.IsNullOrWhiteSpace(_connectionManager.LastConnectionError) ||
                    Time.realtimeSinceStartup - joinStart > _options.JoinTimeoutSeconds));

                if (!_connectionManager.IsConnectedToDedicatedServer)
                {
                    Fail($"cycle {cycle} failed to connect within {_options.JoinTimeoutSeconds:F1}s. LastError='{_connectionManager.LastConnectionError ?? string.Empty}'");
                    yield break;
                }

                Log($"CYCLE {cycle}/{_options.Cycles} JOINED");
                yield return new WaitForSecondsRealtime(_options.DwellSeconds);

                if (!_connectionManager.IsConnectedToDedicatedServer)
                {
                    Fail($"cycle {cycle} disconnected before dwell completed. LastError='{_connectionManager.LastConnectionError ?? string.Empty}'");
                    yield break;
                }

                Log($"CYCLE {cycle}/{_options.Cycles} DWELL_OK");
                _completedCycles = cycle;

                if (cycle == _options.Cycles)
                {
                    break;
                }

                Log($"CYCLE {cycle}/{_options.Cycles} DISCONNECT_START");
                _connectionManager.DisconnectFromDedicatedServer();
                yield return WaitForMenu(_options.MenuTimeoutSeconds, $"cycle-{cycle}-menu");
                if (_failed)
                {
                    yield break;
                }

                Log($"CYCLE {cycle}/{_options.Cycles} DISCONNECTED");
            }

            Log($"PASS completedCycles={_completedCycles}");
            QuitIfRequested(exitCode: 0);
        }

        private IEnumerator WaitForStartMarker()
        {
            Log($"WAIT_START_MARKER path={_options.StartMarkerPath} timeoutSeconds={_options.StartDelaySeconds:F1}");
            float waitStart = Time.realtimeSinceStartup;
            yield return new WaitUntil((Func<bool>)(() =>
                File.Exists(_options.StartMarkerPath) ||
                Time.realtimeSinceStartup - waitStart > _options.StartDelaySeconds));

            if (!File.Exists(_options.StartMarkerPath))
            {
                Fail($"timed out waiting for start marker '{_options.StartMarkerPath}'");
            }
            else
            {
                Log("START_MARKER_FOUND");
            }
        }

        private IEnumerator WaitForMenu(float timeoutSeconds, string phase)
        {
            float waitStart = Time.realtimeSinceStartup;
            yield return new WaitUntil((Func<bool>)(() =>
                (SceneManager.GetActiveScene().name == "Menu" &&
                 !_connectionManager.IsConnecting &&
                 !_connectionManager.IsConnectedToDedicatedServer &&
                 !_connectionManager.IsReturningToMenu) ||
                Time.realtimeSinceStartup - waitStart > timeoutSeconds));

            if (SceneManager.GetActiveScene().name != "Menu" ||
                _connectionManager.IsConnecting ||
                _connectionManager.IsConnectedToDedicatedServer ||
                _connectionManager.IsReturningToMenu)
            {
                Fail($"timed out waiting for menu during {phase}");
            }
        }

        private void OnDedicatedServerConnected(string host, int port)
        {
            _connectionEventReceived = true;
        }

        private static void Log(string message)
        {
            DebugLog.Info($"[RECONNECT_TEST] {message}");
        }

        private void Fail(string message)
        {
            _failed = true;
            DebugLog.Error($"[RECONNECT_TEST] FAIL {message}");
            QuitIfRequested(exitCode: 2);
        }

        private void QuitIfRequested(int exitCode)
        {
            if (!_options.QuitOnComplete)
            {
                return;
            }

            try
            {
                Application.Quit(exitCode);
            }
            catch
            {
                Application.Quit();
            }
        }

        private sealed class ReconnectTestOptions
        {
            public bool Enabled { get; private set; }
            public string Host { get; private set; } = "127.0.0.1";
            public int Port { get; private set; } = 38465;
            public int Cycles { get; private set; } = 4;
            public float DwellSeconds { get; private set; } = 30f;
            public float StartDelaySeconds { get; private set; } = 30f;
            public string StartMarkerPath { get; private set; }
            public float JoinTimeoutSeconds { get; private set; } = 120f;
            public float MenuTimeoutSeconds { get; private set; } = 45f;
            public bool QuitOnComplete { get; private set; } = true;

            public static ReconnectTestOptions Parse(string[] args)
            {
                var options = new ReconnectTestOptions();
                if (args == null)
                {
                    return options;
                }

                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i];
                    if (string.Equals(arg, TestFlag, StringComparison.Ordinal))
                    {
                        options.Enabled = true;
                    }
                    else if (string.Equals(arg, HostFlag, StringComparison.Ordinal) && TryReadNext(args, i, out string host))
                    {
                        options.Host = host;
                    }
                    else if (string.Equals(arg, PortFlag, StringComparison.Ordinal) && TryReadNext(args, i, out string portText) && int.TryParse(portText, out int port))
                    {
                        options.Port = port;
                    }
                    else if (string.Equals(arg, CyclesFlag, StringComparison.Ordinal) && TryReadNext(args, i, out string cyclesText) && int.TryParse(cyclesText, out int cycles))
                    {
                        options.Cycles = Mathf.Clamp(cycles, 1, 10);
                    }
                    else if (string.Equals(arg, DwellFlag, StringComparison.Ordinal) && TryReadNext(args, i, out string dwellText) && TryParseFloat(dwellText, out float dwellSeconds))
                    {
                        options.DwellSeconds = Mathf.Clamp(dwellSeconds, 1f, 300f);
                    }
                    else if (string.Equals(arg, StartDelayFlag, StringComparison.Ordinal) && TryReadNext(args, i, out string startDelayText) && TryParseFloat(startDelayText, out float startDelaySeconds))
                    {
                        options.StartDelaySeconds = Mathf.Clamp(startDelaySeconds, 0f, 300f);
                    }
                    else if (string.Equals(arg, StartMarkerFlag, StringComparison.Ordinal) && TryReadNext(args, i, out string startMarkerPath))
                    {
                        options.StartMarkerPath = startMarkerPath;
                    }
                    else if (string.Equals(arg, JoinTimeoutFlag, StringComparison.Ordinal) && TryReadNext(args, i, out string joinTimeoutText) && TryParseFloat(joinTimeoutText, out float joinTimeoutSeconds))
                    {
                        options.JoinTimeoutSeconds = Mathf.Clamp(joinTimeoutSeconds, 10f, 600f);
                    }
                    else if (string.Equals(arg, MenuTimeoutFlag, StringComparison.Ordinal) && TryReadNext(args, i, out string menuTimeoutText) && TryParseFloat(menuTimeoutText, out float menuTimeoutSeconds))
                    {
                        options.MenuTimeoutSeconds = Mathf.Clamp(menuTimeoutSeconds, 10f, 300f);
                    }
                    else if (string.Equals(arg, QuitFlag, StringComparison.Ordinal))
                    {
                        options.QuitOnComplete = true;
                    }
                }

                return options;
            }

            private static bool TryParseFloat(string value, out float result)
            {
                return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
            }

            private static bool TryReadNext(string[] args, int index, out string value)
            {
                value = null;
                if (index + 1 >= args.Length)
                {
                    return false;
                }

                value = args[index + 1];
                return !string.IsNullOrWhiteSpace(value);
            }
        }
    }
}
