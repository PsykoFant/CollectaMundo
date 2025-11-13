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

        //  Step UI Info
        public string PrimaryActionButtonText => "  Let's go!  \u27A1";
        public string SecondaryActionButtonText => string.Empty; // No secondary action on first screen
        public Visibility SecondaryActionVisibility => Visibility.Collapsed;

        [ObservableProperty]
        private Visibility flowDocumentVisibility = Visibility.Visible;

        //  Step-level button enablement
        public bool CanExecutePrimaryAction => true;
        public bool CanExecuteSecondaryAction => false;

        //  Actions
        public async Task<OperationResult> OnPrimaryAction() => await _parent.AfterStep1Action();

        //  Clear Mapping Command
        [RelayCommand]
        private static void ClearSelectedMapping(ColumnMapping mapping) { } // no-op, no mappings to clear on first step

    }
}
