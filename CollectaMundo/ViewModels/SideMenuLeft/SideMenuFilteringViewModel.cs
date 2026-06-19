using CollectaMundo.ViewModels.CardLists;
using CollectaMundo.ViewModels.Filtering;
using CollectaMundo.ViewModels.Shell;
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

namespace CollectaMundo.ViewModels.SideMenuLeft
{
    public sealed partial class SideMenuFilteringViewModel : ObservableObject
    {
        private readonly IShellUiState _shellUiState;

        public SideMenuFilteringViewModel(FilterPanelViewModel filterVM, CardListViewModel<ManaSymbolViewModel> colorIconsViewModel, IShellUiState shellUiState)
        {
            FilterVM = filterVM;
            ColorIconsViewModel = colorIconsViewModel;

            _shellUiState = shellUiState;
            _shellUiState.PropertyChanged += ShellUiState_PropertyChanged;
        }

        public FilterPanelViewModel FilterVM { get; }
        public CardListViewModel<ManaSymbolViewModel> ColorIconsViewModel { get; }

        public bool IsPrintingCardFilteringVisble => (_shellUiState.CurrentPage == ShellPageEnum.SearchAndFilter || _shellUiState.CurrentPage == ShellPageEnum.MyCollection);
        public bool IsCollectionCardFilteringVisble => _shellUiState.CurrentPage == ShellPageEnum.MyCollection;

        private void ShellUiState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IShellUiState.CurrentPage))
            {
                OnPropertyChanged(nameof(IsCollectionCardFilteringVisble));
                OnPropertyChanged(nameof(IsPrintingCardFilteringVisble));
            }
        }
    }
}
