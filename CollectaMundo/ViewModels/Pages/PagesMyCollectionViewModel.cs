using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.ViewModels.CardLists;
using CollectaMundo.ViewModels.Filtering;
using CollectaMundo.ViewModels.ModifyCollection;
using CollectaMundo.ViewModels.Pages.SharedElements;
using CollectaMundo.ViewModels.Shell.Models;
using System.Windows.Input;

namespace CollectaMundo.ViewModels.Pages
{
    public sealed class PagesMyCollectionViewModel(
        CardListViewModel<CollectionCard> cardsVM,
        FilterPanelViewModel filterVM,
        string pageTitle,
        ShellPageEnum cardListPage,
        string primarySubmitButtonText,
        ICommand? primarySubmitCommand = null,
        PricesViewModel? pricesVM = null,
        ModifyCollectionViewModel? modifyCollectionVM = null) : CardListPageViewModel<CollectionCard>(cardsVM, filterVM, pageTitle, cardListPage, primarySubmitButtonText, primarySubmitCommand, pricesVM, modifyCollectionVM)
    {
        // All the logic is in CardListPageViewModel, this class just serves to differentiate the page type for the view
    }
}
