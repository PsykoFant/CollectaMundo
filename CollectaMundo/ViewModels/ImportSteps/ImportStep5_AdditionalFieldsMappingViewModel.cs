using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.Import.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;

namespace CollectaMundo.ViewModels.ImportSteps
{
    public partial class ImportStep5_AdditionalFieldsMappingViewModel(ImportViewModel parent) : ObservableObject, IImportStepViewModel
    {
        private readonly ImportViewModel _parent = parent;
        public ObservableCollection<ColumnMapping> Mappings => _parent.Mappings; // proxy to parent's mappings
        public string PrimaryActionButtonText => "  Proceed  \u27A1";
        public string SecondaryActionButtonText => "  Skip  \u23ED";
        public Visibility CancelVisibility => Visibility.Visible;
        public Visibility SecondaryActionVisibility => Visibility.Collapsed;
        public async Task<OperationResult> OnPrimaryAction()
        {
            return await _parent.AfterStep5Action();
        }
        public void OnSecondaryAction()
        {
            _parent.GoToStep(ImportStep.AdditionalFieldsMapping);
        }

        [RelayCommand]
        private static void ClearSelectedMapping(ColumnMapping mapping)
        {
            // Clear the selected mappings
        }
    }
}
