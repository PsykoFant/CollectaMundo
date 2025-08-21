namespace CollectaMundo.DomainLogic.CardIcons
{
    public sealed class ManaCostBytesLogic(IReadOnlyDictionary<string, byte[]> map) : IImageBytesLogic<string>
    {
        private readonly IReadOnlyDictionary<string, byte[]> _map = map;

        public byte[]? GetBytes(string key) => _map.TryGetValue(key, out var b) ? b : null;
    }
}
