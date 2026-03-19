using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ApplicationServices.Shell;
using CollectaMundo.ViewModels.Pages.SharedElements;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.ComponentModel;
using System.Diagnostics;

namespace CollectaMundo.ViewModels.Shell;
public sealed partial class TopMenuViewModel : ObservableObject
{
    private readonly IShellUiState _shellUIState;
    private readonly INavigationCleanupService _navigationCleanupService;

    // Page viewmodels
    public object AllCardsPageViewModel { get; }
    public object MyCollectionPageViewModel { get; }
    public object? DecksPageViewModel { get; }
    public object? UtilitiesPageViewModel { get; }

    // Sidemenu viewmodels
    public object FilteringSideMenuViewModel { get; }
    public object UtilitiesSideMenuViewModel { get; }

    public TopMenuViewModel(IShellUiState shellUIState, INavigationCleanupService navigationCleanupService, object filteringSideMenuViewModel, object utilitiesSideMenuViewModel, object allCardsPageViewModel, object myCollectionPageViewModel, object? decksPageViewModel = null, object? utilitiesPageViewModel = null)
    {
        _shellUIState = shellUIState;
        _navigationCleanupService = navigationCleanupService;

        FilteringSideMenuViewModel = filteringSideMenuViewModel;
        UtilitiesSideMenuViewModel = utilitiesSideMenuViewModel;
        AllCardsPageViewModel = allCardsPageViewModel;
        MyCollectionPageViewModel = myCollectionPageViewModel;
        DecksPageViewModel = decksPageViewModel;
        UtilitiesPageViewModel = utilitiesPageViewModel;

        _shellUIState.PropertyChanged += Host_PropertyChanged;
    }

    public bool IsTopMenuEnabled => _shellUIState.IsTopMenuEnabled;

    public bool IsAllCardsPageActive => ReferenceEquals(_shellUIState.CurrentPageViewModel, AllCardsPageViewModel);
    public bool IsMyCollectionPageActive => ReferenceEquals(_shellUIState.CurrentPageViewModel, MyCollectionPageViewModel);
    public bool IsDecksPageActive => DecksPageViewModel is not null && ReferenceEquals(_shellUIState.CurrentPageViewModel, DecksPageViewModel);
    public bool IsUtilitiesPageActive => ReferenceEquals(_shellUIState.CurrentPageViewModel, UtilitiesPageViewModel);

    [RelayCommand]
    private void ShowAllCardsPage() => NavigateTo(AllCardsPageViewModel);

    [RelayCommand]
    private void ShowMyCollectionPage() => NavigateTo(MyCollectionPageViewModel);

    [RelayCommand]
    private void ShowDecksPage() => NavigateTo(DecksPageViewModel);

    [RelayCommand]
    private void ShowUtilitiesPage() => NavigateTo(UtilitiesPageViewModel);

    private void NavigateTo(object? pageViewModel)
    {
        if (pageViewModel is null)
            return;

        var oldPage = _shellUIState.CurrentPageViewModel;

        _navigationCleanupService.CleanupBeforePageChange(oldPage, pageViewModel);

        _shellUIState.CurrentPageViewModel = pageViewModel;
        _shellUIState.CurrentSideMenuViewModel = ResolveSideMenu(pageViewModel);
    }
    private object? ResolveSideMenu(object pageViewModel)
    {
        if (ReferenceEquals(pageViewModel, UtilitiesPageViewModel))
        {
            return UtilitiesSideMenuViewModel;
        }

        // all current card-list pages use filtering side menu
        return FilteringSideMenuViewModel;
    }
    private void Host_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IShellUiState.CurrentPageViewModel))
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
