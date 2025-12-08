using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectaMundo.DomainLogic.Import.Models
{
    public partial class AdditionalFieldMapping : ObservableObject
    {
        [ObservableProperty]
        private string cardSetField = string.Empty;

        [ObservableProperty]
        private List<string> csvHeaders = [];

        [ObservableProperty]
        private string? selectedCsvHeader;
    }

}
