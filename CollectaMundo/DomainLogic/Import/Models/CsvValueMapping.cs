using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectaMundo.DomainLogic.Import.Models
{
    public partial class CsvValueMapping : ObservableObject
    {
        [ObservableProperty]
        private List<string> cardSetValues = []; // Available card set values from the database - used for Conditions, Finish and Language Mapping (e.g. "Near Mint", "Foil", "English" etc.)

        [ObservableProperty]
        private string csvValue = string.Empty; // Possible value from the CSV file for this field (e.g. "Near Mint", "Foil", "English" etc.)

        [ObservableProperty]
        private string? selectedCsvValue;
    }
}
