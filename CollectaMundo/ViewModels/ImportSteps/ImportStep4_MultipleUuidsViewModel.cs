using CollectaMundo.DomainLogic.Import.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace CollectaMundo.ViewModels.ImportSteps
{
    public partial class ImportStep4_MultipleUuidsViewModel(ImportViewModel parent) : ObservableObject, IImportStepViewModel
    {
        private readonly ImportViewModel _parent = parent;
        public ObservableCollection<ColumnMapping> Mappings => _parent.Mappings; // proxy to parent's mappings
        public string PrimaryActionButtonText => "  Proceed  \u27A1";
        public string SecondaryActionButtonText => "  Skip  \u23ED";
        public bool IsCancelEnabled => true;

        [ObservableProperty]
        private bool isSecondaryActionEnabled = true;

        [RelayCommand]
        private async Task PrimaryAction()
        {
            _parent.GoToStep(ImportStep.AdditionalFieldsMapping);
        }
        public void OnSecondaryAction()
        {
            Debug.WriteLine("ImportStep4_MultipleUuidsViewModel: SecondaryAction invoked - skipping Multiple UUIDs selection step.");
            _parent.GoToStep(ImportStep.AdditionalFieldsMapping);
        }

        [RelayCommand]
        private static void ClearSelectedMapping(ColumnMapping mapping)
        {
            // Clear the selected mappings
        }
    }
}
