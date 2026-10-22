using System.Collections;

namespace UnityEngine
{
    public enum LogType { Error, Assert, Warning, Log, Exception }
    public static class Application
    {
        public delegate void LogCallback(string message, string stackTrace, LogType type);
        public static event LogCallback logMessageReceived;
        public static int ListenerCount => logMessageReceived?.GetInvocationList().Length ?? 0;
        public static void Log(string message, string stackTrace = "", LogType type = LogType.Error) =>
            logMessageReceived?.Invoke(message, stackTrace, type);
    }
    public static class Time { public static float realtimeSinceStartup; }
    public sealed class WaitForSeconds(float seconds) { public float Seconds = seconds; }
}

namespace UnityEngine.Events
{
    public delegate void UnityAction();
    public sealed class UnityEvent
    {
        private event UnityAction Listeners;
        public int ListenerCount => Listeners?.GetInvocationList().Length ?? 0;
        public void AddListener(UnityAction listener) => Listeners += listener;
        public void RemoveListener(UnityAction listener) => Listeners -= listener;
        public void Invoke() => Listeners?.Invoke();
    }
}

namespace FishNet { public static class InstanceFinder { public static bool IsServer = true; } }

namespace ScheduleOne.Persistence
{
    public sealed class LoadManager
    {
        public static LoadManager Instance = new();
        public bool IsGameLoaded = true;
        public string LoadedGameFolderPath = "disposable-save";
    }

    public sealed class SaveManager
    {
        public static SaveManager Instance = new();
        public static bool SaveError;
        public bool IsSaving;
        public bool RejectSave;
        public bool CompleteSynchronously;
        public bool ThrowOnSave;
        public string ErrorDuringSave;
        public int Calls;
        public UnityEngine.Events.UnityEvent onSaveStart = new();
        public UnityEngine.Events.UnityEvent onSaveComplete = new();
        public void Save()
        {
            Calls++;
            if (ThrowOnSave) throw new IOException("injected save failure");
            if (RejectSave || IsSaving) return;
            SaveError = false;
            IsSaving = true;
            onSaveStart.Invoke();
            if (ErrorDuringSave != null) UnityEngine.Application.Log(ErrorDuringSave);
            if (CompleteSynchronously) Finish();
        }
        public void Finish(bool error = false)
        {
            SaveError = error;
            IsSaving = false;
            onSaveComplete.Invoke();
        }
    }
}

namespace MelonLoader
{
    public static class MelonLogger
    {
        public sealed class Instance
        {
            public static readonly List<string> Errors = new();
            public void Msg(string text) { }
            public void Warning(string text) { }
            public void Error(string text) => Errors.Add(text);
        }
    }

    public static class MelonCoroutines
    {
        private sealed class Routine(IEnumerator iterator)
        {
            internal Stack<IEnumerator> Stack = new(new[] { iterator });
            internal float ResumeAt;
            internal bool Queued;
            internal bool EnumeratorToken;
        }
        private static readonly List<Routine> Routines = new();
        public static bool QueueStarts;
        public static int StopCalls;
        public static int ActiveCount => Routines.Count;
        public static object Start(IEnumerator iterator)
        {
            var routine = new Routine(iterator) { Queued = QueueStarts, EnumeratorToken = QueueStarts };
            Routines.Add(routine);
            if (!routine.Queued) Advance(routine);
            return routine;
        }
        public static void Stop(object token)
        {
            StopCalls++;
            var routine = (Routine)token;
            if ((!QueueStarts && routine.EnumeratorToken) || !Routines.Contains(routine))
                throw new NullReferenceException("routine is null");
            Routines.Remove(routine);
        }
        public static void FlushQueued()
        {
            QueueStarts = false;
            foreach (var routine in Routines.Where(routine => routine.Queued).ToArray())
            {
                routine.Queued = false;
                Advance(routine);
            }
        }
        public static void Tick(float seconds = 1f)
        {
            UnityEngine.Time.realtimeSinceStartup += seconds;
            foreach (var routine in Routines.ToArray())
                if (Routines.Contains(routine) && !routine.Queued && routine.ResumeAt <= UnityEngine.Time.realtimeSinceStartup)
                    Advance(routine);
        }
        public static void Reset()
        {
            Routines.Clear();
            QueueStarts = false;
            StopCalls = 0;
            UnityEngine.Time.realtimeSinceStartup = 0;
        }
        private static void Advance(Routine routine)
        {
            while (routine.Stack.Count > 0)
            {
                var current = routine.Stack.Peek();
                if (!current.MoveNext()) { routine.Stack.Pop(); continue; }
                if (current.Current is IEnumerator child) { routine.Stack.Push(child); continue; }
                routine.ResumeAt = UnityEngine.Time.realtimeSinceStartup +
                    (current.Current is UnityEngine.WaitForSeconds wait ? wait.Seconds : 0);
                return;
            }
            Routines.Remove(routine);
        }
    }
}

namespace DedicatedServerMod.Shared.Configuration
{
    public sealed class ServerConfig
    {
        public static ServerConfig Instance = new();
        public bool AutoSaveEnabled = true;
        public float AutoSaveIntervalMinutes = 1;
        public bool AutoSaveOnPlayerJoin;
        public bool AutoSaveOnPlayerLeave;
    }
}

namespace DedicatedServerMod.API
{
    public static class ModManager
    {
        public static int AfterSaves;
        public static void NotifyBeforeSave() { }
        public static void NotifyAfterSave() => AfterSaves++;
    }
}
