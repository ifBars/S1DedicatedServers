using DedicatedServerMod.Client.Managers;
using DedicatedServerMod.Client.Patches;
using DedicatedServerMod.Utils;
using Il2CppFishNet;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.UI;

int cases = 0;
foreach (bool dedicated in new[] { false, true })
foreach (bool client in new[] { false, true })
foreach (bool host in new[] { false, true })
foreach (bool allowed in new[] { false, true })
foreach (bool paused in new[] { false, true })
foreach (bool pressed in new[] { false, true })
foreach (bool open in new[] { false, true })
{
    DedicatedRuntimeContext.IsActive = dedicated;
    InstanceFinder.IsClient = client;
    InstanceFinder.IsHost = host;
    AdminStatusManager.Allowed = allowed;
    Singleton<PauseMenu>.Instance = new PauseMenu { IsPaused = paused };
    var ui = new ConsoleUI();
    ui.canvas.enabled = open;
    ui.ToggleConsoleReference.action.Performed = pressed;

    bool handled = dedicated && client && !host;
    bool toggle = handled && (open || allowed) && !paused && pressed;
    bool expectedOpen = toggle ? !open : open;
    Assert(ConsoleInputPatches.UpdatePrefix(ui) == !handled, "native Update delegation");
    Assert(ui.canvas.enabled == expectedOpen, "visibility");
    Assert(ui.ToggleCalls == (toggle ? 1 : 0), "single toggle");
    Assert(ui.HistoryCalls == (handled && expectedOpen ? 1 : 0), "command history");
    cases++;
}

DedicatedRuntimeContext.IsActive = true;
InstanceFinder.IsClient = true;
InstanceFinder.IsHost = false;
AdminStatusManager.Allowed = true;
Singleton<PauseMenu>.Instance = new PauseMenu();
var revokedAccess = new ConsoleUI();
revokedAccess.ToggleConsoleReference.action.Performed = true;
Assert(!ConsoleInputPatches.UpdatePrefix(revokedAccess) && revokedAccess.canvas.enabled, "open before revocation");
AdminStatusManager.Allowed = false;
revokedAccess.ToggleConsoleReference.action.Performed = false;
ConsoleInputPatches.UpdatePrefix(revokedAccess);
Assert(revokedAccess.canvas.enabled && revokedAccess.ToggleCalls == 1, "revocation without input does not toggle");
revokedAccess.ToggleConsoleReference.action.Performed = true;
ConsoleInputPatches.UpdatePrefix(revokedAccess);
Assert(!revokedAccess.canvas.enabled && revokedAccess.ToggleCalls == 2, "close after revocation");
ConsoleInputPatches.UpdatePrefix(revokedAccess);
Assert(!revokedAccess.canvas.enabled && revokedAccess.ToggleCalls == 2, "cannot reopen after revocation");
cases++;
AdminStatusManager.Allowed = true;
var missingCanvas = new ConsoleUI { canvas = null };
Assert(ConsoleInputPatches.UpdatePrefix(missingCanvas), "missing canvas delegates to game");
var missingInput = new ConsoleUI { ToggleConsoleReference = null };
Assert(!ConsoleInputPatches.UpdatePrefix(missingInput) && missingInput.ToggleCalls == 0, "missing input");
var missingAction = new ConsoleUI();
missingAction.ToggleConsoleReference.action = null;
Assert(!ConsoleInputPatches.UpdatePrefix(missingAction) && missingAction.ToggleCalls == 0, "missing action");
Singleton<PauseMenu>.Instance = null;
var missingPause = new ConsoleUI();
missingPause.ToggleConsoleReference.action.Performed = true;
Assert(!ConsoleInputPatches.UpdatePrefix(missingPause) && missingPause.ToggleCalls == 0, "missing pause menu");
Console.WriteLine($"PASS|ConsoleInputPatchTests|cases={cases + 4}");

static void Assert(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException(description);
}
