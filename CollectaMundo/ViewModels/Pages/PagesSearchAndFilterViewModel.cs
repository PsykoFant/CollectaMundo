using CollectaMundo.ApplicationServices.Decks;
using CollectaMundo.DomainLogic.CardLocations.Models;
using CollectaMundo.DomainLogic.Decks.Models.Enums;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.ViewModels.CardLists;
using CollectaMundo.ViewModels.Filtering;
using CollectaMundo.ViewModels.ModifyCollection;
using CollectaMundo.ViewModels.Pages.Models;
using CollectaMundo.ViewModels.Pages.SharedElements;
using CollectaMundo.ViewModels.Shell.Models;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace CollectaMundo.ViewModels.Pages
{
    public sealed partial class PagesSearchAndFilterViewModel(
        CardListViewModel<PrintingCard> cardsVM,
        FilterPanelViewModel filterVM,
        IDeckBuilderService deckBuilderService,
        string pageTitle,
        ShellPageEnum cardListPage,
        string primarySubmitButtonText,
        ICommand? primarySubmitCommand = null,
        PricesViewModel? pricesVM = null,
        ModifyCollectionViewModel? modifyCollectionVM = null) : CardListPageViewModel<PrintingCard>(cardsVM, filterVM, pageTitle, cardListPage, primarySubmitButtonText, primarySubmitCommand, pricesVM, modifyCollectionVM)
    {
        private readonly IDeckBuilderService _deckBuilderService = deckBuilderService;
        private IReadOnlyList<CardLocation> _availableDecks = [];
        public IReadOnlyList<CardLocation> AvailableDecks => _availableDecks;
        public void SetAvailableDecks(IReadOnlyList<CardLocation> decks)
        {
            _availableDecks = decks;
            OnPropertyChanged(nameof(AvailableDecks));
        }

        [RelayCommand]
        private async Task AddSelectedCardsToDeckAsync(AddCardsToDeckParameter? parameter)
        {
            if (parameter is null)
            {
                return;
            }

            var oracleCards = parameter.SelectedItems.OfType<PrintingCard>().Select(card => card.Oracle).ToList();

            if (oracleCards.Count == 0)
            {
                return;
            }

            await _deckBuilderService.AddCardsToDeckAsync(parameter.DeckLocationId, oracleCards, quantity: 1, DeckSection.Mainboard);
        }
    }
}
