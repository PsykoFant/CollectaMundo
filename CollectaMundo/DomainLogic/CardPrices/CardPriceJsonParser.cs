using System.Text.Json;

namespace CollectaMundo.DomainLogic.CardPrices
{
    public static class CardPriceJsonParser
    {
        public static Dictionary<string, decimal> ParsePriceList(JsonElement pricesRoot, string format, string retailer, string finishType)
        {
            var result = new Dictionary<string, decimal>();

            if (!pricesRoot.TryGetProperty("data", out var dataNode))
            {
                return result;
            }

            foreach (var card in dataNode.EnumerateObject())
            {
                var uuid = card.Name;
                var cardData = card.Value;

                if (!cardData.TryGetProperty(format, out var formatNode))
                {
                    continue;
                }

                if (!formatNode.TryGetProperty(retailer, out var retailerNode))
                {
                    continue;
                }

                if (!retailerNode.TryGetProperty("retail", out var retailNode))
                {
                    continue;
                }

                if (!retailNode.TryGetProperty(finishType, out var finishNode))
                {
                    continue;
                }

                foreach (var dateEntry in finishNode.EnumerateObject())
                {
                    if (dateEntry.Value.ValueKind == JsonValueKind.Number &&
                        dateEntry.Value.TryGetDecimal(out var price))
                    {
                        result[uuid] = price;
                        break; // Only need the first available price
                    }
                }
            }

            return result;
        }
    }
}
