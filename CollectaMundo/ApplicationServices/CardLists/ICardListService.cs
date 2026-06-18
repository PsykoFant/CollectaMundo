using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.ViewModels.CardLists;
using CollectaMundo.ViewModels.Filtering;

namespace CollectaMundo.ApplicationServices.CardLists
{
    public interface ICardListService
    {
        Task InitializeCardListsAsync(CardListViewModel<PrintingCard> allCardsVM, CardListViewModel<CollectionCard> myCollectionVM, CardListViewModel<OracleCard> oracleCardsVM, Dictionary<string, FilterItemViewModel> filters, FilterPanelViewModel filterVM);
        Task ReloadPriceLookupsAsync(string retailerKey);
    }
}
