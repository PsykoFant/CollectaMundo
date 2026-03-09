using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.ComponentModel;
using System.Diagnostics;

namespace CollectaMundo.ViewModels.Shell;
public sealed partial class TopMenuViewModel : ObservableObject
{
    private readonly ITopMenuNavigationHost _host;

    public object AllCardsPageViewModel { get; }
    public object MyCollectionPageViewModel { get; }
    public object? DecksPageViewModel { get; }
    public object? UtilitiesPageViewModel { get; }

    public TopMenuViewModel(ITopMenuNavigationHost host, object allCardsPageViewModel, object myCollectionPageViewModel, object? decksPageViewModel = null, object? utilitiesPageViewModel = null)
    {
        _host = host;
        AllCardsPageViewModel = allCardsPageViewModel;
        MyCollectionPageViewModel = myCollectionPageViewModel;
        DecksPageViewModel = decksPageViewModel;
        UtilitiesPageViewModel = utilitiesPageViewModel;

        _host.PropertyChanged += Host_PropertyChanged;
    }

    public bool IsTopMenuEnabled => _host.IsTopMenuEnabled;

    public bool IsAllCardsPageActive => ReferenceEquals(_host.CurrentPageViewModel, AllCardsPageViewModel);
    public bool IsMyCollectionPageActive => ReferenceEquals(_host.CurrentPageViewModel, MyCollectionPageViewModel);
    public bool IsDecksPageActive => DecksPageViewModel is not null && ReferenceEquals(_host.CurrentPageViewModel, DecksPageViewModel);
    public bool IsUtilitiesPageActive => UtilitiesPageViewModel is not null && ReferenceEquals(_host.CurrentPageViewModel, UtilitiesPageViewModel);

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
    private void Host_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ITopMenuNavigationHost.CurrentPageViewModel))
        {
            OnPropertyChanged(nameof(IsAllCardsPageActive));
            OnPropertyChanged(nameof(IsMyCollectionPageActive));
            OnPropertyChanged(nameof(IsDecksPageActive));
            OnPropertyChanged(nameof(IsUtilitiesPageActive));
        }

        if (e.PropertyName == nameof(ITopMenuNavigationHost.IsTopMenuEnabled))
        {
            OnPropertyChanged(nameof(IsTopMenuEnabled));
        }

        Debug.WriteLine($"Host property changed: {e.PropertyName}");
    }
}
