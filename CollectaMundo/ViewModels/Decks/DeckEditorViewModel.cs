using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CollectaMundo.ViewModels.Decks
{
    public partial class DeckEditorViewModel : ObservableObject
    {
        public event EventHandler? ExitEditorRequested;

        [RelayCommand]
        private void BackToDeckManagement()
        {
            ExitEditorRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
