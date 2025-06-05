using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Net.Http;

namespace CollectaMundo.Data.ScryfallLookups
{
    public class ScryfallLookups : IScryfallLookups
    {
        public async Task<JArray?> FetchSetMetadataAsync()
        {
            try
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
            catch (Exception ex)
            {
                Debug.WriteLine($"[PngLogic] Failed to fetch Scryfall set metadata: {ex.Message}");
                return null;
            }
        }
    }
}
