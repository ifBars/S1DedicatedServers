using DedicatedServerMod.API;
using DedicatedServerMod.Server.Persistence;
using DedicatedServerMod.Shared.Configuration;
using MelonLoader;
using ScheduleOne.Persistence;

int cases = 0;
foreach (string state in new[] { "queued", "running", "completed", "promoted" })
{
    Run($"shutdown awaits its own final save with {state} autosave", () =>
    {
        var manager = Create();
        MelonCoroutines.QueueStarts = state == "queued" || state == "promoted";
        manager.Initialize();
        if (state == "completed")
        {
            ServerConfig.Instance.AutoSaveEnabled = false;
            MelonCoroutines.Tick(60);
        }
        if (state == "promoted") MelonCoroutines.FlushQueued();

        bool? shutdown = null;
        MelonCoroutines.Start(manager.SaveForShutdown(success => shutdown = success));
        MelonCoroutines.FlushQueued();
        Check(shutdown == null && SaveManager.Instance.Calls == 1);
        MelonCoroutines.Tick(120);
        Check(shutdown == null && SaveManager.Instance.Calls == 1);
        SaveManager.Instance.Finish();
        MelonCoroutines.Tick();
        Check(shutdown == true && ModManager.AfterSaves == 1);
        manager.Shutdown();
        MelonCoroutines.Tick(120);
        Check(MelonCoroutines.ActiveCount == 0 && MelonCoroutines.StopCalls == 0);
    });
}
Run("failed shutdown restarts only one autosave generation", () =>
{
    var manager = Create();
    manager.Initialize();
    bool? shutdown = null;
    MelonCoroutines.Start(manager.SaveForShutdown(success => shutdown = success));
    SaveManager.Instance.Finish(error: true);
    MelonCoroutines.Tick();
    Check(shutdown == false);
    manager.CancelShutdown();
    MelonCoroutines.Tick(60);
    Check(SaveManager.Instance.Calls == 2 && manager.SaveInProgress);
    Check(MelonLogger.Instance.Errors.Count == 1);
    SaveManager.Instance.Finish();
    MelonCoroutines.Tick();
    Check(ModManager.AfterSaves == 1 && MelonCoroutines.ActiveCount == 1);
    manager.Shutdown();
    MelonCoroutines.Tick(60);
    Check(SaveManager.Instance.Calls == 2 && MelonCoroutines.ActiveCount == 0);
});
Run("direct persistence teardown retires an early autosave without stopping its token", () =>
{
    var manager = Create();
    MelonCoroutines.QueueStarts = true;
    manager.Initialize();
    MelonCoroutines.FlushQueued();
    manager.Shutdown();
    MelonCoroutines.Tick(60);
    Check(SaveManager.Instance.Calls == 0 && MelonCoroutines.ActiveCount == 0 && MelonCoroutines.StopCalls == 0);
});
Run("a queued old loop cannot start after shutdown is cancelled", () =>
{
    var manager = Create();
    MelonCoroutines.QueueStarts = true;
    manager.Initialize();
    LoadManager.Instance.IsGameLoaded = false;
    bool? shutdown = null;
    var finalSave = manager.SaveForShutdown(success => shutdown = success);
    Check(!finalSave.MoveNext() && shutdown == true);
    manager.CancelShutdown();
    LoadManager.Instance.IsGameLoaded = true;
    MelonCoroutines.FlushQueued();
    Check(MelonCoroutines.ActiveCount == 1);
    MelonCoroutines.Tick(60);
    Check(SaveManager.Instance.Calls == 1);
});
Run("completion follows native save, including saves longer than 30 seconds", () =>
{
    var manager = Create();
    bool? completed = null;
    manager.SaveCompleted += (_, _, success) => completed = success;
    manager.TriggerManualSave("long-save");
    for (int frame = 0; frame < 40; frame++) MelonCoroutines.Tick();
    Check(manager.SaveInProgress && completed == null && ModManager.AfterSaves == 0);
    SaveManager.Instance.Finish();
    MelonCoroutines.Tick();
    Check(!manager.SaveInProgress && completed == true && ModManager.AfterSaves == 1);
    Check(SaveManager.Instance.onSaveComplete.ListenerCount == 0);
});
Run("shutdown drains a current save then completes a separate final save", () =>
{
    var manager = Create();
    manager.TriggerManualSave("in-flight");
    manager.TriggerManualSave("queued");
    bool? shutdown = null;
    MelonCoroutines.Start(manager.SaveForShutdown(success => shutdown = success));
    for (int frame = 0; frame < 7; frame++) MelonCoroutines.Tick();
    Check(shutdown == null && SaveManager.Instance.Calls == 1);
    SaveManager.Instance.Finish();
    MelonCoroutines.Tick();
    Check(shutdown == null && SaveManager.Instance.Calls == 2);
    manager.TriggerAutoSave("late-auto");
    manager.TriggerManualSave("late-manual");
    Check(SaveManager.Instance.Calls == 2);
    SaveManager.Instance.Finish();
    MelonCoroutines.Tick();
    Check(shutdown == true && !manager.SaveInProgress && SaveManager.Instance.Calls == 2);
});
Run("native saves outside the manager are drained before the final save", () =>
{
    var manager = Create();
    SaveManager.Instance.IsSaving = true;
    bool? shutdown = null;
    MelonCoroutines.Start(manager.SaveForShutdown(success => shutdown = success));
    MelonCoroutines.Tick();
    Check(SaveManager.Instance.Calls == 0 && shutdown == null);
    SaveManager.Instance.Finish();
    MelonCoroutines.Tick();
    Check(SaveManager.Instance.Calls == 1 && shutdown == null);
    SaveManager.Instance.Finish();
    MelonCoroutines.Tick();
    Check(shutdown == true);
});
Run("save errors cancel shutdown and allow a retry", () =>
{
    var manager = Create();
    bool? shutdown = null;
    MelonCoroutines.Start(manager.SaveForShutdown(success => shutdown = success));
    SaveManager.Instance.Finish(error: true);
    MelonCoroutines.Tick();
    Check(shutdown == false && ModManager.AfterSaves == 0);
    manager.CancelShutdown();
    manager.TriggerManualSave("retry");
    SaveManager.Instance.Finish();
    MelonCoroutines.Tick();
    Check(ModManager.AfterSaves == 1 && !manager.SaveInProgress);
});
Run("a native no-op is reported as a failed save", () =>
{
    var manager = Create();
    SaveManager.Instance.RejectSave = true;
    bool? completed = null;
    manager.SaveCompleted += (_, _, success) => completed = success;
    manager.TriggerManualSave("rejected");
    Check(completed == false && !manager.SaveInProgress && ModManager.AfterSaves == 0);
});
Run("synchronous native completion is observed", () =>
{
    var manager = Create();
    SaveManager.Instance.CompleteSynchronously = true;
    bool? completed = null;
    manager.SaveCompleted += (_, _, success) => completed = success;
    manager.TriggerManualSave("synchronous");
    Check(completed == true && !manager.SaveInProgress);
});
Run("save exceptions release the manager and remove listeners", () =>
{
    var manager = Create();
    SaveManager.Instance.ThrowOnSave = true;
    bool? completed = null;
    manager.SaveCompleted += (_, _, success) => completed = success;
    manager.TriggerManualSave("exception");
    Check(completed == false && !manager.SaveInProgress);
    Check(SaveManager.Instance.onSaveStart.ListenerCount == 0 && SaveManager.Instance.onSaveComplete.ListenerCount == 0);
});
Run("shutdown before world load does not try to save", () =>
{
    var manager = Create();
    LoadManager.Instance.IsGameLoaded = false;
    bool? shutdown = null;
    MelonCoroutines.Start(manager.SaveForShutdown(success => shutdown = success));
    Check(shutdown == true && SaveManager.Instance.Calls == 0);
});
foreach (bool saveError in new[] { false, true })
{
    Run($"throwing save subscribers do not interrupt shutdown continuation (saveError={saveError})", () =>
    {
        var manager = Create();
        int observed = 0;
        manager.SaveCompleted += (_, _, _) => throw new InvalidOperationException("injected subscriber failure");
        manager.SaveCompleted += (_, _, _) => observed++;
        bool? resumed = null;
        MelonCoroutines.Start(ObserveShutdown(manager, success => resumed = success));
        SaveManager.Instance.Finish(error: saveError);
        MelonCoroutines.Tick();
        Check(resumed == !saveError && observed == 1 && !manager.SaveInProgress);
        Check(SaveManager.Instance.onSaveStart.ListenerCount == 0 && SaveManager.Instance.onSaveComplete.ListenerCount == 0);
        Check(MelonLogger.Instance.Errors.Count(message => message.Contains("injected subscriber failure")) == 1);

        if (saveError)
        {
            manager.CancelShutdown();
            resumed = null;
            MelonCoroutines.Start(ObserveShutdown(manager, success => resumed = success));
            SaveManager.Instance.Finish();
            MelonCoroutines.Tick();
            Check(resumed == true && observed == 2 && !manager.SaveInProgress);
        }
    });
}
Console.WriteLine($"PASS|PersistenceManagerTests|cases={cases}");

static System.Collections.IEnumerator ObserveShutdown(PersistenceManager manager, Action<bool> resumed)
{
    bool saved = false;
    yield return manager.SaveForShutdown(success => saved = success);
    resumed(saved);
}

void Run(string name, Action test)
{
    try { test(); cases++; }
    catch (Exception ex) { throw new Exception(name, ex); }
}
static void Check(bool condition)
{
    if (!condition) throw new InvalidOperationException("Unexpected persistence state");
}
static PersistenceManager Create()
{
    MelonCoroutines.Reset();
    SaveManager.Instance = new();
    SaveManager.SaveError = false;
    LoadManager.Instance = new();
    ServerConfig.Instance = new();
    FishNet.InstanceFinder.IsServer = true;
    ModManager.AfterSaves = 0;
    MelonLogger.Instance.Errors.Clear();
    return new PersistenceManager(new MelonLogger.Instance());
}
