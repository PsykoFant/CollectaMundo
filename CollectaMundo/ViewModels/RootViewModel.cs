using CollectaMundo.ViewModels.Import;
using CollectaMundo.ViewModels.Shared;

namespace CollectaMundo.ViewModels
{
    public class RootViewModel(MainWindowViewModel main, OperationOverlayViewModel status)
    {
        public MainWindowViewModel Main { get; } = main;
        public OperationOverlayViewModel OperationOverlay { get; } = status;
        public ImportViewModel ImportOverlay => Main.ImportVM;
    }
}
