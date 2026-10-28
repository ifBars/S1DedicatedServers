namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch(Type type, string method) { }
    }
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class HarmonyPrefix : Attribute { }
}
namespace DedicatedServerMod.Utils
{
    internal static class DedicatedRuntimeContext { internal static bool IsActive; }
}
namespace DedicatedServerMod.Client.Managers
{
    internal static class AdminStatusManager
    {
        internal static bool Allowed;
        internal static bool CanOpenConsole() => Allowed;
    }
}
namespace Il2CppFishNet
{
    internal static class InstanceFinder { internal static bool IsClient; internal static bool IsHost; }
}
namespace Il2CppScheduleOne.DevUtilities
{
    internal static class Singleton<T> { internal static T Instance; }
}
namespace Il2CppScheduleOne.UI
{
    internal sealed class PauseMenu { internal bool IsPaused; }
    internal sealed class Canvas { internal bool enabled; }
    internal sealed class InputAction
    {
        internal bool Performed;
        internal bool WasPerformedThisFrame() => Performed;
    }
    internal sealed class InputReference { internal InputAction action = new InputAction(); }
    internal sealed class ConsoleUI
    {
        internal Canvas canvas = new Canvas();
        internal InputReference ToggleConsoleReference = new InputReference();
        internal int ToggleCalls;
        internal int HistoryCalls;
        internal void SetIsOpen(bool open) { ToggleCalls++; canvas.enabled = open; }
        internal void UpdateCommandHistory() { HistoryCalls++; }
    }
}
