namespace CollectaMundo.DomainLogic.CardImages
{
    public interface ICardImageLogic
    {
        string BuildImageUrl(string scryfallId, bool isFront);
    }
}
