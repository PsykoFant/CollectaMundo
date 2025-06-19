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
        private string _statusLabelAboveBar = string.Empty;
        private string _statusLabelBelowBar = string.Empty;
        private string _statusLabelMain = string.Empty;

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
        public string StatusLabelAboveBar
        {
            get => _statusLabelAboveBar;
            set => SetField(ref _statusLabelAboveBar, value);
        }
        public string StatusLabelBelowBar
        {
            get => _statusLabelBelowBar;
            set => SetField(ref _statusLabelBelowBar, value);
        }
        public string StatusLabelMain
        {
            get => _statusLabelMain;
            set => SetField(ref _statusLabelMain, value);
        }
        public void Show(string message, bool showProgress = false)
        {
            IsVisible = true;
            StatusLabelMain = message;
            IsProgressVisible = showProgress;
        }
        public void Hide()
        {
            IsVisible = false;
            IsProgressVisible = false;
            StatusLabelMain = string.Empty;
            StatusLabelAboveBar = string.Empty;
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
