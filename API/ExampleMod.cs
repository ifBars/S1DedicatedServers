using MelonLoader;
using System;

[assembly: MelonInfo(typeof(DedicatedServerMod.API.ExampleMod), "DedicatedServerAPIExample", "1.0.0", "Bars")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace DedicatedServerMod.API
{
    /// <summary>
    /// Example mod demonstrating how to use the S1DS API
    /// This mod works on both server and client sides
    /// </summary>
    public class ExampleMod : SideAwareMelonModBase
    {
        public override void OnInitializeMelon()
        {
            // This is called when MelonLoader initializes the mod
            MelonLogger.Msg($"ExampleMod loaded! Build: {S1DS.BuildConfig}");

            // The ModManager will automatically discover and register this mod
            // because it implements IServerMod and/or IClientMod
        }

        #region Server Hooks

        public override void OnServerInitialize()
        {
            MelonLogger.Msg("ExampleMod: Server initialized!");

            // Server is initialized when this is called
            MelonLogger.Msg($"Server is running with {S1DS.Server.PlayerCount} players");
        }

        public override void OnServerShutdown()
        {
            MelonLogger.Msg("ExampleMod: Server shutting down!");
        }

        public override void OnPlayerConnected(string playerId)
        {
            MelonLogger.Msg($"ExampleMod: Player {playerId} connected!");

            // Shared systems are available during connection events
            MelonLogger.Msg($"Could send welcome message to {playerId}");
        }

        public override void OnPlayerDisconnected(string playerId)
        {
            MelonLogger.Msg($"ExampleMod: Player {playerId} disconnected!");
        }

        #endregion

        #region Client Hooks

        public override void OnClientInitialize()
        {
            MelonLogger.Msg("ExampleMod: Client initialized!");

            // Client is initialized when this is called
            MelonLogger.Msg("Client is ready!");
        }

        public override void OnClientShutdown()
        {
            MelonLogger.Msg("ExampleMod: Client shutting down!");
        }

        public override void OnConnectedToServer()
        {
            MelonLogger.Msg("ExampleMod: Connected to server!");

            // Client is connected when this is called
            MelonLogger.Msg("Ready to interact with server!");
        }

        public override void OnDisconnectedFromServer()
        {
            MelonLogger.Msg("ExampleMod: Disconnected from server!");
        }

        #endregion
    }
}
