using CollectaMundo.ViewModels.Import;
using CollectaMundo.ViewModels.Shared;

namespace CollectaMundo.ViewModels
{
    public class RootViewModel(MainWindowViewModel main, OperationOverlayViewModel operationOverlayViewModel)
    {
        public MainWindowViewModel Main { get; } = main;
        public OperationOverlayViewModel OperationOverlayVM { get; } = operationOverlayViewModel;
        public ImportViewModel ImportOverlay => Main.ImportVM;
    }
}
