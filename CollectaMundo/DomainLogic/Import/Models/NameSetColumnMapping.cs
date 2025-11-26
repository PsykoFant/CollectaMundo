using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectaMundo.DomainLogic.Import.Models
{
    public partial class NameSetColumnMapping : ObservableObject
    {
        // Options from the CSV file (e.g. ["Name", "Card Name", "Set", ...])
        [ObservableProperty]
        private List<string> csvHeaders = [];

        // Selected CSV header
        [ObservableProperty]
        private string? selectedCsvHeader;

        // The logical field to map (Card Name, Set Name, Set Code)
        [ObservableProperty]
        private string fieldToMap = string.Empty;
    }
}
