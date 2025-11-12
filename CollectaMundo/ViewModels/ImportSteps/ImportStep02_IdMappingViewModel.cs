using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.Import.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;

namespace CollectaMundo.ViewModels.ImportSteps
{
    public partial class ImportStep02_IdMappingViewModel(ImportViewModel parent) : ObservableObject, IImportStepViewModel
    {
        private readonly ImportViewModel _parent = parent;

        // Visibilities and button texts
        public string PrimaryActionButtonText => "  Proceed  \u27A1";
        public string SecondaryActionButtonText => "  Skip  \u23ED";
        public Visibility SecondaryActionVisibility => Visibility.Visible;

        // Invoked when buttons are clicked
        public async Task<OperationResult> OnPrimaryAction()
        {
            return await _parent.AfterStep2Action();
        }
        public void OnSecondaryAction()
        {
            _parent.GoToStep(ImportStep.NameAndSetMapping);
        }

        // Command to clear selected mapping
        [RelayCommand]
        private static void ClearSelectedMapping(ColumnMapping mapping)
        {
            mapping.SelectedCsvHeader = null;
            mapping.SelectedDatabaseField = null;
        }

        // proxy to parent's mappings (for easier binding)
        public ObservableCollection<ColumnMapping> Mappings => _parent.Mappings;
    }
}
