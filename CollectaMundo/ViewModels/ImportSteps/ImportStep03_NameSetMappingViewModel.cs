using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.Import.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;

namespace CollectaMundo.ViewModels.ImportSteps
{
    public partial class ImportStep03_NameSetMappingViewModel(ImportViewModel parent) : ObservableObject, IImportStepViewModel
    {
        private readonly ImportViewModel _parent = parent;
        public ObservableCollection<ColumnMapping> Mappings => _parent.Mappings; // proxy to parent's mappings
        public string PrimaryActionButtonText => "  Proceed  \u27A1";
        public string SecondaryActionButtonText => "  Skip  \u23ED";
        public Visibility SecondaryActionVisibility => Visibility.Collapsed;
        public bool CanExecutePrimaryAction => true;
        public bool CanExecuteSecondaryAction => false;
        public async Task<OperationResult> OnPrimaryAction()
        {
            return await _parent.AfterStep3Action();
        }
        public void OnSecondaryAction()
        {
            Debug.WriteLine("ImportStep3_NameSetMappingViewModel: SecondaryAction invoked - go to AdditionalFieldsMapping");
            _parent.GoToStep(ImportStep.AdditionalFieldsMapping);
        }

        [RelayCommand]
        private static void ClearSelectedMapping(ColumnMapping mapping)
        {
            // Clear the selected mappings
        }
    }
}
