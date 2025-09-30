using CollectaMundo.DomainLogic.CardLists.Models;

namespace CollectaMundo.DomainLogic.CardImages
{
    public interface ICardImageLogic
    {
        string[] BuildImageUrlsAsync(string scryfallId, CardSet card);
        string? BuildOtherSideImageUrlAsync(string scryfallId, string frontUrl);
    }
}
