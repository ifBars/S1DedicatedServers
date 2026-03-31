using System.Reflection;
#if IL2CPP
using ConsoleArgumentList = Il2CppSystem.Collections.Generic.List<string>;
using ConsoleCommandDictionary = Il2CppSystem.Collections.Generic.Dictionary<string, Il2CppScheduleOne.Console.ConsoleCommand>;
using ConsoleType = Il2CppScheduleOne.Console;
#else
using ConsoleArgumentList = System.Collections.Generic.List<string>;
using ConsoleCommandDictionary = System.Collections.Generic.Dictionary<string, ScheduleOne.Console.ConsoleCommand>;
using ConsoleType = ScheduleOne.Console;
#endif

namespace DedicatedServerMod.Shared.ConsoleSupport
{
    /// <summary>
    /// Resolves runtime-specific Schedule I console members for both Mono and IL2CPP builds.
    /// </summary>
    internal static class GameConsoleAccess
    {
        internal static bool TryGetCommandDictionary(out ConsoleCommandDictionary commands)
        {
#if IL2CPP
            commands = ConsoleType.commands;
#else
            FieldInfo commandsField = typeof(ConsoleType).GetField("commands", BindingFlags.NonPublic | BindingFlags.Static);
            commands = commandsField?.GetValue(null) as ConsoleCommandDictionary;
#endif
            return commands != null;
        }

        internal static bool TryGetSubmitCommandArgumentListOverload(out MethodInfo method)
        {
            method = typeof(ConsoleType).GetMethod(
                "SubmitCommand",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(ConsoleArgumentList) },
                null);

            return method != null;
        }
    }
}
