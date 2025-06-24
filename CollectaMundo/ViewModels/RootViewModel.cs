using System.ComponentModel;

namespace CollectaMundo.ViewModels
{
    public class RootViewModel(MainWindowViewModel main, StatusViewModel status) : INotifyPropertyChanged
    {
        public MainWindowViewModel Main { get; } = main;
        public StatusViewModel StatusOverlay { get; } = status;

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}