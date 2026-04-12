using CollectaMundo.ApplicationServices.KeyedDataProvider.Providers;
using CollectaMundo.ApplicationServices.KeyedDataProvider.Sources;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CardLocations.Models;
using System.Windows.Media;

namespace CollectaMundo.DomainLogic.KeyedDataProvider
{
    public sealed class KeyedDataProviderBuilder
    {
        public static KeyedDataProviderPackage Build(IReadOnlyDictionary<string, byte[]> manaIcons, IReadOnlyDictionary<string, byte[]> setIcons, IReadOnlyDictionary<string, SetDto> sets, IReadOnlyDictionary<string, PriceDto> prices, IReadOnlyDictionary<int, CardLocation> locations)
        {
            var tokenToCodeMap = sets.Values
                .Where(s => !string.IsNullOrWhiteSpace(s.TokenCode))
                .ToDictionary(
                    s => s.TokenCode,
                    s => s.Code,
                    StringComparer.OrdinalIgnoreCase);

            var setIconByteSource = new ByteSourceWithAlias<string>(
                new DictionaryByteSource<string>(setIcons),
                tokenToCodeMap
            );

            return new KeyedDataProviderPackage
            {
                ManaCostImages = new ImageProvider<string>(new DictionaryByteSource<string>(manaIcons)),
                SetIconImages = new ImageProvider<string>(setIconByteSource),
                SetMetaProvider = new SetDtoLookupProvider(sets),
                PriceMetaProvider = new ValueProvider<string, PriceDto>(prices),
                CardLocationProvider = new ValueProvider<int, CardLocation>(locations)
            };
        }
    }
    public sealed class KeyedDataProviderPackage
    {
        public required IKeyedDataProvider<string, ImageSource> ManaCostImages { get; init; }
        public required IKeyedDataProvider<string, ImageSource> SetIconImages { get; init; }
        public required IKeyedDataProvider<string, SetDto> SetMetaProvider { get; init; }
        public required IKeyedDataProvider<string, PriceDto> PriceMetaProvider { get; init; }
        public required IKeyedDataProvider<int, CardLocation> CardLocationProvider { get; init; }
    }
}
