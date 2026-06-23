using System;
using DedicatedServerMod.Shared;

namespace DedicatedServerMod.Client.Managers
{
	/// <summary>
	/// Client-side storage for minimal server data shared by the server.
	/// </summary>
	internal static class ServerDataStore
	{
		internal static event Action<ServerData> OnUpdated;

		private static ServerData _current = new ServerData();
		internal static ServerData Current => _current;

		internal static bool AllowSleeping => _current?.AllowSleeping ?? true;

		internal static void Update(ServerData newData)
		{
			if (newData == null) return;
			_current = newData;
			try { OnUpdated?.Invoke(_current); } catch { }
		}

		internal static void Reset()
		{
			_current = new ServerData();
		}
	}
}


