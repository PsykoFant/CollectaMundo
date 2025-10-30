using System.Windows;

namespace CollectaMundo.ViewModels.Shared
{
    public interface IParentViewModelContext
    {
        // UI visibility and enablement
        Visibility SideMenuVisibility { get; set; }
        Visibility CardViewSectionVisibility { get; set; }
        bool IsTopMenuEnabled { get; set; }

        // Cardlist and filter refresh
        Task ReloadAllCardListsAndFiltersAsync();
        public void RefreshAllPrices();
    }
}
