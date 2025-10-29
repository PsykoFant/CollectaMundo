using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardImages;
using CollectaMundo.DomainLogic.CardImages.Models.CollectaMundo.DomainLogic.CardImages.Models;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.Infrastructure.CardImages;
using CollectaMundo.Infrastructure.RemoteLookups;
using CollectaMundo.Infrastructure.Shared;
using System.Data.SQLite;
using System.Diagnostics;

namespace CollectaMundo.ApplicationServices.CardImages
{
    public sealed class CardImageService(IDbConnectionFactory dbFactory, IRemoteLookups remoteLookups, ICardImageLogic logic, ICardImageRepo repo, ICardImageDownloader downloader) : ICardImageService
    {
        private readonly IDbConnectionFactory _dbFactory = dbFactory;
        private readonly IRemoteLookups _remoteLookups = remoteLookups;
        private readonly ICardImageRepo _repo = repo;
        private readonly ICardImageLogic _logic = logic;
        private readonly ICardImageDownloader _downloader = downloader;

        public async Task<CardImageDto?> GetImageForCardAsync(CardSet card)
        {
            string? scryfallID = null;
            string? promoteTypes = null;

            // Get IDs
            if (!string.IsNullOrWhiteSpace(card.Uuid))
            {
                await WithReadOnlyUowAsync(async conn =>
                {
                    scryfallID = await _repo.GetScryfallIdByUuidAsync(card.Uuid, conn);
                    promoteTypes = await _repo.GetImagePromoTypeByUuidAsync(card.Uuid, conn);
                    return true;
                });
            }
            else if (!string.IsNullOrWhiteSpace(card.Name))
            {
                var idTuple = await WithReadOnlyUowAsync(conn => _repo.GetScryfallIdByNameAsync(card.Name, conn));
                scryfallID = idTuple.Value.ScryfallId;
                card.Uuid = idTuple.Value.Uuid;
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

            // Build image URLs
            var (FrontUrl, BackUrl) = _logic.BuildImageUrls(scryfallID, card);
            string frontUrl = FrontUrl;
            string? backUrl = BackUrl;

            // Handle double-faced cards
            if (card.Side == "a" && backUrl is not null)
            {
                if (!await _remoteLookups.IsValidUrlAsync(backUrl))
                {
                    var otherFaceScryfallID = await WithReadOnlyUowAsync(conn => _repo.GetOtherFaceScryfallIdByUuidAsync(card.Uuid, conn));
                    if (!string.IsNullOrWhiteSpace(otherFaceScryfallID))
                    {
                        backUrl = _logic.BuildOtherSideImageUrl(otherFaceScryfallID, frontUrl);
                    }
                }
            }

            // Download as byte arrays
            var frontBytes = string.IsNullOrWhiteSpace(frontUrl)
                ? null
                : await _downloader.DownloadAsync(frontUrl, card.Uuid, "front");

            var backBytes = string.IsNullOrWhiteSpace(backUrl)
                ? null
                : await _downloader.DownloadAsync(backUrl, card.Uuid, "back");


            return new CardImageDto
            {
                FrontImageBytes = frontBytes,
                BackImageBytes = backBytes,
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
