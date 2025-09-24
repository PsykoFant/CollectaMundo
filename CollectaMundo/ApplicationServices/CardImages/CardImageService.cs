using CollectaMundo.Data.CardImages;
using CollectaMundo.DomainLogic.CardImages;
using CollectaMundo.DomainLogic.CardImages.Models;
using System.Diagnostics;

namespace CollectaMundo.ApplicationServices.CardImages
{
    public sealed class CardImageService(ICardImageRepo repo, CardImageLogic logic) : ICardImageService
    {
        private readonly ICardImageRepo _repo = repo;
        private readonly ICardImageLogic _logic = logic;

        public async Task<CardImageDto?> GetImageForCardAsync(string? uuid, string? name)
        {
            Debug.WriteLine("Starting GetImageForCardAsync...");

            if (!string.IsNullOrWhiteSpace(uuid))
            {
                Debug.WriteLine($"Using UUID: {uuid}");
            }
            else if (!string.IsNullOrWhiteSpace(name))
            {
                Debug.WriteLine($"Resolving UUID from name: {name}");
                // Placeholder: resolve name to uuid (future)
            }
            else
            {
                Debug.WriteLine("No UUID or name provided");
                return null;
            }

            Debug.WriteLine("Fetching scryfall ID, building image URLs...");
            Debug.WriteLine("Checking if double-faced...");
            Debug.WriteLine("Returning result object");

            return new CardImageDto
            {
                Uuid = uuid ?? "<resolved>",
                FrontImageUrl = "<front-url>",
                BackImageUrl = "<back-url>"
            };
        }
    }
}
