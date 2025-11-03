using CollectaMundo.DomainLogic.Import.Models;
using CommunityToolkit.Mvvm.Input;

namespace CollectaMundo.ViewModels.ImportSteps
{
    public interface IImportStepViewModel
    {
        string PrimaryActionButtonText { get; }
        string SecondaryActionButtonText { get; }
        IAsyncRelayCommand PrimaryActionCommand { get; }
        IRelayCommand SecondaryActionCommand { get; }
        IRelayCommand<ColumnMapping> ClearSelectedMappingCommand { get; }
        bool IsSecondaryActionEnabled { get; }
        bool IsCancelEnabled { get; }
    }
}
