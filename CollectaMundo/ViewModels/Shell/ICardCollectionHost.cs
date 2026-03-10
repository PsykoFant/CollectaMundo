using CollectaMundo.DomainLogic.Shared;

namespace CollectaMundo.ViewModels.Shell
{
    public interface ICardCollectionHost : IShellUiState
    {
        // Cardlist and filter refresh
        ICollectionSnapshot CreateMyCollectionSnapshot();
        Task ReloadAllCardListsAndFiltersAsync();
        public void RefreshAllPrices();
    }
}
