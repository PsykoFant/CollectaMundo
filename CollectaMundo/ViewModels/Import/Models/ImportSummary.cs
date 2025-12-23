using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace CollectaMundo.DomainLogic.Import.Models
{
    public partial class ImportSummary : ObservableObject
    {
        // Aggregates
        [ObservableProperty]
        private int readyToImportCount;

        [ObservableProperty]
        private int unableToImportCount;

        [ObservableProperty]
        private int totalCardsToAdd;

        [ObservableProperty]
        private int totalImportItems; // ImportCardList.Count        

        [ObservableProperty]
        private bool cardsOwnedMapped; // useful for showing "N/A" or tooltip

        // Detail table
        public ObservableCollection<UnimportableItem> UnimportableItems { get; } = [];

        // Convenience for XAML
        public bool HasUnimportableItems => UnableToImportCount > 0;

        partial void OnUnableToImportCountChanged(int value) => OnPropertyChanged(nameof(HasUnimportableItems));
        public void Reset()
        {
            ReadyToImportCount = 0;
            UnableToImportCount = 0;
            TotalCardsToAdd = 0;
            TotalImportItems = 0;
            CardsOwnedMapped = false;

            UnimportableItems.Clear();
        }
    }
}
