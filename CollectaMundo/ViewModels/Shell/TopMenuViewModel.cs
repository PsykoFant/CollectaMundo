using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.ComponentModel;

namespace CollectaMundo.ViewModels.Shell;

public sealed partial class TopMenuViewModel(ITopMenuNavigationHost host, object allCardsPageViewModel, object myCollectionPageViewModel, object? decksPageViewModel = null, object? utilitiesPageViewModel = null) : ObservableObject
{
    private readonly ITopMenuNavigationHost _host = host;
    public object AllCardsPageViewModel { get; } = allCardsPageViewModel;
    public object MyCollectionPageViewModel { get; } = myCollectionPageViewModel;
    public object? DecksPageViewModel { get; } = decksPageViewModel;
    public object? UtilitiesPageViewModel { get; } = utilitiesPageViewModel;

    public bool IsTopMenuEnabled => _host.IsTopMenuEnabled;

    public bool IsAllCardsPageActive => ReferenceEquals(_host.CurrentPageViewModel, AllCardsPageViewModel);
    public bool IsMyCollectionPageActive => ReferenceEquals(_host.CurrentPageViewModel, MyCollectionPageViewModel);
    public bool IsDecksPageActive => DecksPageViewModel is not null &&ReferenceEquals(_host.CurrentPageViewModel, DecksPageViewModel);
    public bool IsUtilitiesPageActive => UtilitiesPageViewModel is not null &&ReferenceEquals(_host.CurrentPageViewModel, UtilitiesPageViewModel);


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
        if (pageViewModel is not null)
        {
            _host.CurrentPageViewModel = pageViewModel;
        }
    }
}
