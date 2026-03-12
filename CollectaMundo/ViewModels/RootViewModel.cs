using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ViewModels.Import;

namespace CollectaMundo.ViewModels
{
    public class RootViewModel(MainWindowViewModel main, IOperationOverlayController operationOverlayController)
    {
        public MainWindowViewModel Main { get; } = main;
        public IOperationOverlayController OperationOverlayController { get; } = operationOverlayController;
        public ImportViewModel ImportOverlay => Main.ImportVM;
    }
}
