using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.Data;
using CollectaMundo.Data.CardImages;
using CollectaMundo.DomainLogic.CardImages;
using CollectaMundo.DomainLogic.CardImages.Models;
using CollectaMundo.DomainLogic.CardLists.Models;
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

            if (string.IsNullOrWhiteSpace(scryfallID))
            {
                Debug.WriteLine("No Scryfall ID found, returning null.");
                return null;
            }

            Debug.WriteLine($"ScryfallId found: {scryfallID}");

            CardImageDto cardImageDto = await _logic.BuildImageUrlsAsync(scryfallID, card);

            return cardImageDto;
        }
    }

}
