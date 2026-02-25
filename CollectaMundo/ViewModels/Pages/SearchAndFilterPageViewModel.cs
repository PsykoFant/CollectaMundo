using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollectaMundo.ViewModels.Pages
{
    public sealed class SearchAndFilterPageViewModel(CardViewModel allCardsVM, EditCollectionViewModel addCardsVM, FilterViewModel filterVM, PricesViewModel pricesVM, CardImageViewModel cardImageVM, ObservableCollection<ObservableCollection<double>> columnWidths)
    {
        public CardViewModel AllCardsVM { get; } = allCardsVM;
        public EditCollectionViewModel AddCardsVM { get; } = addCardsVM;
        public FilterViewModel FilterVM { get; } = filterVM;
        public PricesViewModel PricesVM { get; } = pricesVM;
        public CardImageViewModel CardImageVM { get; } = cardImageVM;

        // Used for column widths.
        public ObservableCollection<ObservableCollection<double>> ColumnWidths { get; } = columnWidths;
    }
}
