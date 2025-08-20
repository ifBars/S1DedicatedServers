using MelonLoader;

namespace DedicatedServerMod.API
{
	/// <summary>
	/// Convenience base for client-side mods. Provides no-op virtual implementations.
	/// </summary>
	public abstract class ClientModBase : IClientMod
	{
		public virtual void OnClientInitialize() { }
		public virtual void OnClientShutdown() { }
		public virtual void OnConnectedToServer() { }
		public virtual void OnDisconnectedFromServer() { }
		public virtual void OnClientPlayerReady() { }
		public virtual bool OnCustomMessage(string messageType, byte[] data) { return false; }
		public virtual void OnUIUpdate() { }
		public virtual void OnServerEvent(string eventType, object eventData) { }
	}

	/// <summary>
	/// Convenience base which also derives from MelonMod for auto-discovery on client.
	/// </summary>
	public abstract class ClientMelonModBase : MelonMod, IClientMod
	{
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


