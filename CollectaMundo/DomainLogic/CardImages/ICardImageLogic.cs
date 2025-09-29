using CollectaMundo.DomainLogic.CardLists.Models;

namespace CollectaMundo.DomainLogic.CardImages
{
    public interface ICardImageLogic
    {
        Task<string[]> BuildImageUrlsAsync(string scryfallId, CardSet card);
        Task<string?> BuildOtherSideImageUrlAsync(string scryfallId);
    }
}
