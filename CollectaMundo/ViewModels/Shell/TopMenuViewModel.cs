using CollectaMundo.ViewModels.Shell.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CollectaMundo.ViewModels.Shell
{
    public sealed partial class TopMenuViewModel : ObservableObject
    {
        public event EventHandler<ShellPageEnum>? NavigationRequested;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAllCardsPageActive))]
        [NotifyPropertyChangedFor(nameof(IsMyCollectionPageActive))]
        [NotifyPropertyChangedFor(nameof(IsDecksPageActive))]
        [NotifyPropertyChangedFor(nameof(IsUtilitiesPageActive))]
        private ShellPageEnum currentPage;

        [ObservableProperty]
        private bool isTopMenuEnabled = true;

        public bool IsAllCardsPageActive => CurrentPage == ShellPageEnum.SearchAndFilter;
        public bool IsMyCollectionPageActive => CurrentPage == ShellPageEnum.MyCollection;
        public bool IsDecksPageActive => CurrentPage == ShellPageEnum.Decks;
        public bool IsUtilitiesPageActive => CurrentPage == ShellPageEnum.Utilities;

        [RelayCommand]
        private void ShowMyCollectionPage() =>
            NavigationRequested?.Invoke(this, ShellPageEnum.MyCollection);

        [RelayCommand]
        private void ShowAllCardsPage() =>
            NavigationRequested?.Invoke(this, ShellPageEnum.SearchAndFilter);

        [RelayCommand]
        private void ShowDecksPage() =>
            NavigationRequested?.Invoke(this, ShellPageEnum.Decks);

        [RelayCommand]
        private void ShowUtilitiesPage() =>
            NavigationRequested?.Invoke(this, ShellPageEnum.Utilities);
    }
}
