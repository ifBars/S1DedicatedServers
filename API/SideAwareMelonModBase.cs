using MelonLoader;

namespace DedicatedServerMod.API
{
	/// <summary>
	/// Convenience base for mods that want both server and client hooks in a single class.
	/// Inherit and override only what you need.
	/// </summary>
	public abstract class SideAwareMelonModBase : MelonMod, IServerMod, IClientMod
	{
		// Server-side hooks
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

		// Client-side hooks
		public virtual void OnClientInitialize() { }
		public virtual void OnClientShutdown() { }
		public virtual void OnConnectedToServer() { }
		public virtual void OnDisconnectedFromServer() { }
		public virtual void OnClientPlayerReady() { }
		public virtual bool OnCustomMessage(string messageType, byte[] data) { return false; }
		public virtual void OnUIUpdate() { }
		public virtual void OnServerEvent(string eventType, object eventData) { }
	}
}


