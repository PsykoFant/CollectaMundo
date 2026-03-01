using CollectaMundo.ViewModels.Pages.SharedElements;
using System.Collections.ObjectModel;

namespace CollectaMundo.ViewModels.Pages
{
    public sealed class SearchAndFilterPageViewModel(CardViewModel allCardsVM, EditCollectionViewModel addCardsVM, FilterViewModel filterVM, PricesViewModel pricesVM, CardImageViewModel cardImageVM, ObservableCollection<ObservableCollection<double>> columnWidths) :
        CardListPageViewModelBase(cardsVM: allCardsVM, cardImageVM: cardImageVM, filterVM: filterVM, columnWidths: columnWidths, pricesVM: pricesVM, addOrEditCardsVM: addCardsVM)
    {

        // Convenience header models for the custom filter headers
        public FilterHeaderModel NameHeader => new("Name", FilterVM.Filters["Name"], ColumnWidths[0][0]);
        public FilterHeaderModel SetNameHeader => new("Set Name", FilterVM.Filters["SetName"], ColumnWidths[0][1]);
    }
}
