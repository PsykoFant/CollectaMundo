namespace CollectaMundo.DomainLogic.KeyedDataProvider
{
    public interface IKeyedDataProvider<TKey, TValue>
    {
        TValue? Get(TKey key);
        bool Contains(TKey key);
    }
}
