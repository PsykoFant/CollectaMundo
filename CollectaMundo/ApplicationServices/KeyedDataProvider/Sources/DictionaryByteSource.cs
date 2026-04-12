namespace CollectaMundo.ApplicationServices.KeyedDataProvider.Sources
{
    internal sealed class DictionaryByteSource<TKey>(IReadOnlyDictionary<TKey, byte[]> map) : IByteSource<TKey> where TKey : notnull
    {
        private readonly IReadOnlyDictionary<TKey, byte[]> _map = map ?? throw new ArgumentNullException(nameof(map));

        public byte[]? GetBytes(TKey key) => _map.TryGetValue(key, out var b) ? b : null;
    }
}
