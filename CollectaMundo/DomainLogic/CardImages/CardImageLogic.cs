using CollectaMundo.DomainLogic.CardImages.Models;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.Infrastructure.RemoteLookups;
using System.Diagnostics;

namespace CollectaMundo.DomainLogic.CardImages
{
    public class CardImageLogic(IRemoteLookups remoteLookups) : ICardImageLogic
    {
        private readonly IRemoteLookups _remoteLookups = remoteLookups;

        public async Task<CardImageDto> BuildImageUrlsAsync(string scryfallId, CardSet card)
        {
            var frontUrl = BuildImageUrl(scryfallId, front: true);
            string? backUrl = null;

            if (card.Side == "a")
            {
                var potentialBackUrl = BuildImageUrl(scryfallId, front: false);
                backUrl = await ValidateUrlOrNullAsync(potentialBackUrl, scryfallId, "Back");
            }

            return new CardImageDto
            {
                FrontImageUrl = frontUrl,
                BackImageUrl = backUrl
            };
        }

        public async Task<string?> BuildOtherSideImageUrlAsync(string scryfallId)
        {
            var url = BuildImageUrl(scryfallId, front: true); // 'other face' always assumed to be a front
            return await ValidateUrlOrNullAsync(url, scryfallId, "OtherFace");
        }
        private static string BuildImageUrl(string scryfallId, bool front)
        {
            var dir1 = scryfallId[0];
            var dir2 = scryfallId[1];
            var face = front ? "front" : "back";
            return $"https://cards.scryfall.io/normal/{face}/{dir1}/{dir2}/{scryfallId}.jpg";
        }

        private async Task<string?> ValidateUrlOrNullAsync(string url, string scryfallId, string label)
        {
            if (!await _remoteLookups.IsValidUrlAsync(url))
            {
                Debug.WriteLine($"{label} image URL check for {scryfallId}: Not found");
                return null;
            }

            Debug.WriteLine($"{label} image URL check for {scryfallId}: Exists");
            return url;
        }
    }

}
