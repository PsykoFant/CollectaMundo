namespace CollectaMundo.DomainLogic.CardPrices
{
    public static class CardPriceDefinitions
    {
        public static readonly string[] Finishes = ["Normal", "Foil", "Etched"];

        public static readonly Dictionary<string, string[]> RetailersByFormat = new()
        {
            ["paper"] = ["cardkingdom", "cardmarket", "cardsphere", "tcgplayer"],
            ["mtgo"] = ["cardhoarder"]
        };
        public static IEnumerable<PriceSourceKey> GetAllKeys()
        {
            foreach (var (format, retailers) in CardPriceDefinitions.RetailersByFormat)
            {
                foreach (string retailer in retailers)
                {
                    foreach (string finish in CardPriceDefinitions.Finishes)
                    {
                        yield return new PriceSourceKey(format, retailer, finish.ToLowerInvariant());
                    }
                }
            }
        }

        public readonly record struct PriceSourceKey(string Format, string Retailer, string Finish);
    }
}

