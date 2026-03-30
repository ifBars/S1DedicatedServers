#if IL2CPP
using Console = Il2CppScheduleOne.Console;
#else
using Console = ScheduleOne.Console;
#endif
namespace DedicatedServerMod.Server.Commands.Output
{
    /// <summary>
    /// Writes command output to the game console.
    /// </summary>
    public sealed class GameConsoleCommandOutput : ICommandOutput
    {
        /// <inheritdoc />
        public void WriteInfo(string message)
        {
            Console.Log(message);
        }

        /// <inheritdoc />
        public void WriteWarning(string message)
        {
            Console.LogWarning(message);
        }

        /// <inheritdoc />
        public void WriteError(string message)
        {
            Console.LogError(message);
        }
    }
}
