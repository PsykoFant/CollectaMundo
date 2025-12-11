using CollectaMundo.DomainLogic.Import.Models.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectaMundo.DomainLogic.Import.Models
{
    public partial class CsvFieldMapping : ObservableObject
    {
        [ObservableProperty]
        private List<string> csvHeaders = []; // Options from the CSV file (e.g. ["Name", "Card Name", "Set", ...])

        [ObservableProperty]
        private string? selectedCsvHeader; // Selected CSV header

        [ObservableProperty]
        private ImportField fieldToMap; // e.g. "Card Name", "Condition"
    }
}
