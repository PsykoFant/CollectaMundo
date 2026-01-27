namespace CollectaMundo.DomainLogic.CardLists.CardLookups
{
    public interface ILookupProvider<TKey, TValue>
    {
        TValue? Get(TKey key);
        bool Contains(TKey key);
    }
}
