using CollectaMundo.ViewModels.Pages.SharedElements;
using CollectaMundo.Views.Pages.SharedElements;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace CollectaMundo.ViewModels.Pages
{
    public class CardListPageViewModel : ObservableObject
    {
        public CardViewModel CardsVM { get; }
        public CardImageViewModel CardImageVM { get; }
        public FilterViewModel FilterVM { get; }
        public EditCollectionViewModel? AddCardsVM { get; }
        public PricesViewModel? PricesVM { get; }

        public IReadOnlyList<double> HeaderPaddings { get; }
        public ObservableCollection<double> ColumnWidths { get; }

        public FilterHeaderModel? NameHeader { get; private set; }
        public FilterHeaderModel? SetNameHeader { get; private set; }

        public CardListPageViewModel(
            CardViewModel cardsVM,
            CardImageViewModel cardImageVM,
            FilterViewModel filterVM,
            ColumnResizeSpec resizeSpec,
            PricesViewModel? pricesVM = null,
            EditCollectionViewModel? addOrEditCardsVM = null)
        {
            CardsVM = cardsVM;
            CardImageVM = cardImageVM;
            FilterVM = filterVM;
            PricesVM = pricesVM;
            AddCardsVM = addOrEditCardsVM;

            HeaderPaddings = resizeSpec.HeaderPaddings;

            var initial = resizeSpec.InitialComboWidths
                          ?? resizeSpec.HeaderPaddings.Select(_ => 50d).ToArray();

            ColumnWidths = new ObservableCollection<double>(initial);

            // Build if already ready
            TryBuildHeaders();

            // Build later when filters are rebuilt
            FilterVM.PropertyChanged += FilterVM_PropertyChanged;
        }

        private void FilterVM_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FilterViewModel.Filters))
            {
                TryBuildHeaders();
            }
        }

        private void TryBuildHeaders()
        {
            if (NameHeader != null && SetNameHeader != null)
            {
                return;
            }

            if (!FilterVM.Filters.TryGetValue("Name", out var nameItem))
            {
                return;
            }

            if (!FilterVM.Filters.TryGetValue("SetName", out var setItem))
            {
                return;
            }

            NameHeader = new FilterHeaderModel("Name", nameItem, colIndex: 0, initialComboWidth: ColumnWidths[0]);
            SetNameHeader = new FilterHeaderModel("Set Name", setItem, colIndex: 1, initialComboWidth: ColumnWidths[1]);

            OnPropertyChanged(nameof(NameHeader));
            OnPropertyChanged(nameof(SetNameHeader));
        }

        // Called by the resizer behavior:
        public void SetComboWidth(int col, double width)
        {
            if (col < 0 || col >= ColumnWidths.Count)
            {
                return;
            }

            ColumnWidths[col] = width;

            // push into the right header model so WPF refreshes
            if (NameHeader != null && NameHeader.ColIndex == col)
            {
                NameHeader.ComboWidth = width;
            }

            if (SetNameHeader != null && SetNameHeader.ColIndex == col)
            {
                SetNameHeader.ComboWidth = width;
            }
        }
    }
}
