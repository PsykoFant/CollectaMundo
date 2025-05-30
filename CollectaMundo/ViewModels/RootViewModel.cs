using CollectaMundo.ViewModels;
using System.ComponentModel;

public class RootViewModel : INotifyPropertyChanged
{
    public MainWindowViewModel Main { get; }
    public StatusViewModel StatusOverlay { get; }

    public RootViewModel(MainWindowViewModel main, StatusViewModel status)
    {
        Main = main;
        StatusOverlay = status;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
