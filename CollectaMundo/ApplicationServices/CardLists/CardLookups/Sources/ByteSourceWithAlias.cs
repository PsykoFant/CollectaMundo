namespace CollectaMundo.ApplicationServices.CardLists.CardLookups.Sources
{
    public sealed class ByteSourceWithAlias<TKey>(IByteSource<TKey> inner, IReadOnlyDictionary<TKey, TKey> aliasMap) : IByteSource<TKey> where TKey : notnull
    {
        private readonly IByteSource<TKey> _inner = inner;
        private readonly IReadOnlyDictionary<TKey, TKey> _aliasMap = aliasMap;

        public byte[]? GetBytes(TKey key)
        {
            // Try original key
            var result = _inner.GetBytes(key);
            if (result != null)
            {
                return result;
            }

            // Try alias if known
            if (_aliasMap.TryGetValue(key, out var realKey))
            {
                return _inner.GetBytes(realKey);
            }

            return null;
        }
    }

}
