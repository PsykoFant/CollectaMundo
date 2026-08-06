using CollectaMundo.DomainLogic.Decks.Models.Enums;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.ViewModels.ModifyCollection.BindinViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Media;

namespace CollectaMundo.ViewModels.Decks
{
    public partial class DeckCardEntryViewModel : ObservableObject
    {
        public required OracleCard OracleCard { get; init; }

        public string OracleId => OracleCard.ScryfallOracleId;
        public string CardName => OracleCard.Name;
        public double? ManaValue => OracleCard.ManaValue;
        public ImageSource? ManaCostImage => OracleCard.ManaCostImage;
        public string? Type => OracleCard.Type;
        
        [ObservableProperty]
        private int ownedQuantity;

        [ObservableProperty]
        private int allocatedQuantity;

        [ObservableProperty]
        private int desiredQuantity = 1;

        [ObservableProperty]
        private DeckSection section = DeckSection.Mainboard;

        [ObservableProperty]
        private bool isLegal;

        public NumericBindingViewModel DesiredQuantityBinding { get; }

        public DeckCardEntryViewModel(Func<DeckCardEntryViewModel, Task> quantityCommitAsync)
        {
            DesiredQuantityBinding = new NumericBindingViewModel(
                getter: () => DesiredQuantity,
                setter: value => DesiredQuantity = value,
                commitCommand: new AsyncRelayCommand(() => quantityCommitAsync(this)),
                min: 0,
                delayMs: 300);
        }

        partial void OnDesiredQuantityChanged(int value)
        {
            DesiredQuantityBinding.NotifyValueChanged();
        }
    }
}
