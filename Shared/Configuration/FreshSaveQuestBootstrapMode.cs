namespace DedicatedServerMod.Shared.Configuration
{
    /// <summary>
    /// Controls how a freshly prepared dedicated-server save initializes the vanilla quest line.
    /// </summary>
    public enum FreshSaveQuestBootstrapMode
    {
        /// <summary>
        /// Start from the original opening quest path, beginning with Welcome to Hyland Point.
        /// This matches the native game's intended first-time quest flow and is the recommended default.
        /// </summary>
        StartFromBeginning,

        /// <summary>
        /// Start from the post-intro checkpoint by activating Getting Started directly,
        /// after the dead-drop money pickup sequence and RV explosion/fix event.
        /// </summary>
        StartPostIntro
    }
}
