using CollectaMundo.DomainLogic.CardImages.Models;
using CollectaMundo.DomainLogic.CardLists.Models;

namespace CollectaMundo.DomainLogic.CardImages
{
    public interface ICardImageLogic
    {
        Task<CardImageDto> BuildImageUrlsAsync(string scryfallId, CardSet card);
        Task<string?> BuildOtherSideImageUrlAsync(string scryfallId);
    }
}
