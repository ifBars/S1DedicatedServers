using System.Collections;
using System.Globalization;
using DedicatedServerMod.Client.Managers;
using DedicatedServerMod.Utils;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DedicatedServerMod.Client.Core
{
    internal sealed class ClientPublicServerListTestRunner
    {
        private const string TEST_FLAG = "--s1ds-public-list-test";
        private const string EXPECTED_SERVER_FLAG = "--s1ds-public-list-test-expected-server";
        private const string RESULT_FLAG = "--s1ds-public-list-test-result";
        private const string SCREENSHOT_FLAG = "--s1ds-public-list-test-screenshot";
        private const string TIMEOUT_FLAG = "--s1ds-public-list-test-timeout-seconds";
        private const string QUIT_FLAG = "--s1ds-public-list-test-quit";

        private readonly ClientUIManager _uiManager;
        private TestOptions _options;
        private bool _enabled;
        private bool _completed;

        internal ClientPublicServerListTestRunner(ClientUIManager uiManager)
        {
            _uiManager = uiManager ?? throw new ArgumentNullException(nameof(uiManager));
        }

        internal void Initialize()
        {
            _options = TestOptions.Parse(Environment.GetCommandLineArgs());
            _enabled = _options.Enabled;
            if (!_enabled)
            {
                return;
            }

            MelonCoroutines.Start(RunTest());
        }

        internal void Shutdown()
        {
            _enabled = false;
        }

        private IEnumerator RunTest()
        {
            Log($"START expectedServer='{_options.ExpectedServerName}' timeoutSeconds={_options.TimeoutSeconds:F1}");
            if (!string.IsNullOrWhiteSpace(_options.ParseError))
            {
                Complete($"FAIL|reason={_options.ParseError}", 2);
                yield break;
            }

            float startedAt = Time.realtimeSinceStartup;
            bool publicTabVisible = false;
            bool publicTabLogged = false;
            while (_enabled && Time.realtimeSinceStartup - startedAt < _options.TimeoutSeconds)
            {
                if (!publicTabVisible)
                {
                    publicTabVisible = _uiManager.TryShowPublicServerListForTest();
                    if (publicTabVisible && !publicTabLogged)
                    {
                        publicTabLogged = true;
                        Log("PHASE public-tab-visible");
                    }
                }

                if (publicTabVisible && !_uiManager.IsPublicDirectoryRefreshInFlightForTest)
                {
                    if (!string.IsNullOrWhiteSpace(_uiManager.PublicDirectoryLastErrorForTest))
                    {
                        Complete($"FAIL|reason=directory-refresh-error|error={_uiManager.PublicDirectoryLastErrorForTest}", 2);
                        yield break;
                    }

                    if (!_uiManager.HasPublicServerForTest(_options.ExpectedServerName))
                    {
                        Complete($"FAIL|reason=expected-server-not-listed|serverCount={_uiManager.PublicServerCountForTest}|expectedServer={_options.ExpectedServerName}", 2);
                        yield break;
                    }

                    Log($"PHASE expected-server-visible serverCount={_uiManager.PublicServerCountForTest}");
                    break;
                }

                yield return null;
            }

            if (!_enabled)
            {
                yield break;
            }

            if (!publicTabVisible)
            {
                Complete($"FAIL|reason=public-tab-unavailable|scene={SceneManager.GetActiveScene().name}", 2);
                yield break;
            }

            if (_uiManager.IsStartupNoticeVisibleForTest())
            {
                Log("PHASE waiting-for-startup-notice-to-clear");
                float noticeStartedAt = Time.realtimeSinceStartup;
                while (_uiManager.IsStartupNoticeVisibleForTest() && Time.realtimeSinceStartup - noticeStartedAt < 20f)
                {
                    yield return null;
                }

                if (_uiManager.IsStartupNoticeVisibleForTest())
                {
                    Complete("FAIL|reason=startup-notice-did-not-clear", 2);
                    yield break;
                }

                Log("PHASE startup-notice-cleared");
            }

            yield return new WaitForSecondsRealtime(1f);

            Directory.CreateDirectory(Path.GetDirectoryName(_options.ScreenshotPath) ?? ".");
#if IL2CPP
            ScreenCapture.CaptureScreenshot(_options.ScreenshotPath);
#else
            if (!TryCaptureScreenshot(_options.ScreenshotPath, out string captureError))
            {
                Complete($"FAIL|reason=screenshot-capture-unavailable|error={captureError}", 2);
                yield break;
            }
#endif
            float screenshotStartedAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - screenshotStartedAt < 10f)
            {
                if (File.Exists(_options.ScreenshotPath) && new FileInfo(_options.ScreenshotPath).Length > 0)
                {
                    string runtime =
#if IL2CPP
                        "Il2Cpp";
#else
                        "Mono";
#endif
                    Complete(
                        $"PASS|runtime={runtime}|scene={SceneManager.GetActiveScene().name}|serverCount={_uiManager.PublicServerCountForTest}|expectedServer={_options.ExpectedServerName}|screenshot={_options.ScreenshotPath}",
                        0);
                    yield break;
                }

                yield return null;
            }

            Complete("FAIL|reason=screenshot-timeout", 2);
        }

#if !IL2CPP
        private static bool TryCaptureScreenshot(string path, out string error)
        {
            const string screenCaptureTypeName = "UnityEngine.ScreenCapture";
            Type screenCaptureType = Type.GetType($"{screenCaptureTypeName}, UnityEngine.ScreenCaptureModule");
            if (screenCaptureType == null)
            {
                foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    screenCaptureType = assembly.GetType(screenCaptureTypeName, false);
                    if (screenCaptureType != null)
                    {
                        break;
                    }
                }
            }

            System.Reflection.MethodInfo captureMethod = screenCaptureType?.GetMethod(
                "CaptureScreenshot",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);
            if (captureMethod == null)
            {
                error = "UnityEngine.ScreenCapture.CaptureScreenshot(string)-not-found";
                return false;
            }

            try
            {
                captureMethod.Invoke(null, new object[] { path });
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetBaseException().Message;
                return false;
            }
        }
#endif

        private void Complete(string result, int exitCode)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            Log(result);
            Directory.CreateDirectory(Path.GetDirectoryName(_options.ResultPath) ?? ".");
            File.WriteAllText(_options.ResultPath, result);

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

        private static void Log(string message)
        {
            DebugLog.Info($"[PUBLIC_LIST_TEST] {message}");
        }

        private sealed class TestOptions
        {
            internal bool Enabled { get; private set; }

            internal string ExpectedServerName { get; private set; }

            internal string ResultPath { get; private set; }

            internal string ScreenshotPath { get; private set; }

            internal float TimeoutSeconds { get; private set; } = 120f;

            internal bool QuitOnComplete { get; private set; }

            internal string ParseError { get; private set; }

            internal static TestOptions Parse(string[] arguments)
            {
                var options = new TestOptions();
                if (arguments == null)
                {
                    return options;
                }

                for (int i = 0; i < arguments.Length; i++)
                {
                    string argument = arguments[i];
                    if (string.Equals(argument, TEST_FLAG, StringComparison.Ordinal))
                    {
                        options.Enabled = true;
                    }
                    else if (string.Equals(argument, EXPECTED_SERVER_FLAG, StringComparison.Ordinal) && TryReadNext(arguments, i, out string serverName))
                    {
                        options.ExpectedServerName = serverName;
                    }
                    else if (string.Equals(argument, RESULT_FLAG, StringComparison.Ordinal) && TryReadNext(arguments, i, out string resultPath))
                    {
                        options.ResultPath = resultPath;
                    }
                    else if (string.Equals(argument, SCREENSHOT_FLAG, StringComparison.Ordinal) && TryReadNext(arguments, i, out string screenshotPath))
                    {
                        options.ScreenshotPath = screenshotPath;
                    }
                    else if (string.Equals(argument, TIMEOUT_FLAG, StringComparison.Ordinal) &&
                             TryReadNext(arguments, i, out string timeoutText) &&
                             float.TryParse(timeoutText, NumberStyles.Float, CultureInfo.InvariantCulture, out float timeoutSeconds))
                    {
                        options.TimeoutSeconds = Mathf.Clamp(timeoutSeconds, 30f, 600f);
                    }
                    else if (string.Equals(argument, QUIT_FLAG, StringComparison.Ordinal))
                    {
                        options.QuitOnComplete = true;
                    }
                }

                if (!options.Enabled)
                {
                    return options;
                }

                if (string.IsNullOrWhiteSpace(options.ExpectedServerName))
                {
                    options.ParseError = "missing-expected-server";
                }
                else if (string.IsNullOrWhiteSpace(options.ResultPath))
                {
                    options.ParseError = "missing-result-path";
                }
                else if (string.IsNullOrWhiteSpace(options.ScreenshotPath))
                {
                    options.ParseError = "missing-screenshot-path";
                }

                return options;
            }

            private static bool TryReadNext(string[] arguments, int index, out string value)
            {
                value = null;
                if (index + 1 >= arguments.Length)
                {
                    return false;
                }

                value = arguments[index + 1];
                return !string.IsNullOrWhiteSpace(value);
            }
        }
    }
}
