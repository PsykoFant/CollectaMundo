using CollectaMundo.DomainLogic.CardLists.Models;
using System.Windows.Media;

namespace CollectaMundo.ViewModels.CardLists
{
    public sealed class ManaSymbolViewModel
    {
        public required string ManaCostRaw { get; init; }
        public ImageSource? ManaCostImage => CardDataProviders.ManaCostImages?.Get(ManaCostRaw);
    }
}
