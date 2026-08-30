namespace DedicatedServerMod.Server.Game.Patches.UI
{
    internal static class ConversationEntryVisibilityPolicy
    {
        internal static bool ShouldApply(bool visible, bool conversationCanBeHidden)
        {
            return visible || conversationCanBeHidden;
        }
    }
}
