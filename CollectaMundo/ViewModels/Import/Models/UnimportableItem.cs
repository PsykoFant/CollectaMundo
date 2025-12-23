using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectaMundo.DomainLogic.Import.Models
{
    public partial class UnimportableItem : ObservableObject
    {
        // What the user sees
        [ObservableProperty]
        private string cardName = "Unknown";

        [ObservableProperty]
        private string setName = "Unknown";

        [ObservableProperty]
        private string setCode = "Unknown";

        // - for saving unmapped items
        // - for debugging
        // - for stable identification even if names are missing
        public string? TempItemImportKey { get; init; }

        // Raw line/row metadata 
        public int? RowNumber { get; init; } // if you track CSV line number
    }
}
