namespace CollectaMundo.DomainLogic.CardLists
{
    public interface ILookupProvider<TKey, TValue>
    {
        TValue? Get(TKey key);
        bool Contains(TKey key);
    }
}
