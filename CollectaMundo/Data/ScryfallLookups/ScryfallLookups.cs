using Newtonsoft.Json.Linq;
using System.Net.Http;

namespace CollectaMundo.Data.ScryfallLookups
{
    public class ScryfallLookups : IScryfallLookups
    {
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
            var match = setMetadata?
                .FirstOrDefault(x =>
                    x["code"]?.ToString().Equals(setCode, StringComparison.OrdinalIgnoreCase) == true);

            return match?["icon_svg_uri"]?.ToString();
        }
        public async Task<string?> FetchSvgContentAsync(string svgUrl)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "CollectaMundo/1.0");

            return await client.GetStringAsync(svgUrl);
        }

    }
}
