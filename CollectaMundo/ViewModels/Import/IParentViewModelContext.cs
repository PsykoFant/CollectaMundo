using CollectaMundo.DomainLogic.Shared;
using System.Windows;

namespace CollectaMundo.ViewModels.Import
{
    public interface IParentViewModelContext
    {
        // UI visibility and enablement
        Visibility SideMenuVisibility { get; set; }
        Visibility CardViewSectionVisibility { get; set; }
        bool IsTopMenuEnabled { get; set; }

        // Cardlist and filter refresh
        ICollectionSnapshot CreateMyCollectionSnapshot();
        Task ReloadAllCardListsAndFiltersAsync();
        public void RefreshAllPrices();
        public void SetUiBusy(bool isBusy);
    }
}
