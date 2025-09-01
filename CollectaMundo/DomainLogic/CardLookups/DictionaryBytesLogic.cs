namespace CollectaMundo.DomainLogic.CardLookups
{
    public sealed class DictionaryBytesLogic<TKey> : IImageBytesLogic<TKey> where TKey : notnull
    {
        private readonly IReadOnlyDictionary<TKey, byte[]> _map;

        public DictionaryBytesLogic(IReadOnlyDictionary<TKey, byte[]> map)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
        }

        public byte[]? GetBytes(TKey key) => _map.TryGetValue(key, out var b) ? b : null;
    }
}
