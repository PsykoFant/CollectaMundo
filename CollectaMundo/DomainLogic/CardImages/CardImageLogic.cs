using CollectaMundo.Data.RemoteLookups;
using CollectaMundo.DomainLogic.CardImages.Models;
using CollectaMundo.DomainLogic.CardLists.Models;
using System.Diagnostics;

namespace CollectaMundo.DomainLogic.CardImages
{
    public class CardImageLogic(IRemoteLookups remoteLookups) : ICardImageLogic
    {
        private readonly IRemoteLookups _remoteLookups = remoteLookups;

        public async Task<CardImageDto> BuildImageUrlsAsync(string scryfallId, CardSet card)
        {
            var dir1 = scryfallId[0];
            var dir2 = scryfallId[1];

            string frontImageUrl = $"https://cards.scryfall.io/normal/front/{dir1}/{dir2}/{scryfallId}.jpg";
            string? backImageUrl = card.Side == "a"
                ? $"https://cards.scryfall.io/normal/back/{dir1}/{dir2}/{scryfallId}.jpg"
                : null;

            Debug.WriteLine($"Constructed front image URL: {frontImageUrl}");

            if (backImageUrl is not null)
            {
                if (!await _remoteLookups.IsValidUrlAsync(backImageUrl))
                {
                    backImageUrl = null;
                }

                Debug.WriteLine($"Back image URL check for {scryfallId}: {(backImageUrl is null ? "Not found" : "Exists")}");
            }

            return new CardImageDto
            {
                FrontImageUrl = frontImageUrl,
                BackImageUrl = backImageUrl
            };
        }

    }
}
