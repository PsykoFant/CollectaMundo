using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Net.Http;

namespace CollectaMundo.Data.RemoteLookups
{
    public class RemoteLookups : IRemoteLookups
    {
        public async Task<bool> IsInternetAvailableAsync(CancellationToken cancelToken = default)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var result = await client.GetAsync("https://www.google.com", cancelToken);
                return result.IsSuccessStatusCode;
            }
            catch (OperationCanceledException)
            {
                // Cancellation was requested — propagate or return false depending on behavior
                throw;
            }
            catch
            {
                // Network failure or other error
                return false;
            }
        }
        public async Task<JArray?> FetchSetMetadataAsync()
        {
            using var client = new HttpClient();

            // Set headers that Scryfall expects
            client.DefaultRequestHeaders.Add("User-Agent", "CollectaMundo/1.0 (https://github.com/your-org)");

            HttpResponseMessage response = await client.GetAsync("https://api.scryfall.com/sets/");
            response.EnsureSuccessStatusCode(); // throws if not 2xx

            string json = await response.Content.ReadAsStringAsync();
            JObject parsed = JObject.Parse(json);
            return parsed["data"] as JArray;
        }
        public string? TryGetIconUriForSetCode(JArray setMetadata, string setCode)
        {
            var match = setMetadata?.FirstOrDefault(x => x["code"]?.ToString().Equals(setCode, StringComparison.OrdinalIgnoreCase) == true);
            return match?["icon_svg_uri"]?.ToString();
        }
        public async Task<string?> FetchSvgContentAsync(string svgUrl)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "CollectaMundo/1.0");

            return await client.GetStringAsync(svgUrl);
        }
        public async Task<int> FetchSetsCountAsync(CancellationToken ct = default)
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.GetStringAsync("https://mtgjson.com/api/v5/SetList.json", ct);

            var json = JObject.Parse(response);
            var sets = json["data"] as JArray;
            int count = sets?.Count ?? 0;

            Debug.WriteLine($"Number of sets fetched: {count}");
            return count;
        }
        public async Task<bool> IsValidUrlAsync(string url)
        {
            try
            {
                using var client = new HttpClient();
                var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }


    }
}
