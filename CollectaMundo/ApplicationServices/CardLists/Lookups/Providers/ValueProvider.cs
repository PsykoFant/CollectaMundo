using CollectaMundo.DomainLogic.CardLists;

namespace CollectaMundo.ApplicationServices.CardLists.Lookups.Providers
{
    // Wraps a dictionary for plain data (strings, DTOs, prices, etc.)
    internal sealed class ValueProvider<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> map) : ILookupProvider<TKey, TValue> where TKey : notnull
    {
        private readonly IReadOnlyDictionary<TKey, TValue> _map = map;

        public TValue? Get(TKey key) => _map.TryGetValue(key, out var v) ? v : default;
        public bool Contains(TKey key) => _map.ContainsKey(key);
    }
}
