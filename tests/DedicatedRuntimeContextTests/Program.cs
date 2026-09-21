using DedicatedServerMod.Utils;

AssertPolicy(isServerBuild: false, isDedicatedClientSession: false, expected: false);
AssertPolicy(isServerBuild: false, isDedicatedClientSession: true, expected: true);
AssertPolicy(isServerBuild: true, isDedicatedClientSession: false, expected: true);
AssertPolicy(isServerBuild: true, isDedicatedClientSession: true, expected: true);

Console.WriteLine("PASS|DedicatedRuntimeContextTests|cases=4");

static void AssertPolicy(bool isServerBuild, bool isDedicatedClientSession, bool expected)
{
    bool actual = DedicatedRuntimeContext.ShouldApply(isServerBuild, isDedicatedClientSession);
    if (actual != expected)
    {
        throw new InvalidOperationException(
            $"Expected ShouldApply(isServerBuild: {isServerBuild}, " +
            $"isDedicatedClientSession: {isDedicatedClientSession}) to return {expected}, " +
            $"but it returned {actual}.");
    }
}
