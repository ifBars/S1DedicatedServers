namespace DedicatedServerMod.Shared.Networking.Messaging
{
    /// <summary>
    /// Available messaging backends for custom server-client communication.
    /// </summary>
    public enum MessagingBackendType
    {
        /// <summary>
        /// FishNet custom RPC messaging using DailySummary NetworkBehaviour.
        /// Works on both Mono and IL2CPP builds.
        /// </summary>
        FishNetRpc,

        /// <summary>
        /// Steam Networking Sockets transport using Steam client APIs on clients and
        /// Steam game-server APIs on dedicated servers.
        /// </summary>
        SteamNetworkingSockets
    }
}
