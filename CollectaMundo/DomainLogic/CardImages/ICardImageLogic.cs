namespace CollectaMundo.DomainLogic.CardImages
{
    public interface ICardImageLogic
    {
        (string FrontUrl, string? BackUrl) BuildImageUrls(string scryfallId, string? side);
        string? BuildOtherSideImageUrl(string scryfallId, string frontUrl);
    }
}
