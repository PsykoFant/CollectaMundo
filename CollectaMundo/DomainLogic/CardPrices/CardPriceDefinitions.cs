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
    }
}

