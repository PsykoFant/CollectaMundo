namespace CollectaMundo.DomainLogic.CardPrices
{
    public static class CardPriceDefinitions
    {
        public static readonly string[] Finishes = ["Normal", "Foil", "Etched"];

        public static readonly Dictionary<string, Dictionary<string, string>> RetailersByFormat = new()
        {
            ["paper"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["cardkingdom"] = "Card Kingdom",
                ["cardmarket"] = "Cardmarket",
                ["cardsphere"] = "Cardsphere",
                ["tcgplayer"] = "TCG Player"
            },
            ["mtgo"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["cardhoarder"] = "Cardhoarder"
            }
        };
        public static IEnumerable<PriceSourceKey> GetAllKeys()
        {
            foreach (var (format, retailers) in CardPriceDefinitions.RetailersByFormat)
            {
                foreach (var retailerId in retailers.Keys) // use the canonical id, not display name
                {
                    foreach (var finish in CardPriceDefinitions.Finishes)
                    {
                        yield return new PriceSourceKey(format, retailerId, finish.ToLowerInvariant());
                    }
                }
            }
        }
        public readonly record struct PriceSourceKey(string Format, string Retailer, string Finish);
    }
}

