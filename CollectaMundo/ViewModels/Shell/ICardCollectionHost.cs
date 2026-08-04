using CollectaMundo.DomainLogic.Shared.CollectionSnapshot;

namespace CollectaMundo.ViewModels.Shell
{
    public interface ICardCollectionHost
    {
        // Cardlist and filter refresh
        ICollectionIdentitySnapshot CreateCollectionIdentitySnapshot();
        ICollectionQuantitySnapshot CreateCollectionQuantitySnapshot();
        Task ReloadAllCardListsAndFiltersAsync();
        Task RefreshAllPrices();
    }
}
