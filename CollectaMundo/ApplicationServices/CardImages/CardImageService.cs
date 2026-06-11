using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardImages;
using CollectaMundo.DomainLogic.CardImages.Models;
using CollectaMundo.DomainLogic.CardImages.Models.CollectaMundo.DomainLogic.CardImages.Models;
using CollectaMundo.Infrastructure.CardImages;
using CollectaMundo.Infrastructure.CardImages.Models;
using CollectaMundo.Infrastructure.RemoteLookups;
using System.Diagnostics;

namespace CollectaMundo.ApplicationServices.CardImages
{
    public sealed class CardImageService(IUnitOfWorkRunner uowRunner, IRemoteLookups remoteLookups, ICardImageLogic logic, ICardImageRepo repo, ICardImageDownloader downloader) : ICardImageService
    {
        private readonly IUnitOfWorkRunner _uowRunner = uowRunner;
        private readonly IRemoteLookups _remoteLookups = remoteLookups;
        private readonly ICardImageRepo _repo = repo;
        private readonly ICardImageLogic _logic = logic;
        private readonly ICardImageDownloader _downloader = downloader;

        public async Task<CardImageDto?> GetImageForCardAsync(CardImageRequest request)
        {
            string? scryfallID = null;
            CardImageMetadata? metadata = null;

            var uuid = request.Uuid;
            var name = request.Name;
            var side = request.Side;

            // Get IDs + metadata
            if (!string.IsNullOrWhiteSpace(uuid))
            {
                await _uowRunner.ExecuteReadOnlyAsync(async conn =>
                {
                    scryfallID = await _repo.GetScryfallIdByUuidAsync(uuid, conn);
                    metadata = await _repo.GetImageMetadataByUuidAsync(uuid, conn);
                    return true;
                });
            }
            else if (!string.IsNullOrWhiteSpace(name))
            {
                var idTuple = await _uowRunner.ExecuteReadOnlyAsync(conn => _repo.GetScryfallIdByNameAsync(name, conn));
                scryfallID = idTuple.Value.ScryfallId;
                uuid = idTuple.Value.Uuid;

                // Now that we have UUID, fetch metadata (same UoW style, read-only)
                if (!string.IsNullOrWhiteSpace(uuid))
                {
                    metadata = await _uowRunner.ExecuteReadOnlyAsync(conn => _repo.GetImageMetadataByUuidAsync(uuid, conn));
                }
            }

            else
            {
                Debug.WriteLine("Both request UUID and name are missing. Cannot fetch image.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(scryfallID))
            {
                Debug.WriteLine("No Scryfall ID found, returning null.");
                return null;
            }

            // CreateCollectionChangeSetFromEdits image URLs
            var (FrontUrl, BackUrl) = _logic.BuildImageUrls(scryfallID, side);
            string frontUrl = FrontUrl;
            string? backUrl = BackUrl;

            // Handle double-faced cards
            if (side == "a" && backUrl is not null)
            {
                if (!await _remoteLookups.IsValidUrlAsync(backUrl))
                {
                    var otherFaceScryfallID = await _uowRunner.ExecuteReadOnlyAsync(conn => _repo.GetOtherFaceScryfallIdByUuidAsync(uuid, conn));
                    if (!string.IsNullOrWhiteSpace(otherFaceScryfallID))
                    {
                        backUrl = _logic.BuildOtherSideImageUrl(otherFaceScryfallID, frontUrl);
                    }
                }
            }

            // Download as byte arrays
            var frontBytes = string.IsNullOrWhiteSpace(frontUrl)
                ? null
                : await _downloader.DownloadAsync(frontUrl, uuid, "front");

            var backBytes = string.IsNullOrWhiteSpace(backUrl)
                ? null
                : await _downloader.DownloadAsync(backUrl, uuid, "back");

            return new CardImageDto
            {
                FrontImageBytes = frontBytes,
                BackImageBytes = backBytes,
                PromoType = metadata?.PromoType,
                SetName = metadata?.SetName
            };
        }
    }

}
