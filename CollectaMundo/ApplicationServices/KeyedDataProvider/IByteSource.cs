namespace CollectaMundo.ApplicationServices.KeyedDataProvider
{
    public interface IByteSource<TKey>
    {
        byte[]? GetBytes(TKey key);
    }
}
