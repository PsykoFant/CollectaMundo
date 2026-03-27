using CollectaMundo.ApplicationServices.Shell;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.ComponentModel;

namespace CollectaMundo.ViewModels.Shell;
public sealed partial class TopMenuViewModel : ObservableObject
{
    private readonly IShellNavigationHost _shellNavigationHost;
    private readonly INavigationCleanupService _navigationCleanupService;

    // Page viewmodels
    public object AllCardsPageViewModel { get; }
    public object MyCollectionPageViewModel { get; }
    public object? DecksPageViewModel { get; }
    public object? PagesUtilitiesHostViewModel { get; }

    // Sidemenu viewmodels
    public object FilteringSideMenuViewModel { get; }
    public object UtilitiesSideMenuViewModel { get; }

    public TopMenuViewModel(IShellNavigationHost shellNavigationHost, INavigationCleanupService navigationCleanupService, object filteringSideMenuViewModel, object utilitiesSideMenuViewModel, object allCardsPageViewModel, object myCollectionPageViewModel, object? decksPageViewModel = null, object? pagesUtilitiesHostVM = null)
    {
        _shellNavigationHost = shellNavigationHost;
        _navigationCleanupService = navigationCleanupService;
        FilteringSideMenuViewModel = filteringSideMenuViewModel;
        UtilitiesSideMenuViewModel = utilitiesSideMenuViewModel;
        AllCardsPageViewModel = allCardsPageViewModel;
        MyCollectionPageViewModel = myCollectionPageViewModel;
        DecksPageViewModel = decksPageViewModel;
        PagesUtilitiesHostViewModel = pagesUtilitiesHostVM;

        _shellNavigationHost.PropertyChanged += Host_PropertyChanged;
    }

    public bool IsTopMenuEnabled => _shellNavigationHost.IsTopMenuEnabled;

    public bool IsAllCardsPageActive => _shellNavigationHost.CurrentPage == ShellPage.SearchAndFilter;
    public bool IsMyCollectionPageActive => _shellNavigationHost.CurrentPage == ShellPage.MyCollection;
    public bool IsDecksPageActive => _shellNavigationHost.CurrentPage == ShellPage.Decks;
    public bool IsUtilitiesPageActive => _shellNavigationHost.CurrentPage == ShellPage.Utilities;

    [RelayCommand]
    private void ShowAllCardsPage() => NavigateTo(AllCardsPageViewModel, ShellPage.SearchAndFilter);

    [RelayCommand]
    private void ShowMyCollectionPage() => NavigateTo(MyCollectionPageViewModel, ShellPage.MyCollection);

    [RelayCommand]
    private void ShowDecksPage() => NavigateTo(DecksPageViewModel, ShellPage.Decks);

    [RelayCommand]
    private void ShowUtilitiesPage() => NavigateTo(PagesUtilitiesHostViewModel, ShellPage.Utilities);


    private void NavigateTo(object? pageViewModel, ShellPage page)
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
        if (ReferenceEquals(pageViewModel, PagesUtilitiesHostViewModel))
        {
            return UtilitiesSideMenuViewModel;
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
