using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CollectaMundo.ViewModels
{
    public class StatusViewModel : INotifyPropertyChanged
    {
        private bool _isVisible;
        private bool _isLogoVisible = true;
        private bool _isSetupFailVisible = false;
        private bool _isProgressVisible;
        private int _progressValue;
        private string _statusLabel1 = string.Empty;
        private string _statusLabel2 = string.Empty;
        private string _statusLabel3 = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool IsVisible
        {
            get => _isVisible;
            set => SetField(ref _isVisible, value);
        }
        public bool IsLogoVisible
        {
            get => _isLogoVisible;
            set => SetField(ref _isLogoVisible, value);
        }
        public bool IsSetupFailVisible
        {
            get => _isSetupFailVisible;
            set => SetField(ref _isSetupFailVisible, value);
        }
        public bool IsProgressVisible
        {
            get => _isProgressVisible;
            set => SetField(ref _isProgressVisible, value);
        }
        public int ProgressValue
        {
            get => _progressValue;
            set => SetField(ref _progressValue, value);
        }
        public string StatusLabel1
        {
            get => _statusLabel1;
            set => SetField(ref _statusLabel1, value);
        }
        public string StatusLabel2
        {
            get => _statusLabel2;
            set => SetField(ref _statusLabel2, value);
        }
        public string StatusLabel3
        {
            get => _statusLabel3;
            set => SetField(ref _statusLabel3, value);
        }
        public void ShowStatusOverlay(string message, bool showProgress = false)
        {
            IsVisible = true;
            StatusLabel3 = message;
            IsProgressVisible = showProgress;
        }
        public void HideStatusOverlay()
        {
            IsVisible = false;
            IsProgressVisible = false;
            StatusLabel3 = string.Empty;
            StatusLabel1 = string.Empty;
        }
        private void SetField<T>(ref T field, T value, [CallerMemberName] string? propName = null)
        {
            if (!Equals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
            }
        }
    }
}
