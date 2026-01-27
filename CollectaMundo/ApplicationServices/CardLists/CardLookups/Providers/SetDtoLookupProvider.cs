using CollectaMundo.DomainLogic.CardLists.CardLookups;
using CollectaMundo.DomainLogic.CardLists.Models;

namespace CollectaMundo.ApplicationServices.CardLists.CardLookups.Providers
{
    public sealed class SetDtoLookupProvider(IReadOnlyDictionary<string, SetDto> baseMap) : ILookupProvider<string, SetDto>
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
