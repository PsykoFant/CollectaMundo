using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.KeyedDataProvider;

namespace CollectaMundo.ApplicationServices.KeyedDataProvider.Providers
{
    public sealed class SetDtoLookupProvider(IReadOnlyDictionary<string, SetDto> baseMap) : IKeyedDataProvider<string, SetDto>
    {
        private readonly IReadOnlyDictionary<string, SetDto> _map = baseMap;
        private readonly Dictionary<string, SetDto> _tokenMap = baseMap
                .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value.TokenCode))
                .ToDictionary(
                    kvp => kvp.Value.TokenCode,
                    kvp => kvp.Value,
                    StringComparer.OrdinalIgnoreCase
                );

        public SetDto? Get(string key)
        {
            if (_map.TryGetValue(key, out var result))
            {
                return result;
            }

            if (_tokenMap.TryGetValue(key, out var tokenResult))
            {
                return tokenResult;
            }

            return null;
        }

        public bool Contains(string key)
        {
            return _map.ContainsKey(key) || _tokenMap.ContainsKey(key);
        }

        public IEnumerable<SetDto> Values => _map.Values;
    }

}
