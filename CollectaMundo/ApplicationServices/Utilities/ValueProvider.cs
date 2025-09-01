namespace CollectaMundo.ApplicationServices.Utilities
{
    public sealed class ValueProvider<TKey, TValue> : IValueProvider<TKey, TValue>
        where TKey : notnull
    {
        private readonly IReadOnlyDictionary<TKey, TValue> _map;
        public ValueProvider(IReadOnlyDictionary<TKey, TValue> map) => _map = map;
        public TValue? Get(TKey key) => _map.TryGetValue(key, out var v) ? v : default;
    }
}
