using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardImages;
using CollectaMundo.DomainLogic.CardImages.Models;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.Infrastructure.CardImages;
using CollectaMundo.Infrastructure.Common;
using System.Data.SQLite;
using System.Diagnostics;

namespace CollectaMundo.ApplicationServices.CardImages
{
    public sealed class CardImageService(IDbConnectionFactory dbFactory, ICardImageRepo repo, ICardImageLogic logic) : ICardImageService
    {
        private readonly IDbConnectionFactory _dbFactory = dbFactory;
        private readonly ICardImageRepo _repo = repo;
        private readonly ICardImageLogic _logic = logic;

        public async Task<CardImageDto?> GetImageForCardAsync(CardSet card)
        {
            string? scryfallID = null;
            string? promoteTypes = null;

            Debug.WriteLine("Starting GetImageForCardAsync...");

            // First, try to get Scryfall ID by UUID if available
            if (!string.IsNullOrWhiteSpace(card.Uuid))
            {
                await WithReadOnlyUowAsync(async conn =>
                {
                    scryfallID = await _repo.GetScryfallIdByUuidAsync(card.Uuid, conn);
                    promoteTypes = await _repo.GetImagePromoTypeByUuidAsync(card.Uuid, conn);
                    return true;
                });
            }

            // If UUID is not available, try to get the oldest card's Scryfall ID by name
            else if (!string.IsNullOrWhiteSpace(card.Name))
            {
                scryfallID = await WithReadOnlyUowAsync(conn => _repo.GetScryfallIdByNameAsync(card.Name, conn));
            }

            // If neither UUID nor name is available, log and return null
            else
            {
                Debug.WriteLine("Both card UUID and name are missing. Cannot fetch image.");
                return null;
            }

            // If no Scryfall ID found, return null
            if (string.IsNullOrWhiteSpace(scryfallID))
            {
                Debug.WriteLine("No Scryfall ID found, returning null.");
                return null;
            }

            Debug.WriteLine($"ScryfallId found: {scryfallID}");

            // Build image URLs
            var imageUrls = await _logic.BuildImageUrlsAsync(scryfallID, card);
            string frontUrl = imageUrls[0];
            string? backUrl = imageUrls.Length > 1 ? imageUrls[1] : null;

            // If it is a card with multiple parts (side == "a") and back image URL is null, check if otherFace image exists
            // So far, this will only apply to cards with Meld keyword and it will show the melded card as the back image
            // Only do this if we have a UUID
            if (backUrl is null && card.Side == "a" && card.Uuid is not null)
            {
                var otherFaceScryfallID = await WithReadOnlyUowAsync(conn => _repo.GetOtherFaceScryfallIdByUuidAsync(card.Uuid, conn));

                if (otherFaceScryfallID is not null)
                {
                    if (otherFaceScryfallID != scryfallID)
                    {
                        Debug.WriteLine("Other face Scryfall ID is not the same as the front face.");
                        backUrl = await _logic.BuildOtherSideImageUrlAsync(otherFaceScryfallID);

                    }
                }
            }

            return new CardImageDto
            {
                FrontImageUrl = frontUrl,
                BackImageUrl = backUrl,
                PromoType = promoteTypes
            };
        }

        private async Task<T> WithReadOnlyUowAsync<T>(Func<SQLiteConnection, Task<T>> work)
        {
            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginReadOnlyAsync();
            try
            {
                var result = await work(uow.CurrentConnection);
                await uow.CommitAsync();
                return result;
            }
            catch
            {
                await uow.RollbackAsync();
                throw;
            }
            finally
            {
                await uow.DisposeAsync();
            }
        }

    }

}
