using CollectaMundo.ViewModels.Decks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectaMundo.ViewModels.Pages
{
    public partial class PagesDecksHostViewModel : ObservableObject
    {
        public DeckManagementViewModel DeckManagementVM { get; }
        public DeckEditorViewModel DeckEditorVM { get; }
        public PagesDecksHostViewModel(DeckManagementViewModel deckManagementVM, DeckEditorViewModel deckEditorVM)
        {
            DeckManagementVM = deckManagementVM;
            DeckEditorVM = deckEditorVM;

            currentDecksContentViewModel = DeckManagementVM;
        }

        [ObservableProperty]
        private object currentDecksContentViewModel;
        public async Task BeginAsync()
        {
            CurrentDecksContentViewModel = DeckManagementVM;
            await DeckManagementVM.LoadDecksAsync();
        }
    }
}
