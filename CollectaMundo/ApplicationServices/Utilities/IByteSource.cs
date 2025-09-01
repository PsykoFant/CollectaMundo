namespace CollectaMundo.ApplicationServices.Utilities
{
    public interface IByteSource<TKey>
    {
        byte[]? GetBytes(TKey key);
    }
}
