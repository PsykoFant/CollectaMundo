namespace CollectaMundo.ApplicationServices.Utilities
{
    public interface IValueProvider<TKey, TValue>
    {
        TValue? Get(TKey key);
    }
}
