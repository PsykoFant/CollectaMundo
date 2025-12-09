using CollectaMundo.DomainLogic.Import.Models.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectaMundo.DomainLogic.Import.Models
{
    public partial class CsvFieldMapping : ObservableObject
    {
        // Options from the CSV file (e.g. ["Name", "Card Name", "Set", ...])
        [ObservableProperty]
        private List<string> csvHeaders = [];

        // Selected CSV header
        [ObservableProperty]
        private string? selectedCsvHeader;

        [ObservableProperty]
        private ImportField fieldToMap; // e.g. "Card Name", "Condition"
    }
}
