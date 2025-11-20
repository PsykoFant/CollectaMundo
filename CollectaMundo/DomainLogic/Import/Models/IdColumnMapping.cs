using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectaMundo.DomainLogic.Import.Models
{
    public partial class IdColumnMapping : ObservableObject
    {
        [ObservableProperty]
        private List<string> csvHeaders = [];

        [ObservableProperty]
        private string? selectedCsvHeader;

        [ObservableProperty]
        private List<string> databaseFields = [];

        [ObservableProperty]
        private string? selectedDatabaseField;

    }
}
