# Beta sleep compatibility regression checks

Run `dotnet run --project tests/SleepControllerPatchTests/SleepControllerPatchTests.csproj` with .NET 8 or newer.

The runner applies the production Harmony patches to small managed stand-ins. It checks both sleep event queues, retained gameplay events, interactive sessions, empty servers, consumed readiness votes, and direct generated-RPC calls that bypass the public wrapper. It also checks special-customer selection and save IDs against the case-sensitive native lookup.

These checks verify patch registration and policy; they do not simulate Unity, FishNet synchronization, or IL2CPP inlining. Runtime validation still requires a server and client on each runtime: hold the summary panels open, verify exactly one day advances on both peers, confirm the saved day, and reconnect after sleep. Include special-customer announcements and arrivals in the runtime check.
