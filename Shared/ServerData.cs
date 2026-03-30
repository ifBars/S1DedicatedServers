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
		public string ServerDescription { get; set; } = "A dedicated server for Schedule One";
		public int CurrentPlayers { get; set; }
		public int MaxPlayers { get; set; }
		public bool AllowSleeping { get; set; } = false;
	}
}



