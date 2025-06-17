using System.Collections.Concurrent;
using System.Text.Json;
using static CollectaMundo.DomainLogic.CardPrices.CardPriceDefinitions;

namespace CollectaMundo.DomainLogic.CardPrices
{
    public static class CardPriceJsonParser
    {
        public static async Task<List<CardPrice>> ParseAllPricesAsync(JsonElement root)
        {
            var prices = new ConcurrentBag<CardPrice>();

            var tasks = CardPriceDefinitions.GetAllKeys().Select(key =>
                Task.Run(() =>
                {
                    var result = ParsePricesForSource(root, key);
                    foreach (var price in result)
                    {
                        prices.Add(price);
                    }
                })
            );

            await Task.WhenAll(tasks);
            return [.. prices];
        }
        public static List<CardPrice> ParsePricesForSource(JsonElement root, PriceSourceKey key)
        {
            var results = new List<CardPrice>();

            foreach (JsonProperty card in root.GetProperty("data").EnumerateObject())
            {
                string uuid = card.Name;

                if (!card.Value.TryGetProperty(key.Format, out JsonElement formatElement)) continue;
                if (!formatElement.TryGetProperty(key.Retailer, out JsonElement retailerElement)) continue;
                if (!retailerElement.TryGetProperty("retail", out JsonElement retailElement)) continue;
                if (!retailElement.TryGetProperty(key.Finish, out JsonElement finishElement)) continue;

                // Directly fetch the single price value
                if (finishElement.EnumerateObject().FirstOrDefault() is { } datePricePair)
                {
                    var price = datePricePair.Value.GetDecimal();

                    results.Add(new CardPrice
                    {
                        Uuid = uuid,
                        Format = key.Format,
                        Retailer = key.Retailer,
                        Finish = key.Finish,
                        Price = price
                    });
                }
            }

            return results;
        }
    }
}
