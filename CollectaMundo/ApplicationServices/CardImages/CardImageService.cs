using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardImages;
using CollectaMundo.DomainLogic.CardImages.Models;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.Infrastructure.CardImages;
using CollectaMundo.Infrastructure.Common;
using CollectaMundo.Infrastructure.RemoteLookups;
using System.Data.SQLite;
using System.Diagnostics;

namespace CollectaMundo.ApplicationServices.CardImages
{
    public sealed class CardImageService(IDbConnectionFactory dbFactory, IRemoteLookups remoteLookups, ICardImageRepo repo, ICardImageLogic logic) : ICardImageService
    {
        private readonly IDbConnectionFactory _dbFactory = dbFactory;
        private readonly IRemoteLookups _remoteLookups = remoteLookups;
        private readonly ICardImageRepo _repo = repo;
        private readonly ICardImageLogic _logic = logic;

        public async Task<CardImageDto?> GetImageForCardAsync(CardSet card)
        {
            string? scryfallID = null;
            string? promoteTypes = null;

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
                var idTuple = await WithReadOnlyUowAsync(conn => _repo.GetScryfallIdByNameAsync(card.Name, conn));
                scryfallID = idTuple.Value.ScryfallId;
                card.Uuid = idTuple.Value.Uuid; // Assign the found UUID from the oldest version of the card back to the card object

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

            // Build image URLs (logic will only return back is side = "a")
            var (FrontUrl, BackUrl) = _logic.BuildImageUrls(scryfallID, card);
            string frontUrl = FrontUrl;
            string? backUrl = BackUrl;

            // If it is a card with multiple parts (side == "a")
            if (card.Side == "a" && backUrl is not null)
            {
                // If backUrl is already valid, no need to check further (dfc cards)
                if (!await _remoteLookups.IsValidUrlAsync(backUrl))
                {
                    // If backup URL is not valid, it could be Meld or variant of split cards                    
                    // Get the other face Scryfall ID
                    var otherFaceScryfallID = await WithReadOnlyUowAsync(conn => _repo.GetOtherFaceScryfallIdByUuidAsync(card.Uuid, conn));
                    if (!string.IsNullOrWhiteSpace(otherFaceScryfallID))
                    {
                        // Build the other side image URL. This logic will return null if the URL is the same as frontUrl (e.g. variant of split cards)
                        backUrl = _logic.BuildOtherSideImageUrl(otherFaceScryfallID, frontUrl);
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
