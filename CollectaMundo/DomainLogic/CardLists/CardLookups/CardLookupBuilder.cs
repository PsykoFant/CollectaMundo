using CollectaMundo.ApplicationServices.CardLists.CardLookups.Providers;
using CollectaMundo.ApplicationServices.CardLists.CardLookups.Sources;
using CollectaMundo.DomainLogic.CardLists.Models;
using System.Windows.Media;

namespace CollectaMundo.DomainLogic.CardLists.CardLookups
{
    public sealed class CardLookupBuilder
    {
        public CardLookupPackage Build(IReadOnlyDictionary<string, byte[]> manaIcons, IReadOnlyDictionary<string, byte[]> setIcons, IReadOnlyDictionary<string, SetDto> sets, IReadOnlyDictionary<string, PriceDto> prices)

        {
            return new CardLookupPackage
            {
                ManaCostImages = new ImageProvider<string>(new DictionaryByteSource<string>(manaIcons)),
                SetIconImages = new ImageProvider<string>(new DictionaryByteSource<string>(setIcons)),
                SetMetaProvider = new ValueProvider<string, SetDto>(sets),
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
