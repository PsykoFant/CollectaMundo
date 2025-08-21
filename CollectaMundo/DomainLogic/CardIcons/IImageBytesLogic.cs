namespace CollectaMundo.DomainLogic.CardIcons
{
    public interface IImageBytesLogic<TKey>
    {
        byte[]? GetBytes(TKey key);
    }
}
