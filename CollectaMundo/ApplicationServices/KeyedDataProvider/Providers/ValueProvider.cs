using CollectaMundo.DomainLogic.KeyedDataProvider;

namespace CollectaMundo.ApplicationServices.KeyedDataProvider.Providers
{
    // Wraps a dictionary for plain data (strings, DTOs, prices, etc.)
    public sealed class ValueProvider<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> map) : IKeyedDataProvider<TKey, TValue> where TKey : notnull
    {
        private readonly IReadOnlyDictionary<TKey, TValue> _map = map;

        public TValue? Get(TKey key) => _map.TryGetValue(key, out var v) ? v : default;
        public bool Contains(TKey key) => _map.ContainsKey(key);
        public IEnumerable<TValue> Values => _map.Values;
    }
}
