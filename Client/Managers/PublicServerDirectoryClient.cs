using System.Net.Http;
using System.Text;
using DedicatedServerMod.Client.Data;
using DedicatedServerMod.Shared;
using DedicatedServerMod.Utils;
using Newtonsoft.Json;

namespace DedicatedServerMod.Client.Managers
{
    internal sealed class PublicServerDirectoryClient : IDisposable
    {
        private const int MAX_PAGES = 5;
        private const int PAGE_SIZE = 100;

        private readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        internal async Task<IReadOnlyList<SavedServerEntry>> GetServersAsync()
        {
            var servers = new List<SavedServerEntry>();
            string cursor = null;

            for (int page = 0; page < MAX_PAGES; page++)
            {
                Uri requestUri = BuildRequestUri(cursor);
                using HttpResponseMessage response = await _httpClient.GetAsync(requestUri).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                PublicServerListResponse pageResponse;
                try
                {
                    pageResponse = JsonConvert.DeserializeObject<PublicServerListResponse>(json);
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException("Public server directory returned malformed JSON.", ex);
                }

                if (pageResponse == null || !pageResponse.Success || pageResponse.Servers == null)
                {
                    throw new InvalidOperationException("Public server directory returned an invalid response.");
                }

                foreach (PublicServerDirectoryEntry entry in pageResponse.Servers)
                {
                    if (entry == null ||
                        entry.ProtocolVersion != PublicServerListProtocol.Version ||
                        string.IsNullOrWhiteSpace(entry.Host) ||
                        entry.Port < 1 ||
                        entry.Port > 65535)
                    {
                        continue;
                    }

                    servers.Add(new SavedServerEntry
                    {
                        Id = entry.ListingId,
                        Name = entry.ServerName,
                        ServerName = entry.ServerName,
                        ServerDescription = entry.ServerDescription,
                        Host = entry.Host,
                        Port = entry.Port,
                        CurrentPlayers = Math.Max(0, entry.CurrentPlayers),
                        MaxPlayers = Math.Max(0, entry.MaxPlayers),
                        StatusQueryMilliseconds = -1,
                        LastMetadataRefreshUtc = DateTime.UtcNow
                    });
                }

                cursor = pageResponse.NextCursor;
                if (string.IsNullOrWhiteSpace(cursor))
                {
                    break;
                }
            }

            return servers
                .OrderByDescending(entry => entry.CurrentPlayers)
                .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Releases the HTTP resources used to query the public server directory.
        /// </summary>
        public void Dispose()
        {
            _httpClient.Dispose();
        }

        private static Uri BuildRequestUri(string cursor)
        {
            var builder = new StringBuilder(Constants.DefaultPublicServerListServiceUrl.TrimEnd('/'));
            builder.Append("/api/v2/servers?limit=");
            builder.Append(PAGE_SIZE);
            if (!string.IsNullOrWhiteSpace(cursor))
            {
                builder.Append("&cursor=");
                builder.Append(Uri.EscapeDataString(cursor));
            }

            return new Uri(builder.ToString(), UriKind.Absolute);
        }
    }
}
