using CollectaMundo.ViewModels.CardLists;
using CollectaMundo.ViewModels.Filtering;
using CollectaMundo.ViewModels.Shell;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectaMundo.ViewModels.SideMenuLeft
{
    public sealed partial class SideMenuFilteringViewModel(FilterPanelViewModel filterVM, CardListViewModel<ManaSymbolViewModel> colorIconsViewModel) : ObservableObject
    {
        private ShellPageEnum currentShellPageContext;

        public FilterPanelViewModel FilterVM { get; } = filterVM;
        public CardListViewModel<ManaSymbolViewModel> ColorIconsViewModel { get; } = colorIconsViewModel;
        public void SetContext(ShellPageEnum context)
        {
            if (currentShellPageContext == context)
            {
                return;
            }

            currentShellPageContext = context;

            OnPropertyChanged(nameof(IsPrintingCardFilteringVisible));
            OnPropertyChanged(nameof(IsCollectionCardFilteringVisible));
        }
        public bool IsPrintingCardFilteringVisible => currentShellPageContext is ShellPageEnum.SearchAndFilter or ShellPageEnum.MyCollection;
        public bool IsCollectionCardFilteringVisible => currentShellPageContext is ShellPageEnum.MyCollection;
    }
}
