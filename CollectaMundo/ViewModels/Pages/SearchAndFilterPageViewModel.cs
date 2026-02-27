using CollectaMundo.ViewModels.Pages.SharedElements;
using System.Collections.ObjectModel;

namespace CollectaMundo.ViewModels.Pages
{
    public sealed class SearchAndFilterPageViewModel(CardViewModel allCardsVM,EditCollectionViewModel addCardsVM,FilterViewModel filterVM,PricesViewModel pricesVM,CardImageViewModel cardImageVM,ObservableCollection<ObservableCollection<double>> columnWidths)
    {
        public CardViewModel AllCardsVM { get; } = allCardsVM;
        public EditCollectionViewModel AddCardsVM { get; } = addCardsVM;
        public FilterViewModel FilterVM { get; } = filterVM;
        public PricesViewModel PricesVM { get; } = pricesVM;
        public CardImageViewModel CardImageVM { get; } = cardImageVM;

        public ObservableCollection<ObservableCollection<double>> ColumnWidths { get; } = columnWidths;

        public FilterHeaderModel NameHeader => new("Name", FilterVM.Filters["Name"], ColumnWidths[0][0]);
        public FilterHeaderModel SetNameHeader => new("Set Name", FilterVM.Filters["SetName"], ColumnWidths[0][1]);
    }
}
