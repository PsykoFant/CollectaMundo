using CollectaMundo.ViewModels.Import;

namespace CollectaMundo.ViewModels
{
    public class RootViewModel(MainWindowViewModel main, StatusViewModel status)
    {
        public MainWindowViewModel Main { get; } = main;
        public StatusViewModel StatusOverlay { get; } = status;
        public ImportViewModel ImportOverlay => Main.ImportVM;

    }
}
