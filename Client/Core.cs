using MelonLoader;
using DedicatedServerMod.Client;

[assembly: MelonInfo(typeof(DedicatedServerMod.Core), "DedicatedServerClient", "1.0.0", "Bars")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace DedicatedServerMod
{
    /// <summary>
    /// Main entry point for the Dedicated Server Client mod.
    /// Minimal responsibilities - delegates to specialized managers.
    /// </summary>
    public class Core : MelonMod
    {
        private static MelonLogger.Instance logger;
        
        // Client managers
        private ClientConnectionManager connectionManager;
        private ClientTransportPatcher transportPatcher;
        private ClientPlayerSetup playerSetup;
        private ClientUIManager uiManager;
        private ClientQuestManager questManager;
        private ClientLoopbackHandler loopbackHandler;

        public override void OnInitializeMelon()
        {
            logger = LoggerInstance;
            logger.Msg("DedicatedServerClient mod initializing...");
            
            try
            {
                // Initialize all client managers
                InitializeManagers();
                
                logger.Msg("DedicatedServerClient mod initialized successfully");
            }
            catch (System.Exception ex)
            {
                logger.Error($"Failed to initialize DedicatedServerClient: {ex}");
            }
        }

        private void InitializeManagers()
        {
            // Initialize transport patcher first (needed for connection patching)
            transportPatcher = new ClientTransportPatcher(logger);
            transportPatcher.Initialize();

            // Initialize connection manager
            connectionManager = new ClientConnectionManager(logger);
            connectionManager.Initialize();

            // Initialize player setup handler
            playerSetup = new ClientPlayerSetup(logger);
            playerSetup.Initialize();

            // Initialize quest manager
            questManager = new ClientQuestManager(logger);
            questManager.Initialize();

            // Initialize loopback handler
            loopbackHandler = new ClientLoopbackHandler(logger);
            loopbackHandler.Initialize();

            // Initialize UI manager (depends on connection manager)
            uiManager = new ClientUIManager(logger, connectionManager);
            uiManager.Initialize();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            try
            {
                // Delegate scene handling to appropriate managers
                uiManager?.OnSceneLoaded(sceneName);
                questManager?.OnSceneLoaded(sceneName);
            }
            catch (System.Exception ex)
            {
                logger.Error($"Error handling scene load ({sceneName}): {ex}");
            }
        }

        public override void OnUpdate()
        {
            try
            {
                // Handle debug input
                HandleDebugInput();
            }
            catch (System.Exception ex)
            {
                logger.Error($"Error in OnUpdate: {ex}");
            }
        }

        private void HandleDebugInput()
        {
            // Debug key for testing (F9)
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F9))
            {
                logger.Msg("F9 pressed - triggering prototype connection");
                connectionManager?.StartDedicatedConnection();
            }
        }

        /// <summary>
        /// Public access to connection manager for other components
        /// </summary>
        public static ClientConnectionManager ConnectionManager => Instance?.connectionManager;
        
        /// <summary>
        /// Access to the Core instance
        /// </summary>
        public static Core Instance { get; private set; }

        public override void OnApplicationStart()
        {
            Instance = this;
        }
    }
}
