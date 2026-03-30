using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader.Utils;
using Newtonsoft.Json;
using DedicatedServerMod.Utils;

namespace DedicatedServerMod.Client.Data
{
    /// <summary>
    /// Persists the client's favorite and recently joined dedicated servers.
    /// </summary>
    internal sealed class ClientServerListRepository
    {
        private const int HISTORY_LIMIT = 20;

        private readonly string _filePath;

        private ClientServerListState _state = new ClientServerListState();

        internal ClientServerListRepository()
        {
            _filePath = Path.Combine(MelonEnvironment.UserDataDirectory, "DedicatedServerClientServers.json");
        }

        internal event Action Changed;

        internal IReadOnlyList<SavedServerEntry> Favorites => _state.Favorites;

        internal IReadOnlyList<SavedServerEntry> History => _state.History;

        internal void Initialize()
        {
            Load();
            CleanupDuplicateEntries();
        }

        internal SavedServerEntry GetFavoriteById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            for (int i = 0; i < _state.Favorites.Count; i++)
            {
                SavedServerEntry entry = _state.Favorites[i];
                if (string.Equals(entry.Id, id, StringComparison.Ordinal))
                {
                    return Clone(entry);
                }
            }

            return null;
        }

        internal SavedServerEntry SaveFavorite(string existingId, string name, string host, int port)
        {
            string normalizedHost = NormalizeHost(host);
            string normalizedName = NormalizeName(name, normalizedHost, port);

            SavedServerEntry existing = null;
            if (!string.IsNullOrWhiteSpace(existingId))
            {
                existing = FindById(_state.Favorites, existingId);
            }

            if (existing == null)
            {
                existing = FindByEndpoint(_state.Favorites, normalizedHost, port);
            }

            if (existing == null)
            {
                existing = new SavedServerEntry();
                _state.Favorites.Insert(0, existing);
            }

            existing.Name = normalizedName;
            existing.Host = normalizedHost;
            existing.Port = port;

            MoveToFront(_state.Favorites, existing);
            Persist();
            return Clone(existing);
        }

        internal bool RemoveFavorite(string id)
        {
            return RemoveById(_state.Favorites, id);
        }

        internal bool RemoveHistory(string id)
        {
            return RemoveById(_state.History, id);
        }

        internal SavedServerEntry RecordJoinedServer(string host, int port, string preferredName)
        {
            string normalizedHost = NormalizeHost(host);
            string normalizedName = NormalizeName(preferredName, normalizedHost, port);

            SavedServerEntry existing = FindByEndpoint(_state.History, normalizedHost, port);
            if (existing == null)
            {
                existing = new SavedServerEntry();
                _state.History.Insert(0, existing);
            }

            existing.Name = normalizedName;
            existing.Host = normalizedHost;
            existing.Port = port;
            existing.LastJoinedUtc = DateTime.UtcNow;

            MoveToFront(_state.History, existing);
            TrimHistory();
            Persist();
            return Clone(existing);
        }

        internal void UpdateMetadata(string host, int port, string serverName, string serverDescription, int currentPlayers, int maxPlayers, int pingMilliseconds)
        {
            string normalizedHost = NormalizeHost(host);
            bool changed = UpdateListMetadata(_state.Favorites, normalizedHost, port, serverName, serverDescription, currentPlayers, maxPlayers, pingMilliseconds);
            changed |= UpdateListMetadata(_state.History, normalizedHost, port, serverName, serverDescription, currentPlayers, maxPlayers, pingMilliseconds);

            if (changed)
            {
                Persist();
            }
        }

        internal void MarkPingUnavailable(string host, int port)
        {
            string normalizedHost = NormalizeHost(host);
            bool changed = UpdatePingUnavailable(_state.Favorites, normalizedHost, port);
            changed |= UpdatePingUnavailable(_state.History, normalizedHost, port);

            if (changed)
            {
                Persist();
            }
        }

        private static bool UpdateListMetadata(List<SavedServerEntry> entries, string host, int port, string serverName, string serverDescription, int currentPlayers, int maxPlayers, int pingMilliseconds)
        {
            SavedServerEntry existing = FindByEndpoint(entries, host, port);
            if (existing == null)
            {
                return false;
            }

            bool changed = false;
            string normalizedServerName = NormalizeDescription(serverName);
            string normalizedServerDescription = NormalizeDescription(serverDescription);

            if (!string.Equals(existing.ServerName, normalizedServerName, StringComparison.Ordinal))
            {
                existing.ServerName = normalizedServerName;
                changed = true;
            }

            if (!string.Equals(existing.ServerDescription, normalizedServerDescription, StringComparison.Ordinal))
            {
                existing.ServerDescription = normalizedServerDescription;
                changed = true;
            }

            if (existing.CurrentPlayers != Math.Max(0, currentPlayers))
            {
                existing.CurrentPlayers = Math.Max(0, currentPlayers);
                changed = true;
            }

            if (existing.MaxPlayers != Math.Max(0, maxPlayers))
            {
                existing.MaxPlayers = Math.Max(0, maxPlayers);
                changed = true;
            }

            if (existing.PingMilliseconds != pingMilliseconds)
            {
                existing.PingMilliseconds = pingMilliseconds;
                changed = true;
            }

            if (changed)
            {
                existing.LastMetadataRefreshUtc = DateTime.UtcNow;
            }

            return changed;
        }

        private static bool UpdatePingUnavailable(List<SavedServerEntry> entries, string host, int port)
        {
            SavedServerEntry existing = FindByEndpoint(entries, host, port);
            if (existing == null || existing.PingMilliseconds == -1)
            {
                return false;
            }

            existing.PingMilliseconds = -1;
            existing.LastMetadataRefreshUtc = DateTime.UtcNow;
            return true;
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    _state = new ClientServerListState();
                    return;
                }

                string json = File.ReadAllText(_filePath);
                _state = JsonConvert.DeserializeObject<ClientServerListState>(json) ?? new ClientServerListState();
                _state.Favorites ??= new List<SavedServerEntry>();
                _state.History ??= new List<SavedServerEntry>();
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"Failed to load dedicated server list data: {ex.Message}");
                _state = new ClientServerListState();
            }
        }

        private void CleanupDuplicateEntries()
        {
            bool changed = CleanupList(_state.Favorites, enforceHistoryLimit: false);
            changed |= CleanupList(_state.History, enforceHistoryLimit: true);

            if (changed)
            {
                Persist();
            }
        }

        private bool CleanupList(List<SavedServerEntry> entries, bool enforceHistoryLimit)
        {
            bool changed = false;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = entries.Count - 1; i >= 0; i--)
            {
                SavedServerEntry entry = entries[i];
                if (entry == null)
                {
                    entries.RemoveAt(i);
                    changed = true;
                    continue;
                }

                entry.Id = string.IsNullOrWhiteSpace(entry.Id) ? Guid.NewGuid().ToString("N") : entry.Id;
                entry.Host = NormalizeHost(entry.Host);
                entry.Port = NormalizePort(entry.Port);
                entry.Name = NormalizeName(entry.Name, entry.Host, entry.Port);
                entry.ServerName = NormalizeDescription(entry.ServerName);
                entry.ServerDescription = NormalizeDescription(entry.ServerDescription);
                entry.PingMilliseconds = entry.PingMilliseconds < -1 ? -1 : entry.PingMilliseconds;
                entry.CurrentPlayers = Math.Max(0, entry.CurrentPlayers);
                entry.MaxPlayers = Math.Max(0, entry.MaxPlayers);

                string key = BuildEndpointKey(entry.Host, entry.Port);
                if (!seen.Add(key))
                {
                    entries.RemoveAt(i);
                    changed = true;
                }
            }

            if (enforceHistoryLimit && entries.Count > HISTORY_LIMIT)
            {
                entries.RemoveRange(HISTORY_LIMIT, entries.Count - HISTORY_LIMIT);
                changed = true;
            }

            return changed;
        }

        private bool RemoveById(List<SavedServerEntry> entries, string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (string.Equals(entries[i].Id, id, StringComparison.Ordinal))
                {
                    entries.RemoveAt(i);
                    Persist();
                    return true;
                }
            }

            return false;
        }

        private void Persist()
        {
            try
            {
                string directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonConvert.SerializeObject(_state, Formatting.Indented);
                File.WriteAllText(_filePath, json);
                Changed?.Invoke();
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"Failed to persist dedicated server list data: {ex.Message}");
            }
        }

        private void TrimHistory()
        {
            if (_state.History.Count > HISTORY_LIMIT)
            {
                _state.History.RemoveRange(HISTORY_LIMIT, _state.History.Count - HISTORY_LIMIT);
            }
        }

        private static SavedServerEntry FindById(List<SavedServerEntry> entries, string id)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (string.Equals(entries[i].Id, id, StringComparison.Ordinal))
                {
                    return entries[i];
                }
            }

            return null;
        }

        private static SavedServerEntry FindByEndpoint(List<SavedServerEntry> entries, string host, int port)
        {
            string key = BuildEndpointKey(host, port);
            for (int i = 0; i < entries.Count; i++)
            {
                SavedServerEntry entry = entries[i];
                if (string.Equals(BuildEndpointKey(entry.Host, entry.Port), key, StringComparison.OrdinalIgnoreCase))
                {
                    return entries[i];
                }
            }

            return null;
        }

        private static void MoveToFront(List<SavedServerEntry> entries, SavedServerEntry entry)
        {
            int index = entries.IndexOf(entry);
            if (index > 0)
            {
                entries.RemoveAt(index);
                entries.Insert(0, entry);
            }
        }

        private static SavedServerEntry Clone(SavedServerEntry entry)
        {
            if (entry == null)
            {
                return null;
            }

            return new SavedServerEntry
            {
                Id = entry.Id,
                Name = entry.Name,
                ServerName = entry.ServerName,
                Host = entry.Host,
                Port = entry.Port,
                ServerDescription = entry.ServerDescription,
                PingMilliseconds = entry.PingMilliseconds,
                CurrentPlayers = entry.CurrentPlayers,
                MaxPlayers = entry.MaxPlayers,
                LastMetadataRefreshUtc = entry.LastMetadataRefreshUtc,
                LastJoinedUtc = entry.LastJoinedUtc
            };
        }

        private static string NormalizeHost(string host)
        {
            return string.IsNullOrWhiteSpace(host) ? "localhost" : host.Trim();
        }

        private static int NormalizePort(int port)
        {
            return port > 0 && port <= 65535 ? port : 38465;
        }

        private static string NormalizeName(string name, string host, int port)
        {
            return string.IsNullOrWhiteSpace(name) ? $"{host}:{port}" : name.Trim();
        }

        private static string NormalizeDescription(string description)
        {
            return string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
        }

        private static string BuildEndpointKey(string host, int port)
        {
            return $"{NormalizeHost(host)}:{NormalizePort(port)}";
        }

        private sealed class ClientServerListState
        {
            public List<SavedServerEntry> Favorites { get; set; } = new List<SavedServerEntry>();

            public List<SavedServerEntry> History { get; set; } = new List<SavedServerEntry>();
        }
    }
}
