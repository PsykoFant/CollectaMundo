namespace CollectaMundo.DomainLogic.CardLookups
{
    public interface IImageBytesLogic<TKey>
    {
        byte[]? GetBytes(TKey key);
    }
}
