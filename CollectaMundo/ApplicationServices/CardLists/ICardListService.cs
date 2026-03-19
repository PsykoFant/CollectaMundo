using CollectaMundo.ViewModels;
using CollectaMundo.ViewModels.Filtering;

namespace CollectaMundo.ApplicationServices.CardLists
{
    public interface ICardListService
    {
        Task InitializeCardListsAsync(CardViewModel allCardsVM, CardViewModel myCollectionVM, Dictionary<string, FilterItemViewModel> filters, FilterViewModel filterVM);
        Task ReloadPriceLookupsAsync(string retailerKey);
    }
}
