using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.Data;
using CollectaMundo.Data.CardImages;
using CollectaMundo.DomainLogic.CardImages;
using CollectaMundo.DomainLogic.CardImages.Models;
using CollectaMundo.DomainLogic.CardLists.Models;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CollectaMundo.ApplicationServices.CardImages
{
    public sealed class CardImageService(IDbConnectionFactory dbFactory, ICardImageRepo repo, CardImageLogic logic) : ICardImageService
    {
        private readonly IDbConnectionFactory _dbFactory = dbFactory;
        private readonly ICardImageRepo _repo = repo;
        private readonly ICardImageLogic _logic = logic;

        public async Task<CardImageDto?> GetImageForCardAsync(CardSet card)
        {
            Debug.WriteLine("Starting GetImageForCardAsync...");
            string? scryfallID = string.Empty;

            // Get scryfall ID from UUID
            if (!string.IsNullOrWhiteSpace(card.Uuid))
            {
                Debug.WriteLine($"Using UUID: {card.Uuid}");

                await using var uow = new UnitOfWork(_dbFactory);
                await uow.BeginReadOnlyAsync();
                try
                {
                    // Hand off to your pure domain‐logic batch (no further UoW calls inside)
                    scryfallID = await _repo.GetScryfallIdByUuidAsync(card.Uuid, uow.CurrentConnection);

                    // Commit on success
                    await uow.CommitAsync();
                }
                catch
                {
                    // Roll back on any error
                    await uow.RollbackAsync();
                    throw;
                }
                finally
                {
                    // Clean up / close connection
                    await uow.DisposeAsync();
                }

            }
            // Get scryfall ID from name
            else if (!string.IsNullOrWhiteSpace(card.Name))
            {
                Debug.WriteLine($"Resolving UUID from name: {card.Name}");
                // Placeholder: resolve name to uuid (future)
            }

            Debug.WriteLine($"ScryfallId found: {scryfallID}");

            string? frontImageUrl = _logic.BuildImageUrl(scryfallID, isFront: true);
            Debug.WriteLine("Checking if double-faced...");



            Debug.WriteLine("Returning result object");
            Debug.WriteLine($"Returning {frontImageUrl}");

            return new CardImageDto
            {
                FrontImageUrl = frontImageUrl,
                FrontImageSource = ConvertToImageSource(frontImageUrl),
                BackImageUrl = "<back-url>"
            };
        }

        private static ImageSource? ConvertToImageSource(string url)
        {
            try
            {
                var uri = new Uri(url, UriKind.Absolute);
                return new BitmapImage(uri);
            }
            catch
            {
                return null;
            }
        }

    }

}
