using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;

namespace CollectaMundo.ViewModels
{
    public partial class StatusViewModel : ObservableObject
    {
        private Action<object?> _primaryAction;

        [ObservableProperty]
        private Visibility statusOverlayVisibility;

        [ObservableProperty]
        private Visibility logoVisibility = Visibility.Visible;

        [ObservableProperty]
        private Visibility setupFailVisibility = Visibility.Collapsed;

        [ObservableProperty]
        private Visibility progressVisibility = Visibility.Collapsed;

        [ObservableProperty]
        private int progressValue;

        [ObservableProperty]
        private Visibility primaryButtonVisibility = Visibility.Collapsed;

        [ObservableProperty]
        private string primaryButtonText = "OK";

        [ObservableProperty]
        private string statusLabel1 = string.Empty;

        [ObservableProperty]
        private string statusLabel2 = string.Empty;

        [ObservableProperty]
        private string statusLabel3 = string.Empty;
        public StatusViewModel()
        {
            _primaryAction = _ => HideStatusOverlay();
        }

        [RelayCommand]
        private void PrimaryButton(object? param)
        {
            _primaryAction(param);
        }

        public void SetPrimaryAction(Action<object?>? action) => _primaryAction = action ?? (_ => HideStatusOverlay());
        public void ShowStatusOverlay(string message, bool showProgress = false)
        {

            StatusOverlayVisibility = Visibility.Visible;
            StatusLabel1 = message;
            ProgressVisibility = showProgress ? Visibility.Visible : Visibility.Collapsed;
        }
        public void HideStatusOverlay()
        {
            StatusOverlayVisibility = Visibility.Collapsed;
            ResetStatusOverlay();
        }
        public void ResetStatusOverlay()
        {
            LogoVisibility = Visibility.Visible;
            ProgressVisibility = Visibility.Collapsed;
            PrimaryButtonVisibility = Visibility.Collapsed;
            SetupFailVisibility = Visibility.Collapsed;
            PrimaryButtonVisibility = Visibility.Collapsed;

            StatusLabel1 = string.Empty;
            StatusLabel2 = string.Empty;
            StatusLabel3 = string.Empty;

            ProgressValue = 0;
            PrimaryButtonText = "  OK  ";
        }
    }
}
