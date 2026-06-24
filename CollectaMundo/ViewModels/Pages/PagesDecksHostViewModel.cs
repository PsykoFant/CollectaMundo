using CollectaMundo.ViewModels.Decks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectaMundo.ViewModels.Pages
{
    public partial class PagesDecksHostViewModel : ObservableObject
    {
        public event EventHandler? DecksContentChanged;
        public DeckManagementViewModel DeckManagementVM { get; }
        public DeckBuilderViewModel DeckEditorVM { get; }
        public PagesDecksHostViewModel(DeckManagementViewModel deckManagementVM, DeckBuilderViewModel deckEditorVM)
        {
            DeckManagementVM = deckManagementVM;
            DeckEditorVM = deckEditorVM;

            DeckManagementVM.EditDeckRequested += OnEditDeckRequested;
            DeckEditorVM.ExitEditorRequested += OnExitEditorRequested;

            currentDecksContentViewModel = DeckManagementVM;
        }

        [ObservableProperty]
        private object currentDecksContentViewModel;
        private async void OnEditDeckRequested(object? sender, DeckManagementRowViewModel selectedDeck)
        {
            await DeckEditorVM.BeginEditAsync(selectedDeck.Record);

            CurrentDecksContentViewModel = DeckEditorVM;
            DecksContentChanged?.Invoke(this, EventArgs.Empty);
        }
        private void OnExitEditorRequested(object? sender, EventArgs e)
        {
            CurrentDecksContentViewModel = DeckManagementVM;
            DecksContentChanged?.Invoke(this, EventArgs.Empty);
        }
        public async Task BeginAsync()
        {
            CurrentDecksContentViewModel = DeckManagementVM;
            await DeckManagementVM.LoadDecksAsync();
        }
    }
}
