using CollectaMundo.ViewModels.Pages.SharedElements;
using System.Collections.ObjectModel;

namespace CollectaMundo.ViewModels.Pages
{
    // Base wrapper VM for pages that display a primary card list/grid and share common sub-viewmodels.
    public class CardListPageViewModelBase(CardViewModel cardsVM, CardImageViewModel cardImageVM, FilterViewModel filterVM, ObservableCollection<ObservableCollection<double>> columnWidths, PricesViewModel? pricesVM = null, EditCollectionViewModel? addOrEditCardsVM = null)
    {
        // Non-nullable VMs for features that are present on all pages.
        public CardViewModel CardsVM { get; } = cardsVM;
        public CardImageViewModel CardImageVM { get; } = cardImageVM;
        public FilterViewModel FilterVM { get; } = filterVM;
        public ObservableCollection<ObservableCollection<double>> ColumnWidths { get; } = columnWidths;

        // Nulllable VMs for features that may not be present on all pages.
        public EditCollectionViewModel? AddCardsVM { get; } = addOrEditCardsVM;
        public PricesViewModel? PricesVM { get; } = pricesVM;

        public FilterHeaderModel NameHeader => new("Name", FilterVM.Filters["Name"], ColumnWidths[0][0]);
        public FilterHeaderModel SetNameHeader => new("Set Name", FilterVM.Filters["SetName"], ColumnWidths[0][1]);
    }
}
