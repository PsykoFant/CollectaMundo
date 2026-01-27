using CollectaMundo.ApplicationServices.Import.Models;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.ViewModels;

namespace CollectaMundo.ApplicationServices.CardLists
{
    public interface ICardListService
    {
        Task InitializeCardListsAsync(CardViewModel allCardsVM, CardViewModel myCollectionVM, Dictionary<string, FilterItemViewModel> filters, FilterViewModel filterVM);
        Task ReloadPriceLookupsAsync(string retailerKey);
        CollectionChangeSet<CardSet> BuildCollectionChangeSet(CollectionMutation mutation, CardViewModel myCollection, CardViewModel allCards);
        void ApplyMyCollectionChanges(IList<CardSet> collection, CollectionChangeSet<CardSet> changes);
    }
}
