using System;
using System.Globalization;
using System.IO;

namespace DedicatedServerMod.Shared.Testing
{
    /// <summary>
    /// Shared file protocol for the opt-in real-game NPC ragdoll recovery test.
    /// </summary>
    internal static class NpcRagdollRecoveryTestProtocol
    {
        internal const string LOG_TAG = "[NPC_RAGDOLL_TEST]";
        internal const string CLIENT_READY_FILE = "client-ready.txt";
        internal const string TARGET_FILE = "target.txt";
        internal const string CLIENT_TARGET_READY_FILE = "client-target-ready.txt";
        internal const string SERVER_RESULT_FILE = "result-server.txt";
        internal const string CLIENT_RESULT_FILE = "result-client.txt";

        private const string TEST_FLAG = "--s1ds-npc-ragdoll-test";
        private const string RUN_DIRECTORY_FLAG = "--s1ds-npc-ragdoll-test-run-directory";
        private const string TIMEOUT_FLAG = "--s1ds-npc-ragdoll-test-timeout-seconds";

        internal static NpcRagdollRecoveryTestOptions ParseOptions(string[] arguments)
        {
            bool enabled = false;
            string runDirectory = null;
            float timeoutSeconds = 30f;
            string parseError = null;

            if (arguments != null)
            {
                for (int i = 0; i < arguments.Length; i++)
                {
                    string argument = arguments[i];
                    if (string.Equals(argument, TEST_FLAG, StringComparison.Ordinal))
                    {
                        enabled = true;
                    }
                    else if (string.Equals(argument, RUN_DIRECTORY_FLAG, StringComparison.Ordinal))
                    {
                        if (!TryReadNext(arguments, i, out runDirectory))
                        {
                            parseError = $"missing value for {RUN_DIRECTORY_FLAG}";
                        }
                        else
                        {
                            i++;
                        }
                    }
                    else if (string.Equals(argument, TIMEOUT_FLAG, StringComparison.Ordinal))
                    {
                        if (!TryReadNext(arguments, i, out string timeoutText))
                        {
                            parseError = $"invalid value for {TIMEOUT_FLAG}";
                        }
                        else
                        {
                            i++;
                            if (float.TryParse(
                                timeoutText,
                                NumberStyles.Float,
                                CultureInfo.InvariantCulture,
                                out float parsedTimeoutSeconds))
                            {
                                timeoutSeconds = parsedTimeoutSeconds;
                            }
                            else
                            {
                                parseError = $"invalid value for {TIMEOUT_FLAG}";
                            }
                        }
                    }
                }
            }

            if (enabled)
            {
                if (string.IsNullOrWhiteSpace(runDirectory))
                {
                    parseError = $"{RUN_DIRECTORY_FLAG} is required";
                }
                else if (!Path.IsPathRooted(runDirectory))
                {
                    parseError = $"{RUN_DIRECTORY_FLAG} must be an absolute path";
                }
                else
                {
                    runDirectory = Path.GetFullPath(runDirectory);
                }

                timeoutSeconds = Math.Max(10f, Math.Min(timeoutSeconds, 120f));
            }

            return new NpcRagdollRecoveryTestOptions(enabled, runDirectory, timeoutSeconds, parseError);
        }

        internal static string GetPath(NpcRagdollRecoveryTestOptions options, string fileName)
        {
            return Path.Combine(options.RunDirectory, fileName);
        }

        internal static void WriteFile(NpcRagdollRecoveryTestOptions options, string fileName, string content)
        {
            Directory.CreateDirectory(options.RunDirectory);
            File.WriteAllText(GetPath(options, fileName), content ?? string.Empty);
        }

        internal static string GetRuntimeName()
        {
#if IL2CPP
            return "Il2Cpp";
#else
            return "Mono";
#endif
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

    /// <summary>
    /// Parsed options for the opt-in real-game NPC ragdoll recovery test.
    /// </summary>
    internal sealed class NpcRagdollRecoveryTestOptions
    {
        internal NpcRagdollRecoveryTestOptions(bool enabled, string runDirectory, float timeoutSeconds, string parseError)
        {
            Enabled = enabled;
            RunDirectory = runDirectory;
            TimeoutSeconds = timeoutSeconds;
            ParseError = parseError;
        }

        internal bool Enabled { get; }

        internal string RunDirectory { get; }

        internal float TimeoutSeconds { get; }

        internal string ParseError { get; }
    }
}
