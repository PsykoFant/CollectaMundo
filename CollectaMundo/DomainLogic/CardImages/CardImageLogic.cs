namespace CollectaMundo.DomainLogic.CardImages
{
    public class CardImageLogic : ICardImageLogic
    {
        public string BuildImageUrl(string scryfallId, bool isFront)
        {
            var dir1 = scryfallId[0];
            var dir2 = scryfallId[1];
            var side = isFront ? "front" : "back";
            return $"https://cards.scryfall.io/normal/{side}/{dir1}/{dir2}/{scryfallId}.jpg";
        }

    }
}
