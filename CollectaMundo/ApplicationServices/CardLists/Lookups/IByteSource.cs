namespace CollectaMundo.ApplicationServices.CardLists.Lookups
{
    public interface IByteSource<TKey>
    {
        byte[]? GetBytes(TKey key);
    }
}
