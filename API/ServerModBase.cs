using MelonLoader;

namespace DedicatedServerMod.API
{
	/// <summary>
	/// Convenience base for server-side mods. Provides no-op virtual implementations
	/// so inheritors override only the callbacks they care about.
	/// </summary>
	public abstract class ServerModBase : IServerMod
	{
		public virtual void OnServerInitialize() { }
		public virtual void OnServerStarted() { }
		public virtual void OnServerShutdown() { }
		public virtual void OnPlayerConnected(string playerId) { }
		public virtual void OnPlayerDisconnected(string playerId) { }
		public virtual void OnBeforeSave() { }
		public virtual void OnAfterSave() { }
		public virtual void OnBeforeLoad() { }
		public virtual void OnAfterLoad() { }
		public virtual bool OnCustomMessage(string messageType, byte[] data, string senderId) { return false; }
	}

	/// <summary>
	/// Convenience base which also derives from MelonMod for auto-discovery.
	/// If a mod derives from this class, it will be discovered by ModManager
	/// and receive server callbacks without needing to implement every method.
	/// </summary>
	public abstract class ServerMelonModBase : MelonMod, IServerMod
	{
		public virtual void OnServerInitialize() { }
		public virtual void OnServerStarted() { }
		public virtual void OnServerShutdown() { }
		public virtual void OnPlayerConnected(string playerId) { }
		public virtual void OnPlayerDisconnected(string playerId) { }
		public virtual void OnBeforeSave() { }
		public virtual void OnAfterSave() { }
		public virtual void OnBeforeLoad() { }
		public virtual void OnAfterLoad() { }
		public virtual bool OnCustomMessage(string messageType, byte[] data, string senderId) { return false; }
	}
}


