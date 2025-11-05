using CollectaMundo.DomainLogic.Import.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

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

        [RelayCommand]
        private async Task PrimaryAction()
        {
            IsSecondaryActionEnabled = false; // Disable cancel to prevent interruptions during processing
            await _parent.AfterStep2Action();
        }

        [RelayCommand]
        private void SecondaryAction()
        {
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
