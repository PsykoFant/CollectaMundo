using CommunityToolkit.Mvvm.Input;

namespace CollectaMundo.ViewModels.ImportSteps
{
    public interface IImportStepViewModel
    {
        string PrimaryActionButtonText { get; }
        string SecondaryActionButtonText { get; }
        IRelayCommand PrimaryActionCommand { get; }
        IRelayCommand SecondaryActionCommand { get; }
        bool IsSecondaryActionEnabled { get; }
        bool IsCancelEnabled { get; }
    }
}
