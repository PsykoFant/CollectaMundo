using CollectaMundo.ViewModels.Filtering;
using CollectaMundo.ViewModels.Shell;
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.Diagnostics;

namespace CollectaMundo.ViewModels.SideMenuLeft
{
    public sealed partial class SideMenuFilteringViewModel : ObservableObject
    {
        private readonly IShellUiState _shellUiState;

        public SideMenuFilteringViewModel(FilterViewModel filterVM, CardListViewModel colorIconsViewModel, IShellUiState shellUiState)
        {
            FilterVM = filterVM;
            ColorIconsViewModel = colorIconsViewModel;

            _shellUiState = shellUiState;

            _shellUiState.PropertyChanged += ShellUiState_PropertyChanged;
        }

        public FilterViewModel FilterVM { get; }
        public CardListViewModel ColorIconsViewModel { get; }

        public bool IsMyCollectionPageActive => _shellUiState.CurrentPage == ShellPageEnum.MyCollection;
        private void ShellUiState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IShellUiState.CurrentPage))
            {
                Debug.WriteLine($"[FilteringSideMenu] CurrentPage changed. IsMyCollectionPageActive={IsMyCollectionPageActive}");
                OnPropertyChanged(nameof(IsMyCollectionPageActive));
            }
        }
    }
}
