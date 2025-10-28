using CommunityToolkit.Mvvm.Input;

namespace CollectaMundo.ViewModels.ImportSteps
{
    public interface IImportStepViewModel
    {
        string ActionButtonText { get; }
        IRelayCommand ActionCommand { get; }
        IRelayCommand CancelCommand { get; }
        bool IsCancelEnabled { get; }
    }
}
