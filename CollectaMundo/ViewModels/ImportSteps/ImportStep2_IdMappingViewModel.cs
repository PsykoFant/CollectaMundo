using CollectaMundo.DomainLogic.Import.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace CollectaMundo.ViewModels.ImportSteps
{
    public partial class ImportStep2_IdMappingViewModel(ImportViewModel parent) : ObservableObject, IImportStepViewModel
    {
        private readonly ImportViewModel _parent = parent;
        public ObservableCollection<ColumnMapping> Mappings => _parent.Mappings; // proxy to parent's mappings
        public string PrimaryActionButtonText => "  Proceed  \u27A1";
        public string SecondaryActionButtonText => "  Skip  \u23ED";
        public bool IsCancelEnabled => true;

        [ObservableProperty]
        private bool isSecondaryActionEnabled = true;

        [ObservableProperty] // Controls whether SecondaryActionCommand can execute
        private bool isProcessing = false;
        private bool CanExecuteSecondaryAction => !IsProcessing; // Guard: only allow skip if not processing

        [RelayCommand]
        private async Task PrimaryAction()
        {
            try
            {
                IsSecondaryActionEnabled = false;
                await _parent.AfterStep2Action();
            }
            finally
            {
                IsSecondaryActionEnabled = true;
            }
        }
        public void OnSecondaryAction()
        {
            Debug.WriteLine("ImportStep2_IdMappingViewModel: SecondaryAction invoked - skipping");
            _parent.GoToStep(ImportStep.NameAndSetMapping);
        }

        [RelayCommand]
        private static void ClearSelectedMapping(ColumnMapping mapping)
        {
            mapping.SelectedCsvHeader = null;
            mapping.SelectedDatabaseField = null;
        }
    }
}
