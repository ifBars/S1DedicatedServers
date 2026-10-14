# Persistence regression checks

Run `dotnet run --project tests/PersistenceManagerTests/PersistenceManagerTests.csproj` with .NET 8 or newer.

These checks compile the production `PersistenceManager` against managed stand-ins. A deterministic frame scheduler verifies that completion follows the native save, shutdown drains an in-flight save before its final save, queued requests do not run during shutdown, and save errors cancel shutdown. The checks also cover rejected saves, synchronous completion, exceptions, and stopping before a world loads.

These are coordination tests. Live validation still requires loading an owned business with placed objects, saving, shutting down, restarting, and checking ownership and object identifiers. Test Mono and IL2CPP separately. Check that native save completion precedes player disconnect, network shutdown, and application quit. A forced process termination cannot wait for a final save.
