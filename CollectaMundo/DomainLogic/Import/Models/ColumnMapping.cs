using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectaMundo.DomainLogic.Import.Models
{

    public partial class ColumnMapping : ObservableObject
    {
        [ObservableProperty]
        private List<string> csvHeaders = [];

        [ObservableProperty]
        private List<string> databaseFields = [];

        [ObservableProperty]
        private string? selectedCsvHeader;

        [ObservableProperty]
        private string? selectedDatabaseField;
    }
}
