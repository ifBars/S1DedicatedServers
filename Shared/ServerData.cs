using System;

namespace DedicatedServerMod.Shared
{
	/// <summary>
	/// Minimal server information shared with clients.
	/// Do not include sensitive configuration; only what clients need.
	/// </summary>
	[Serializable]
	public class ServerData
	{
		public string ServerName { get; set; } = "Schedule One Dedicated Server";
		public bool AllowSleeping { get; set; } = false;
		public bool TimeNeverStops { get; set; } = false;
		public bool PublicServer { get; set; } = true;
	}
}


