using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.Data;
using CollectaMundo.Data.CardImages;
using CollectaMundo.DomainLogic.CardImages;
using CollectaMundo.DomainLogic.CardImages.Models;
using System.Diagnostics;

namespace CollectaMundo.ApplicationServices.CardImages
{
    public sealed class CardImageService(IDbConnectionFactory dbFactory, ICardImageRepo repo, CardImageLogic logic) : ICardImageService
    {
        private readonly IDbConnectionFactory _dbFactory = dbFactory;
        private readonly ICardImageRepo _repo = repo;
        private readonly ICardImageLogic _logic = logic;

        public async Task<CardImageDto?> GetImageForCardAsync(string? uuid, string? name)
        {
            Debug.WriteLine("Starting GetImageForCardAsync...");
            string? scryfallID = string.Empty;
            string? frontImageUrl = string.Empty;

            // Get scryfall ID from UUID
            if (!string.IsNullOrWhiteSpace(uuid))
            {
                Debug.WriteLine($"Using UUID: {uuid}");

                await using var uow = new UnitOfWork(_dbFactory);
                await uow.BeginReadOnlyAsync();
                try
                {
                    // Hand off to your pure domain‐logic batch (no further UoW calls inside)
                    scryfallID = await _repo.GetScryfallIdByUuidAsync(uuid, uow.CurrentConnection);

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
            else if (!string.IsNullOrWhiteSpace(name))
            {
                Debug.WriteLine($"Resolving UUID from name: {name}");
                // Placeholder: resolve name to uuid (future)
            }

            Debug.WriteLine($"ScryfallId found: {scryfallID}");
            Debug.WriteLine("Checking if double-faced...");

            frontImageUrl = _logic.BuildImageUrl(scryfallID, isFront: true);

            Debug.WriteLine("Returning result object");
            Debug.WriteLine($"Returning {frontImageUrl}");

            return new CardImageDto
            {
                FrontImageUrl = frontImageUrl,
                BackImageUrl = "<back-url>"
            };
        }
    }
}
