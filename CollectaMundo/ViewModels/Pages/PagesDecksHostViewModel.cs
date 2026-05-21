using CollectaMundo.ViewModels.Decks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectaMundo.ViewModels.Pages
{
    public partial class PagesDecksHostViewModel : ObservableObject
    {
        public DeckManagementViewModel DeckManagementVM { get; }
        public DeckEditorViewModel DeckEditorVM { get; }

        [ObservableProperty]
        private object currentDecksContentViewModel;

        public PagesDecksHostViewModel(DeckManagementViewModel deckManagementVM,DeckEditorViewModel deckEditorVM)
        {
            DeckManagementVM = deckManagementVM;
            DeckEditorVM = deckEditorVM;

            currentDecksContentViewModel = DeckManagementVM;
        }
    }
}
