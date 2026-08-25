using CollectaMundo.DomainLogic.Decks.Models.Enums;
using CollectaMundo.ViewModels.ModifyCollection.BindinViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CollectaMundo.ViewModels.Decks.Models.RowViewModels
{
    public partial class DeckCardEntryViewModel : OracleCardRowViewModel
    {
        private readonly Action<DeckCardEntryViewModel>? _desiredQuantityChanged;

        [ObservableProperty]
        private int desiredQuantity = 1;

        [ObservableProperty]
        private int availableQuantity;

        [ObservableProperty]
        private int ownedQuantity;

        [ObservableProperty]
        private int allocatedQuantity;

        [ObservableProperty]
        private DeckSection section = DeckSection.Mainboard;

        [ObservableProperty]
        private bool isLegal;

        [ObservableProperty]
        private bool hasInsufficientAvailableQuantity;

        public NumericBindingViewModel DesiredQuantityBinding { get; }

        public DeckCardEntryViewModel(Func<DeckCardEntryViewModel, Task> quantityCommitAsync, Action<DeckCardEntryViewModel>? desiredQuantityChanged = null)
        {
            _desiredQuantityChanged = desiredQuantityChanged;

            DesiredQuantityBinding = new NumericBindingViewModel(
                getter: () => DesiredQuantity,
                setter: value => DesiredQuantity = value,
                commitCommand: new AsyncRelayCommand(() => quantityCommitAsync(this)), min: 0, delayMs: 300);
        }
        partial void OnDesiredQuantityChanged(int value)
        {
            DesiredQuantityBinding.NotifyValueChanged();
            _desiredQuantityChanged?.Invoke(this);
        }
    }
}
