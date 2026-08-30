using DedicatedServerMod.Server.Game.Patches.UI;

AssertVisibilityPolicy(visible: true, conversationCanBeHidden: false, expected: true);
AssertVisibilityPolicy(visible: true, conversationCanBeHidden: true, expected: true);
AssertVisibilityPolicy(visible: false, conversationCanBeHidden: true, expected: true);
AssertVisibilityPolicy(visible: false, conversationCanBeHidden: false, expected: false);

Console.WriteLine("PASS|HeadlessMessagingPolicyTests|cases=4");

static void AssertVisibilityPolicy(bool visible, bool conversationCanBeHidden, bool expected)
{
    bool actual = ConversationEntryVisibilityPolicy.ShouldApply(visible, conversationCanBeHidden);
    if (actual != expected)
    {
        throw new InvalidOperationException(
            $"Expected ShouldApply(visible: {visible}, conversationCanBeHidden: {conversationCanBeHidden}) " +
            $"to return {expected}, but it returned {actual}.");
    }
}
