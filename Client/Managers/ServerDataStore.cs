using System;
using DedicatedServerMod.Shared;

namespace DedicatedServerMod.Client.Managers
{
	/// <summary>
	/// Client-side storage for minimal server data shared by the server.
	/// </summary>
	public static class ServerDataStore
	{
		public static event Action<ServerData> OnUpdated;

		private static ServerData _current = new ServerData();
		public static ServerData Current => _current;

		public static bool AllowSleeping => _current?.AllowSleeping ?? true;

		public static void Update(ServerData newData)
		{
			if (newData == null) return;
			_current = newData;
			try { OnUpdated?.Invoke(_current); } catch { }
		}
	}
}


