using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.Import.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;

namespace CollectaMundo.ViewModels.ImportSteps
{
    public partial class ImportStep01_StartViewModel(ImportViewModel parent) : ObservableObject, IImportStepViewModel
    {
        private readonly ImportViewModel _parent = parent;

        // Visibilities and button texts
        [ObservableProperty]
        private Visibility flowDocumentVisibility = Visibility.Visible;
        public Visibility SecondaryActionVisibility => Visibility.Collapsed;
        public string PrimaryActionButtonText => "  Let's go!  \u27A1";
        public string SecondaryActionButtonText => string.Empty; // No secondary action on first screen        

        // Invoked when buttons are clicked
        public async Task<OperationResult> OnPrimaryAction()
        {
            return await _parent.AfterStep1Action();
        }
        public void OnSecondaryAction()
        {
            // no-op, no secondary action on first step

        }

        // Command to clear selected mapping
        [RelayCommand]
        private static void ClearSelectedMapping(ColumnMapping mapping)
        {
            // no-op, no mappings to clear on first step
        }
    }
}
