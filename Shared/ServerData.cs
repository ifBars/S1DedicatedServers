namespace DedicatedServerMod.Shared
{
	/// <summary>
	/// Describes the small public server snapshot shared with clients during dedicated-server discovery and UI updates.
	/// </summary>
	/// <remarks>
	/// This DTO is intentionally limited to client-safe display data. Do not add secrets, operator-only
	/// settings, passwords, tokens, file paths, or full server configuration values to this type.
	/// </remarks>
	[Serializable]
	public class ServerData
	{
		/// <summary>
		/// Gets or sets the display name advertised for the server.
		/// </summary>
		public string ServerName { get; set; } = "Schedule One Dedicated Server";

		/// <summary>
		/// Gets or sets the short public description advertised for the server.
		/// </summary>
		public string ServerDescription { get; set; } = "A dedicated server for Schedule One";

		/// <summary>
		/// Gets or sets the number of currently connected players.
		/// </summary>
		public int CurrentPlayers { get; set; }

		/// <summary>
		/// Gets or sets the maximum number of players allowed by the server.
		/// </summary>
		public int MaxPlayers { get; set; }

		/// <summary>
		/// Gets or sets whether bed-based sleep advancement is currently allowed.
		/// </summary>
		public bool AllowSleeping { get; set; } = false;
	}
}



