using CollectaMundo.ApplicationServices.CardLists.CardLookups.Providers;
using CollectaMundo.ApplicationServices.CardLists.CardLookups.Sources;
using CollectaMundo.DomainLogic.CardLists.Models;
using System.Windows.Media;

namespace CollectaMundo.DomainLogic.CardLists.CardLookups
{
    public sealed class CardLookupBuilder
    {
        public static CardLookupPackage Build(
            IReadOnlyDictionary<string, byte[]> manaIcons,
            IReadOnlyDictionary<string, byte[]> setIcons,
            IReadOnlyDictionary<string, SetDto> sets,
            IReadOnlyDictionary<string, PriceDto> prices)
        {
            // Build alias map: tokenSetCode → setCode
            var tokenToCodeMap = sets.Values.Where(s => !string.IsNullOrWhiteSpace(s.TokenCode)).ToDictionary(
                    s => s.TokenCode,
                    s => s.Code,
                    StringComparer.OrdinalIgnoreCase
                );

            var setIconByteSource = new ByteSourceWithAlias<string>(
                new DictionaryByteSource<string>(setIcons),
                tokenToCodeMap
            );

            return new CardLookupPackage
            {
                ManaCostImages = new ImageProvider<string>(new DictionaryByteSource<string>(manaIcons)),
                SetIconImages = new ImageProvider<string>(setIconByteSource), // <- uses alias-aware logic
                SetMetaProvider = new SetDtoLookupProvider(sets),
                PriceMetaProvider = new ValueProvider<string, PriceDto>(prices)
            };
        }
    }
    public sealed class CardLookupPackage
    {
        public required ILookupProvider<string, ImageSource> ManaCostImages { get; init; }
        public required ILookupProvider<string, ImageSource> SetIconImages { get; init; }
        public required ILookupProvider<string, SetDto> SetMetaProvider { get; init; }
        public required ILookupProvider<string, PriceDto> PriceMetaProvider { get; init; }
    }
}
