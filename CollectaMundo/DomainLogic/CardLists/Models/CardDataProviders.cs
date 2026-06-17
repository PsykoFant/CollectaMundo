using CollectaMundo.DomainLogic.KeyedDataProvider;
using System.Windows.Media;

namespace CollectaMundo.DomainLogic.CardLists.Models
{
    public static class CardDataProviders
    {
        public static IKeyedDataProvider<string, ImageSource>? ManaCostImages { get; set; }
        public static IKeyedDataProvider<string, ImageSource>? SetIconImages { get; set; }
        public static IKeyedDataProvider<string, SetDto>? SetMetaProvider { get; set; }
        public static IKeyedDataProvider<string, PriceDto>? PriceMetaProvider { get; set; }
    }
}
