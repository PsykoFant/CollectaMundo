namespace CollectaMundo.ApplicationServices.CardLists.CardLookups
{
    public interface IByteSource<TKey>
    {
        byte[]? GetBytes(TKey key);
    }
}
