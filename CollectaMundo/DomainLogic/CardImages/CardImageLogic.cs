using CollectaMundo.DomainLogic.CardLists.Models;

namespace CollectaMundo.DomainLogic.CardImages
{
    public class CardImageLogic() : ICardImageLogic
    {
        public (string FrontUrl, string? BackUrl) BuildImageUrls(string scryfallId, CardSet card)
        {
            var frontUrl = BuildImageUrl(scryfallId, front: true);
            string? backUrl = null;

            if (card.Side == "a")
            {
                backUrl = BuildImageUrl(scryfallId, front: false);
            }

            return (frontUrl, backUrl);
        }

        public string? BuildOtherSideImageUrl(string scryfallId, string frontUrl)
        {
            var url = BuildImageUrl(scryfallId, front: true); // 'other face' always assumed to be a front            
            return url != frontUrl ? url : null; // If the URLs are the same, it's probably split, adventure, Aftermath etc. cards where we don't want to show card back
        }
        private static string BuildImageUrl(string scryfallId, bool front)
        {
            var dir1 = scryfallId[0];
            var dir2 = scryfallId[1];
            var face = front ? "front" : "back";
            return $"https://cards.scryfall.io/normal/{face}/{dir1}/{dir2}/{scryfallId}.jpg";
        }
    }
}
