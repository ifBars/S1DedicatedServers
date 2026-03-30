namespace DedicatedServerMod.Server.Game
{
    /// <summary>
    /// Describes the current Harmony patch state for dedicated-server game systems.
    /// </summary>
    public class PatchInfo
    {
        /// <summary>
        /// Gets or sets the total number of applied patches tracked by the manager.
        /// </summary>
        public int TotalPatches { get; set; }

        /// <summary>
        /// Gets or sets the applied patch labels reported by the manager.
        /// </summary>
        public List<string> AppliedPatches { get; set; }

        /// <summary>
        /// Gets or sets the Harmony instance identifier responsible for the patches.
        /// </summary>
        public string HarmonyId { get; set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"Patches: {TotalPatches} applied | Harmony ID: {HarmonyId}";
        }
    }
}
