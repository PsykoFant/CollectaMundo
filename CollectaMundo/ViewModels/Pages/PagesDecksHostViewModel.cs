using CollectaMundo.ViewModels.Decks;
using CollectaMundo.ViewModels.Decks.Models.RowViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectaMundo.ViewModels.Pages
{
    public partial class PagesDecksHostViewModel : ObservableObject
    {
        public event EventHandler? DecksContentChanged;
        public DeckManagementViewModel DeckManagementVM { get; }
        public DeckBuilderViewModel DeckBuilderVM { get; }
        public PagesDecksHostViewModel(DeckManagementViewModel deckManagementVM, DeckBuilderViewModel deckEditorVM)
        {
            DeckManagementVM = deckManagementVM;
            DeckBuilderVM = deckEditorVM;

            DeckManagementVM.EditDeckRequested += OnEditDeckRequested;
            DeckBuilderVM.ExitEditorRequested += OnExitEditorRequested;

            currentDecksContentViewModel = DeckManagementVM;
        }

        [ObservableProperty]
        private object currentDecksContentViewModel;
        private async void OnEditDeckRequested(object? sender, DeckManagementRowViewModel selectedDeck)
        {
            await DeckBuilderVM.BeginEditAsync(selectedDeck.Record);

            CurrentDecksContentViewModel = DeckBuilderVM;
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
