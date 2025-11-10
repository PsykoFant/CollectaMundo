using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.Import.Models;
using CommunityToolkit.Mvvm.Input;
using System.Windows;

namespace CollectaMundo.ViewModels.ImportSteps
{
    public interface IImportStepViewModel
    {
        Task<OperationResult> OnPrimaryAction();
        void OnSecondaryAction();
        string PrimaryActionButtonText { get; }
        string SecondaryActionButtonText { get; }
        IRelayCommand<ColumnMapping> ClearSelectedMappingCommand { get; }
        Visibility SecondaryActionVisibility { get; }
        Visibility CancelVisibility { get; }
    }
}
