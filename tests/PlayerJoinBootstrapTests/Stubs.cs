using System.Collections;

namespace FishNet.Connection
{
    internal sealed class NetworkConnection { internal int ClientId = 1; }
}

namespace UnityEngine
{
    internal static class Time { internal static float realtimeSinceStartup; }
    internal sealed class WaitForSecondsRealtime { internal WaitForSecondsRealtime(float seconds) { } }
    internal class Object
    {
        internal static object[] Instances = Array.Empty<object>();
        internal static bool IncludedInactive;
        internal static T[] FindObjectsOfType<T>(bool includeInactive)
        {
            IncludedInactive = includeInactive;
            return Instances.OfType<T>().ToArray();
        }
    }
}

namespace ScheduleOne.DevUtilities
{
    internal static class Singleton<T> { internal static T Instance { get; set; } }
    internal static class NetworkSingleton<T> { internal static T Instance { get; set; } }
}

namespace ScheduleOne.Persistence
{
    internal sealed class LoadManager { internal bool IsLoading; internal bool IsGameLoaded; }
}

namespace ScheduleOne.GameTime
{
    internal sealed class TimeManager { internal int ElapsedDays { get; set; } internal int CurrentTime { get; set; } }
}

namespace ScheduleOne.Economy
{
    internal sealed class Dealer : UnityEngine.Object
    {
        internal string ID = "dealer";
        internal bool HasBeenRecommended;
        internal bool IsRecruited;
        internal bool IsSpawned = true;
        internal int Replays;
        internal bool ThrowOnReplay;
        internal void SetRecommended()
        {
            if (ThrowOnReplay) throw new InvalidOperationException("Synthetic dealer RPC failure");
            Replays++;
        }
    }
}

namespace ScheduleOne.Quests
{
    internal enum EQuestState { Inactive, Active }
    internal sealed class QuestEntry { internal EQuestState State { get; set; } }
    internal sealed class Quest
    {
        internal Guid GUID { get; set; }
        internal List<QuestEntry> Entries = new();
        internal EQuestState State { get; set; }
        internal bool IsTracked { get; set; }
    }
    internal sealed class QuestManager
    {
        internal List<Quest> DefaultQuests = new();
        internal void ReceiveQuestEntryState(FishNet.Connection.NetworkConnection connection, string guid, int index, EQuestState state) { }
        internal void ReceiveQuestState(FishNet.Connection.NetworkConnection connection, string guid, EQuestState state) { }
        internal void SetQuestTracked(FishNet.Connection.NetworkConnection connection, string guid, bool tracked) { }
    }
}

namespace DedicatedServerMod.Server.Player
{
    internal sealed class PlayerStub { internal bool PlayerLoaded; }
    internal sealed class ConnectedPlayerInfo
    {
        internal FishNet.Connection.NetworkConnection Connection = new();
        internal int ClientId = 1;
        internal bool IsLoopbackConnection;
        internal PlayerStub PlayerInstance = new();
    }
}

namespace DedicatedServerMod.Server.Player.Runtime
{
    internal sealed class PlayerSessionRegistry
    {
        internal bool Active = true;
        internal bool IsTrackedPlayerActive(ConnectedPlayerInfo player) => Active;
        internal int GetVisiblePlayerCount() => 1;
    }
}

namespace DedicatedServerMod.Shared
{
    internal sealed class ServerData
    {
        internal string ServerName { get; set; } internal string ServerDescription { get; set; }
        internal int CurrentPlayers, MaxPlayers;
        internal bool AllowSleeping { get; set; }
    }
}

namespace DedicatedServerMod.Shared.Configuration
{
    internal sealed class ServerConfig
    {
        internal static ServerConfig Instance = new();
        internal string ServerName { get; set; } internal string ServerDescription { get; set; }
        internal int MaxPlayers { get; set; }
        internal bool AllowSleeping { get; set; }
    }
}

namespace DedicatedServerMod.Shared.Networking
{
    internal static class CustomMessaging
    {
        internal static void SendToClient(FishNet.Connection.NetworkConnection connection, string command, string data) { }
    }
}

namespace DedicatedServerMod.Utils
{
    internal static class Constants { internal static class Messages { internal const string ServerData = "server_data"; } }
    internal static class DebugLog
    {
        internal static readonly List<string> Warnings = new();
        internal static void Warning(string message) => Warnings.Add(message);
        internal static void Info(string message) { }
        internal static void PlayerLifecycleDebug(string message) { }
    }
}

namespace HarmonyLib
{
    internal static class AccessTools
    {
        internal static System.Reflection.MethodInfo Method(Type type, string name, Type[] args) => null;
    }
}

namespace MelonLoader
{
    internal static class MelonCoroutines
    {
        internal static readonly List<IEnumerator> Pending = new();
        internal static void Start(IEnumerator routine) => Pending.Add(routine);
        internal static void Tick()
        {
            foreach (IEnumerator routine in Pending.ToArray())
            {
                if (!routine.MoveNext()) Pending.Remove(routine);
            }
            UnityEngine.Time.realtimeSinceStartup += 0.25f;
        }
    }
}

namespace Newtonsoft.Json
{
    internal static class JsonConvert { internal static string SerializeObject(object value) => "{}"; }
}
