using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.Import.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;

namespace CollectaMundo.ViewModels.ImportSteps
{
    public partial class ImportStep04_MultipleUuidsViewModel(ImportViewModel parent) : ObservableObject, IImportStepViewModel
    {
        private readonly ImportViewModel _parent = parent;
        public ObservableCollection<IdColumnMapping> Mappings => _parent.IdMappings; // proxy to parent's mappings
        public string PrimaryActionButtonText => "  Proceed  \u27A1";
        public string SecondaryActionButtonText => "  Skip  \u23ED";
        public Visibility SecondaryActionVisibility => Visibility.Collapsed;
        public bool CanExecutePrimaryAction => true;
        public bool CanExecuteSecondaryAction => false;
        public async Task<OperationResult> OnPrimaryAction()
        {
            return await _parent.AfterStep4Action();
        }
        public void OnSecondaryAction()
        {
            Debug.WriteLine("ImportStep4_MultipleUuidsViewModel: SecondaryAction invoked - skipping Multiple UUIDs selection step.");
            _parent.GoToStep(ImportStep.AdditionalFieldsMapping);
        }

        [RelayCommand]
        private static void ClearSelectedMapping(IdColumnMapping mapping)
        {
            // Clear the selected mappings
        }
    }
}
