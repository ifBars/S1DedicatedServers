# Persistence regression checks

Run `dotnet run --project tests/PersistenceManagerTests/PersistenceManagerTests.csproj` with .NET 8 or newer.

These checks compile the production `PersistenceManager` against managed stand-ins. A deterministic frame scheduler verifies that completion follows the native save, shutdown drains an in-flight save before its final save, queued requests do not run during shutdown, and save errors cancel shutdown. The checks also cover rejected saves, synchronous completion, exceptions, stopping before a world loads, and throwing completion subscribers. Subscriber failures must be logged while later subscribers, shutdown continuation, and retries still work.

Autosave coverage includes queued, running, completed, and early queued tokens promoted after loader initialization. The scheduler models the loader's invalid-token exception. Shutdown must avoid token cancellation, await its own final save, and retire superseded autosave loops after a failed shutdown is cancelled.

Native error coverage includes write failures that leave `SaveManager.SaveError` clear, errors across multiple frames, missing completion events, unrelated logs, preceding native saves, listener cleanup, bounded output, and removal of serialized save contents. These checks verify that a failed save does not advance the autosave timestamp or notify successful completion, and that a repaired save can be retried.

These are coordination tests. Live validation still requires loading an owned business with placed objects, saving, shutting down, restarting, and checking ownership and object identifiers. Test Mono and IL2CPP separately. Check that native save completion precedes player disconnect, network shutdown, and application quit. A forced process termination cannot wait for a final save.
