namespace CollectaMundo.DomainLogic.CardIcons
{
    public sealed class SetIconBytesLogic(IReadOnlyDictionary<string, byte[]> map) : IImageBytesLogic<string>
    {
        public byte[]? GetBytes(string key) => _map.TryGetValue(key, out var b) ? b : null;
        private readonly IReadOnlyDictionary<string, byte[]> _map = map;
    }
}
