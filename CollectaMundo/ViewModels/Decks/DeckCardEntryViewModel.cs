using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace CollectaMundo.ViewModels.Decks
{
    public partial class DeckCardEntryViewModel : ObservableObject
    {
        public required OracleCard OracleCard { get; init; }
        public string OracleId => OracleCard.ScryfallOracleId;
        public string CardName => OracleCard.Name;
        public double? ManaValue => OracleCard?.ManaValue;

        public ImageSource? ManaCostImage => OracleCard?.ManaCostImage;

        public int OwnedQuantity => 0;
        public int AllocatedQuantity => 0;

        [ObservableProperty]
        private int desiredQuantity = 1;

        [ObservableProperty]
        private DeckSection section = DeckSection.Mainboard;

    }
}
