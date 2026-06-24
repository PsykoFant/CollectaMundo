using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace CollectaMundo.ViewModels.Decks
{
    public partial class DeckCardEntryViewModel : ObservableObject
    {
        public required string OracleId { get; init; }
        public required string CardName { get; init; }

        public ImageSource? ManaCostImage { get; init; }

        [ObservableProperty]
        private int desiredQuantity = 1;

        public int OwnedQuantity => 0;
        public int AllocatedQuantity => 0;
    }
}
