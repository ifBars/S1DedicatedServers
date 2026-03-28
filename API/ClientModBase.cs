using MelonLoader;

namespace DedicatedServerMod.API
{
	/// <summary>
	/// Convenience base class for client-side API mods that do not need to derive from <see cref="MelonMod"/>.
	/// </summary>
	/// <remarks>
	/// Inherit from this type when another base class already owns your Melon lifecycle and you only
	/// need DedicatedServerMod client callbacks. Override the members relevant to your mod and use
	/// <see cref="S1DS.Client"/> to access client systems.
	/// </remarks>
	public abstract class ClientModBase : IClientMod
	{
		/// <inheritdoc />
		public virtual void OnClientInitialize() { }
		/// <inheritdoc />
		public virtual void OnClientShutdown() { }
		/// <inheritdoc />
		public virtual void OnConnectedToServer() { }
		/// <inheritdoc />
		public virtual void OnDisconnectedFromServer() { }
		/// <inheritdoc />
		public virtual void OnClientPlayerReady() { }
		/// <inheritdoc />
		public virtual bool OnCustomMessage(string messageType, byte[] data) { return false; }
	}

	/// <summary>
	/// Convenience base class for client-side mods that also need MelonLoader auto-discovery.
	/// </summary>
	/// <remarks>
	/// This is the usual base class for a dedicated-server-aware client mod. MelonLoader discovers
	/// the mod normally, while DedicatedServerMod invokes the <see cref="IClientMod"/> callbacks.
	/// </remarks>
	public abstract class ClientMelonModBase : MelonMod, IClientMod
	{
		/// <inheritdoc />
		public virtual void OnClientInitialize() { }
		/// <inheritdoc />
		public virtual void OnClientShutdown() { }
		/// <inheritdoc />
		public virtual void OnConnectedToServer() { }
		/// <inheritdoc />
		public virtual void OnDisconnectedFromServer() { }
		/// <inheritdoc />
		public virtual void OnClientPlayerReady() { }
		/// <inheritdoc />
		public virtual bool OnCustomMessage(string messageType, byte[] data) { return false; }
	}
}


