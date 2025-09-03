using CollectaMundo.ViewModels;

namespace CollectaMundo.ApplicationServices.CardLists
{
    public interface ICardListService
    {
        Task InitializeCardListsAsync(CardViewModel allCardsVM, CardViewModel myCollectionVM, Dictionary<string, FilterItemViewModel> filters, FilterViewModel filterVM);
        Task ReloadPriceLookupsAsync(string retailerKey);
    }
}
