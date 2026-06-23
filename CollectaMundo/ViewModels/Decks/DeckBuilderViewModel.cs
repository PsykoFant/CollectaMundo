using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.ViewModels.CardLists;
using CollectaMundo.ViewModels.Decks.Models;
using CollectaMundo.ViewModels.Filtering;
using CollectaMundo.ViewModels.SideMenuRight;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CollectaMundo.ViewModels.Decks
{
    public partial class DeckBuilderViewModel(CardListViewModel<OracleCard> oracleCardsVM, CardImageViewModel cardImageViewModel, FilterPanelViewModel filterPanelViewModel) : ObservableObject
    {
        public event EventHandler? ExitEditorRequested;
        public event EventHandler<OracleCardImageSelectionRequest?>? CardImageSelectionRequested;

        public CardListViewModel<OracleCard> CardsVM { get; } = oracleCardsVM;
        public CardImageViewModel CardImageVM { get; } = cardImageViewModel;
        public FilterPanelViewModel FilterVM { get; } = filterPanelViewModel;

        [ObservableProperty]
        private OracleCard? selectedOracleCard;

        partial void OnSelectedOracleCardChanged(OracleCard? value)
        {
            if (value is null)
            {
                CardImageSelectionRequested?.Invoke(this, new OracleCardImageSelectionRequest());
                return;
            }

            CardImageSelectionRequested?.Invoke(this, new OracleCardImageSelectionRequest(OracleId: value.ScryfallOracleId, Name: value.Name));
        }

        // Bindable pass-through properties for the filters 
        public FilterItemViewModel? NameFilter => FilterVM.Filters.TryGetValue("Name", out var f) ? f : null;

        [RelayCommand]
        private void BackToDeckManagement()
        {
            ExitEditorRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
