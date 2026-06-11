using CollectaMundo.ViewModels;
using CollectaMundo.ViewModels.Filtering;

namespace CollectaMundo.ApplicationServices.CardLists
{
    public interface ICardListService
    {
        Task InitializeCardListsAsync(CardListViewModel allCardsVM, CardListViewModel myCollectionVM, Dictionary<string, FilterItemViewModel> filters, FilterPanelViewModel filterVM);
        Task ReloadPriceLookupsAsync(string retailerKey);
    }
}
