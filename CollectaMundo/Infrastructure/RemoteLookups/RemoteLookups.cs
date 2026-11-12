using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Xml.Linq;

namespace CollectaMundo.Infrastructure.RemoteLookups
{
    public class RemoteLookups : IRemoteLookups
    {
        // Hardcoded URL for the European Central Bank's daily exchange rates in XML format
        private const string EcbDailyRatesUrl = "https://www.ecb.europa.eu/stats/eurofxref/eurofxref-daily.xml";

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

            client.DefaultRequestHeaders.Add("User-Agent", "CollectaMundo/1.0 (https://github.com/PsykoFant/CollectaMundo)");
            client.DefaultRequestHeaders.Add("Accept", "application/json;q=0.9,*/*;q=0.8");

            using HttpResponseMessage response = await client.GetAsync("https://api.scryfall.com/sets");
            response.EnsureSuccessStatusCode();

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
            client.DefaultRequestHeaders.Add("User-Agent", "CollectaMundo/1.0 (https://github.com/PsykoFant/CollectaMundo)");
            client.DefaultRequestHeaders.Add("Accept", "application/json;q=0.9,*/*;q=0.8");

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
        public async Task<decimal> FetchUsdToEurRateAsync(CancellationToken cancelToken = default)
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(5)
            };

            using var response = await client.GetAsync(EcbDailyRatesUrl, cancelToken);
            response.EnsureSuccessStatusCode();

            var xml = await response.Content.ReadAsStringAsync(cancelToken);
            var document = XDocument.Parse(xml);

            var usdPerEurText = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "Cube" && (string?)element.Attribute("currency") == "USD")
                ?.Attribute("rate")
                ?.Value;

            if (!decimal.TryParse(usdPerEurText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var usdPerEurRate) || usdPerEurRate <= 0m)
            {
                throw new InvalidDataException("The ECB response did not contain a valid USD rate.");
            }

            return 1m / usdPerEurRate;
        }

    }
}
