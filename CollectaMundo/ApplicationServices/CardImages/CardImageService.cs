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

            Debug.WriteLine("Starting GetImageForCardAsync...");

            // First, try to get Scryfall ID by UUID if available
            if (!string.IsNullOrWhiteSpace(card.Uuid))
            {
                scryfallID = await WithReadOnlyUowAsync(conn => _repo.GetScryfallIdByUuidAsync(card.Uuid, conn));
            }
            else if (!string.IsNullOrWhiteSpace(card.Name))
            {
                // If UUID is not available, try to get the oldest card's Scryfall ID by name
                scryfallID = await WithReadOnlyUowAsync(conn => _repo.GetScryfallIdByNameAsync(card.Name, conn));
            }
            else
            {
                Debug.WriteLine("Both card UUID and name are missing. Cannot fetch image.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(scryfallID))
            {
                Debug.WriteLine("No Scryfall ID found, returning null.");
                return null;
            }

            Debug.WriteLine($"ScryfallId found: {scryfallID}");

            CardImageDto cardImageDto = await _logic.BuildImageUrlsAsync(scryfallID, card);

            // If it is a card with multiple parts (side == "a") and back image URL is null, check if otherFace image exists
            // So far, this will only apply to cards with Meld keyword and it will show the melded card as the back image
            if (cardImageDto.BackImageUrl is null && card.Side == "a")
            {
                var otherFaceScryfallID = await WithReadOnlyUowAsync(conn => _repo.GetOtherFaceScryfallIdByUuidAsync(card.Uuid, conn));

                if (otherFaceScryfallID is not null)
                {
                    if (otherFaceScryfallID != scryfallID)
                    {
                        Debug.WriteLine("Other face Scryfall ID is not the same as the front face.");
                        cardImageDto.BackImageUrl = await _logic.BuildOtherSideImageUrlAsync(otherFaceScryfallID);

                    }
                }
            }

            return cardImageDto;
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
