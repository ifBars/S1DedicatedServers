namespace DedicatedServerMod.Utils
{
    /// <summary>
    /// Documents the native ScheduleOne invariants that the dedicated server mod
    /// must preserve for compatibility. These are locked requirements derived from
    /// analysis of LoadManager, Player, ReplicationQueue, and Lobby.
    ///
    /// Any change to the dedicated server flow that violates these invariants
    /// will cause client load failures.
    ///
    /// Native LoadAsClient sequence (Persistence/LoadManager.cs:642-727):
    ///   1. ActiveSaveInfo = null, IsLoading = true, LoadStatus = LoadingScene
    ///   2. LoadingScreen.Open()
    ///   3. onPreSceneChange.Invoke() -> triggers 10+ cleanup listeners
    ///   4. CleanUp() -> clears GUIDManager, Quest lists, PlayerList, staggeredReplicators, etc.
    ///   5. Transport configured + SetTimeout(30f)
    ///   6. StartConnection()
    ///   7. Player.onLocalPlayerSpawned += handler (AFTER CleanUp which clears it)
    ///   8. Wait Main scene
    ///   9. onPreLoad.Invoke()
    ///  10. LoadStatus = SpawningPlayer, wait Player.Local != null
    ///  11. LoadStatus = LoadingData, wait playerDataRetrieveReturned
    ///  12. LoadStatus = Initializing, wait ReplicationDoneForLocalPlayer or timeout (45s)
    ///  13. onLoadComplete.Invoke() -> triggers 20+ loaders and game systems
    ///  14. WaitForSeconds(1f)
    ///  15. LoadStatus = None, LoadingScreen.Close(), IsGameLoaded = true
    ///
    /// onPreSceneChange subscribers: SaveManager.Clean, ProductManager.Clean, MusicPlayer,
    ///   AudioManager, NPCManager, LawManager, GameInput, Registry.RemoveRuntimeItems,
    ///   TimeManager.Clean, StateMachine.Clean, MessagesApp.Clean, S1API.GameLifecycle.
    ///
    /// onLoadComplete subscribers: ~20 Persistence loaders, LoadEventTransmitter,
    ///   DarkMarket, MessagesApp, MoneyManager, Ray, Oscar, Marco, Fixer, Jeremy, Stan,
    ///   LawController, Quest_SinkOrSwim, ConfigurationReplicator (x9), S1API.GameLifecycle.
    ///
    /// playerDataRetrieveReturned dependencies: ItemPickup, NetworkedItemPickup wait on it;
    ///   QuestManager (server side) waits on it per-connection before syncing quests.
    /// </summary>
    public static class NativeInvariants
    {
        public const string CLEANUP_BEFORE_CONNECT =
            "onPreSceneChange + CleanUp must precede StartConnection; CleanUp clears Player.onLocalPlayerSpawned";

        public const string PLAYER_DATA_GATE =
            "playerDataRetrieveReturned must be true before LoadStatus advances past LoadingData";

        public const string REPLICATION_GATE =
            "ReplicationDoneForLocalPlayer or 45s timeout before onLoadComplete";

        public const string LOAD_COMPLETE_REQUIRED =
            "onLoadComplete must fire after replication for loaders to configure world objects";

        public const string GHOST_HOST_REQUIRED =
            "Loopback ghost host required; native server code assumes Player.Local exists";

        public const string LOADING_SCREEN_DRIVEN_BY_STATUS =
            "LoadingScreen.Update reads LoadManager.GetLoadStatusText; LoadStatus must progress correctly";

        public const float TRANSPORT_TIMEOUT_SECONDS = 30f;
        public const int REPLICATION_TIMEOUT_SECONDS = 45;
        public const float POST_LOAD_DELAY_SECONDS = 1f;
    }
}
