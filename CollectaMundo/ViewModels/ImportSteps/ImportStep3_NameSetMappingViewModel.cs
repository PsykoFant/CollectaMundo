using CollectaMundo.DomainLogic.Import.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace CollectaMundo.ViewModels.ImportSteps
{
    public partial class ImportStep3_NameSetMappingViewModel(ImportViewModel parent) : ObservableObject, IImportStepViewModel
    {
        private readonly ImportViewModel _parent = parent;
        public ObservableCollection<ColumnMapping> Mappings => _parent.Mappings; // proxy to parent's mappings
        public string PrimaryActionButtonText => "  Proceed  \u27A1";
        public string SecondaryActionButtonText => "  Skip  \u23ED";
        public bool IsCancelEnabled => true;
        public bool IsSecondaryActionEnabled => true;

        [RelayCommand]
        private async Task PrimaryAction()
        {
            _parent.GoToNextStep();
        }

        [RelayCommand]
        private void SecondaryAction()
        {
            _parent.GoToNextStep();
        }

        [RelayCommand]
        private static void ClearSelectedMapping(ColumnMapping mapping)
        {
            // Clear the selected mappings
        }
    }
}
