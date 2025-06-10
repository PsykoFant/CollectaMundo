using System.Text.Json;

namespace CollectaMundo.DomainLogic
{
    public static class CardPriceJsonParser
    {
        // Extracts a dictionary of UUID -> price for a specific retailer + price type (e.g., "cardmarketNormal")
        public static Dictionary<string, decimal> ParsePriceList(JsonElement pricesRoot, string columnName)
        {
            var result = new Dictionary<string, decimal>();

            if (!pricesRoot.TryGetProperty("data", out JsonElement dataElement))
                return result;

            foreach (JsonProperty card in dataElement.EnumerateObject())
            {
                string uuid = card.Name;
                if (card.Value.TryGetProperty(columnName, out JsonElement priceValue))
                {
                    decimal? parsed = GetDecimalValue(priceValue);
                    if (parsed.HasValue)
                        result[uuid] = parsed.Value;
                }
            }

            return result;
        }

        // Converts a JSON number to a decimal, safely handling nulls and invalid data
        private static decimal? GetDecimalValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Number when element.TryGetDecimal(out var value) => value,
                _ => null
            };
        }
    }
}
