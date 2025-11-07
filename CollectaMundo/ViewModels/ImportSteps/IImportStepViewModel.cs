using CollectaMundo.DomainLogic.Import.Models;
using CommunityToolkit.Mvvm.Input;

namespace CollectaMundo.ViewModels.ImportSteps
{
    public interface IImportStepViewModel
    {
        string PrimaryActionButtonText { get; }
        string SecondaryActionButtonText { get; }
        IAsyncRelayCommand PrimaryActionCommand { get; }
        IRelayCommand<ColumnMapping> ClearSelectedMappingCommand { get; }
        bool IsSecondaryActionEnabled { get; set; }
        bool IsCancelEnabled { get; }
        void OnSecondaryAction();
    }
}
