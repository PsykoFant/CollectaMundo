using CollectaMundo.DomainLogic.CardLists.Models;

namespace CollectaMundo.DomainLogic.CardImages
{
    public interface ICardImageLogic
    {
        (string FrontUrl, string? BackUrl) BuildImageUrls(string scryfallId, CardSet card);
        string? BuildOtherSideImageUrl(string scryfallId, string frontUrl);
    }
}
