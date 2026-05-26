using CollectaMundo.ApplicationServices.Navigation;
using CollectaMundo.ViewModels.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.ComponentModel;

namespace CollectaMundo.ViewModels.Shell
{
    public sealed partial class TopMenuViewModel : ObservableObject
    {
        private readonly IShellNavigationHost _shellNavigationHost;
        private readonly INavigationCleanupService _navigationCleanupService;

        // Page viewmodels
        public object AllCardsPageVM { get; }
        public object MyCollectionPageVM { get; }
        public object? PagesDecksHostVM { get; }
        public object? PagesUtilitiesHostVM { get; }

        // Sidemenu viewmodels
        public object FilteringSideMenuViewModel { get; }
        public object UtilitiesSideMenuViewModel { get; }

        public TopMenuViewModel(IShellNavigationHost shellNavigationHost, INavigationCleanupService navigationCleanupService, object filteringSideMenuViewModel, object utilitiesSideMenuViewModel, object allCardsPageViewModel, object myCollectionPageViewModel, object pagesDecksHostViewModel, object pagesUtilitiesHostVM)
        {
            _shellNavigationHost = shellNavigationHost;
            _navigationCleanupService = navigationCleanupService;
            FilteringSideMenuViewModel = filteringSideMenuViewModel;
            UtilitiesSideMenuViewModel = utilitiesSideMenuViewModel;
            AllCardsPageVM = allCardsPageViewModel;
            MyCollectionPageVM = myCollectionPageViewModel;
            PagesDecksHostVM = pagesDecksHostViewModel;
            PagesUtilitiesHostVM = pagesUtilitiesHostVM;

            _shellNavigationHost.PropertyChanged += Host_PropertyChanged;
        }

        public bool IsTopMenuEnabled => _shellNavigationHost.IsTopMenuEnabled;
        public bool IsAllCardsPageActive => _shellNavigationHost.CurrentPage == ShellPageEnum.SearchAndFilter;
        public bool IsMyCollectionPageActive => _shellNavigationHost.CurrentPage == ShellPageEnum.MyCollection;
        public bool IsDecksPageActive => _shellNavigationHost.CurrentPage == ShellPageEnum.Decks;
        public bool IsUtilitiesPageActive => _shellNavigationHost.CurrentPage == ShellPageEnum.Utilities;

        [RelayCommand]
        private void ShowAllCardsPage() => NavigateTo(AllCardsPageVM, ShellPageEnum.SearchAndFilter);

        [RelayCommand]
        private void ShowMyCollectionPage() => NavigateTo(MyCollectionPageVM, ShellPageEnum.MyCollection);

        [RelayCommand]
        private async Task ShowDecksPage()
        {
            NavigateTo(PagesDecksHostVM, ShellPageEnum.Decks);

            if (PagesDecksHostVM is PagesDecksHostViewModel decksHost)
            {
                await decksHost.BeginAsync();
            }
        }

        [RelayCommand]
        private void ShowUtilitiesPage() => NavigateTo(PagesUtilitiesHostVM, ShellPageEnum.Utilities);

        private void NavigateTo(object? pageViewModel, ShellPageEnum page)
        {
            if (pageViewModel is null)
            {
                return;
            }

            var oldPage = _shellNavigationHost.CurrentPageViewModel;

            _navigationCleanupService.CleanupBeforePageChange(oldPage, pageViewModel);

            _shellNavigationHost.CurrentPageViewModel = pageViewModel;
            _shellNavigationHost.CurrentSideMenuLeftViewModel = ResolveSideMenu(pageViewModel);
            _shellNavigationHost.CurrentPage = page;
        }
        private object? ResolveSideMenu(object pageViewModel)
        {
            if (ReferenceEquals(pageViewModel, PagesUtilitiesHostVM))
            {
                return UtilitiesSideMenuViewModel;
            }

            if (ReferenceEquals(pageViewModel, PagesDecksHostVM))
            {
                return null;
            }

            // all current card-list pages use filtering side menu
            return FilteringSideMenuViewModel;
        }
        private void Host_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IShellUiState.CurrentPage))
            {
                OnPropertyChanged(nameof(IsAllCardsPageActive));
                OnPropertyChanged(nameof(IsMyCollectionPageActive));
                OnPropertyChanged(nameof(IsDecksPageActive));
                OnPropertyChanged(nameof(IsUtilitiesPageActive));
            }

            if (e.PropertyName == nameof(IShellUiState.IsTopMenuEnabled))
            {
                OnPropertyChanged(nameof(IsTopMenuEnabled));
            }
        }
    }
}
